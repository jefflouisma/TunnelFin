using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Indexers.HtmlScrapers;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Models;

namespace TunnelFin.Indexers;

/// <summary>
/// Aggregates multiple indexers (Torznab and HTML scrapers) for content discovery.
/// Handles parallel queries, result merging, deduplication, and rate limiting.
/// </summary>
public class IndexerManager : IIndexerManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IndexerManager>? _logger;
    private readonly ConcurrentDictionary<Guid, IndexerConfig> _indexers;
    private readonly ConcurrentDictionary<string, (int FailureCount, DateTime CooldownUntil)> _indexerHealth;

    // Built-in scrapers
    private readonly Scraper1337x _scraper1337x;
    private readonly ScraperNyaa _scraperNyaa;
    private readonly ScraperTorrentGalaxy _scraperTorrentGalaxy;
    private readonly ScraperEZTV _scraperEZTV;

    public IndexerManager(HttpClient httpClient, ILogger<IndexerManager>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        _indexers = new ConcurrentDictionary<Guid, IndexerConfig>();
        _indexerHealth = new ConcurrentDictionary<string, (int, DateTime)>();

        // Initialize built-in scrapers
        _scraper1337x = new Scraper1337x(httpClient, logger as ILogger<Scraper1337x>);
        _scraperNyaa = new ScraperNyaa(httpClient, logger as ILogger<ScraperNyaa>);
        _scraperTorrentGalaxy = new ScraperTorrentGalaxy(httpClient, logger as ILogger<ScraperTorrentGalaxy>);
        _scraperEZTV = new ScraperEZTV(httpClient, logger as ILogger<ScraperEZTV>);
    }

    /// <summary>
    /// Searches all enabled indexers in parallel, merges results, deduplicates by InfoHash, and sorts by seeders.
    /// </summary>
    public async Task<IReadOnlyList<TorrentResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        _logger?.LogInformation("Searching all enabled indexers for query '{Query}'", query);

        var tasks = new List<Task<IReadOnlyList<TorrentResult>>>();

        // Query all enabled indexers in parallel
        foreach (var indexer in _indexers.Values.Where(i => i.Enabled))
        {
            // Check if indexer is in cooldown
            if (_indexerHealth.TryGetValue(indexer.Id.ToString(), out var health))
            {
                if (health.CooldownUntil > DateTime.UtcNow)
                {
                    _logger?.LogDebug("Skipping indexer {IndexerName} (in cooldown until {CooldownUntil})",
                        indexer.Name, health.CooldownUntil);
                    continue;
                }
            }

            tasks.Add(SearchIndexerInternalAsync(indexer, query, cancellationToken));
        }

        // Wait for all queries to complete
        var results = await Task.WhenAll(tasks);

        // Merge and deduplicate results
        var merged = results
            .SelectMany(r => r)
            .GroupBy(r => r.InfoHash)
            .Select(g => g.First()) // Take first occurrence of each InfoHash
            .OrderByDescending(r => r.Seeders ?? 0) // Sort by seeders descending
            .ToList();

        _logger?.LogInformation("Merged {Count} unique results from {IndexerCount} indexers for query '{Query}'",
            merged.Count, tasks.Count, query);

        return merged;
    }

    /// <summary>
    /// Searches a specific indexer by ID.
    /// </summary>
    public async Task<IReadOnlyList<TorrentResult>> SearchIndexerAsync(
        Guid indexerId,
        string query,
        CancellationToken cancellationToken)
    {
        if (!_indexers.TryGetValue(indexerId, out var indexer))
            throw new KeyNotFoundException($"Indexer {indexerId} not found");

        return await SearchIndexerInternalAsync(indexer, query, cancellationToken);
    }

    /// <summary>
    /// Internal method to search a specific indexer with error handling.
    /// </summary>
    private async Task<IReadOnlyList<TorrentResult>> SearchIndexerInternalAsync(
        IndexerConfig indexer,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TorrentResult> results;

            if (indexer.Type == IndexerType.Torznab)
            {
                var client = new TorznabClient(_httpClient, _logger as ILogger<TorznabClient>);
                results = await client.SearchAsync(indexer, query, cancellationToken);
            }
            else if (indexer.Type == IndexerType.BuiltIn)
            {
                results = indexer.Name.ToLowerInvariant() switch
                {
                    "1337x" => await _scraper1337x.SearchAsync(query, cancellationToken),
                    "nyaa" => await _scraperNyaa.SearchAsync(query, cancellationToken),
                    "torrentgalaxy" => await _scraperTorrentGalaxy.SearchAsync(query, cancellationToken),
                    "eztv" => await _scraperEZTV.SearchAsync(query, cancellationToken),
                    _ => Array.Empty<TorrentResult>()
                };
            }
            else
            {
                results = Array.Empty<TorrentResult>();
            }

            // Reset failure count on success
            _indexerHealth.AddOrUpdate(
                indexer.Id.ToString(),
                (0, DateTime.MinValue),
                (key, old) => (0, DateTime.MinValue)
            );

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search indexer {IndexerName}", indexer.Name);

            // Track failures and apply cooldown after 3 consecutive failures
            _indexerHealth.AddOrUpdate(
                indexer.Id.ToString(),
                (1, DateTime.MinValue),
                (key, old) =>
                {
                    var newCount = old.FailureCount + 1;
                    var cooldown = newCount >= 3 ? DateTime.UtcNow.AddMinutes(5) : DateTime.MinValue;
                    return (newCount, cooldown);
                }
            );

            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Gets all configured indexers.
    /// </summary>
    public IReadOnlyList<IndexerConfig> GetIndexers()
    {
        return _indexers.Values.ToList();
    }

    /// <summary>
    /// Adds a new indexer configuration.
    /// </summary>
    public async Task AddIndexerAsync(IndexerConfig config, CancellationToken cancellationToken)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Test indexer before adding
        var isValid = await TestIndexerAsync(config, cancellationToken);
        if (!isValid)
            throw new InvalidOperationException($"Indexer {config.Name} failed connectivity test");

        _indexers[config.Id] = config;
        _logger?.LogInformation("Added indexer {IndexerName} ({Type})", config.Name, config.Type);
    }

    /// <summary>
    /// Updates an existing indexer configuration.
    /// </summary>
    public async Task UpdateIndexerAsync(IndexerConfig config, CancellationToken cancellationToken)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (!_indexers.ContainsKey(config.Id))
            throw new KeyNotFoundException($"Indexer {config.Id} not found");

        // Test indexer before updating
        var isValid = await TestIndexerAsync(config, cancellationToken);
        if (!isValid)
            throw new InvalidOperationException($"Indexer {config.Name} failed connectivity test");

        _indexers[config.Id] = config;
        _logger?.LogInformation("Updated indexer {IndexerName}", config.Name);
    }

    /// <summary>
    /// Removes an indexer configuration.
    /// </summary>
    public Task RemoveIndexerAsync(Guid indexerId, CancellationToken cancellationToken)
    {
        if (_indexers.TryRemove(indexerId, out var config))
        {
            _logger?.LogInformation("Removed indexer {IndexerName}", config.Name);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Tests connectivity to an indexer.
    /// </summary>
    public async Task<bool> TestIndexerAsync(IndexerConfig config, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogDebug("Testing indexer {IndexerName}", config.Name);

            // Perform a test search with a common query
            var results = await SearchIndexerInternalAsync(config, "test", cancellationToken);

            // Consider it valid if we get any results or no exception was thrown
            _logger?.LogInformation("Indexer {IndexerName} test successful ({Count} results)",
                config.Name, results.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Indexer {IndexerName} test failed", config.Name);
            return false;
        }
    }
}

