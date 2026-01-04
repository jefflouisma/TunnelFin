using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Indexers.HtmlScrapers;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Metadata;
using TunnelFin.Models;
using IndexerConfig = TunnelFin.Configuration.IndexerConfig;

namespace TunnelFin.Indexers;

/// <summary>
/// Aggregates multiple indexers (Torznab and HTML scrapers) for content discovery.
/// Handles parallel queries, result merging, deduplication, and rate limiting.
/// Supports Prowlarr integration for centralized indexer management.
/// </summary>
public class IndexerManager : IIndexerManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IndexerManager>? _logger;
    private readonly ConcurrentDictionary<Guid, IndexerConfig> _indexers;
    private readonly ConcurrentDictionary<string, (int FailureCount, DateTime CooldownUntil)> _indexerHealth;
    private List<ProwlarrIndexer>? _prowlarrIndexers;
    private DateTime _prowlarrIndexersLastFetch = DateTime.MinValue;

    // Built-in scrapers
    private readonly Scraper1337x _scraper1337x;
    private readonly ScraperNyaa _scraperNyaa;
    private readonly ScraperTorrentGalaxy _scraperTorrentGalaxy;
    private readonly ScraperEZTV _scraperEZTV;

    // TMDB client for metadata enrichment
    private readonly ITmdbClient _tmdbClient;

    public IndexerManager(HttpClient httpClient, ITmdbClient tmdbClient, ILogger<IndexerManager>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tmdbClient = tmdbClient ?? throw new ArgumentNullException(nameof(tmdbClient));
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
    /// Gets Prowlarr configuration from the plugin.
    /// </summary>
    private (bool Enabled, string Url, string ApiKey) GetProwlarrConfig()
    {
        var config = Core.Plugin.Instance?.Configuration;
        if (config == null)
            return (false, string.Empty, string.Empty);

        return (config.ProwlarrEnabled, config.ProwlarrUrl, config.ProwlarrApiKey);
    }

    /// <summary>
    /// Fetches available indexers from Prowlarr.
    /// </summary>
    private async Task<List<ProwlarrIndexer>> FetchProwlarrIndexersAsync(CancellationToken cancellationToken)
    {
        var (enabled, url, apiKey) = GetProwlarrConfig();

        if (!enabled || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            return new List<ProwlarrIndexer>();

        // Cache indexers for 5 minutes
        if (_prowlarrIndexers != null && DateTime.UtcNow - _prowlarrIndexersLastFetch < TimeSpan.FromMinutes(5))
            return _prowlarrIndexers;

        try
        {
            var indexerUrl = $"{url.TrimEnd('/')}/api/v1/indexer?apikey={apiKey}";
            _logger?.LogDebug("Fetching Prowlarr indexers from {Url}", indexerUrl);

            var response = await _httpClient.GetStringAsync(indexerUrl, cancellationToken);
            var indexers = JsonSerializer.Deserialize<List<ProwlarrIndexer>>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<ProwlarrIndexer>();

            // Filter to only enabled indexers that support search
            _prowlarrIndexers = indexers.Where(i => i.Enable && i.SupportsSearch).ToList();
            _prowlarrIndexersLastFetch = DateTime.UtcNow;

            _logger?.LogInformation("Fetched {Count} enabled Prowlarr indexers", _prowlarrIndexers.Count);
            return _prowlarrIndexers;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch Prowlarr indexers");
            return _prowlarrIndexers ?? new List<ProwlarrIndexer>();
        }
    }

    /// <summary>
    /// Searches via Prowlarr API.
    /// </summary>
    private async Task<IReadOnlyList<TorrentResult>> SearchProwlarrAsync(string query, CancellationToken cancellationToken)
    {
        var (enabled, url, apiKey) = GetProwlarrConfig();

        if (!enabled || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            return Array.Empty<TorrentResult>();

        try
        {
            // Use Prowlarr's search endpoint which searches all enabled indexers
            var searchUrl = $"{url.TrimEnd('/')}/api/v1/search?query={Uri.EscapeDataString(query)}&type=search&apikey={apiKey}";
            _logger?.LogDebug("Searching Prowlarr: {Url}", searchUrl);

            var response = await _httpClient.GetStringAsync(searchUrl, cancellationToken);
            var results = JsonSerializer.Deserialize<List<ProwlarrSearchResult>>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<ProwlarrSearchResult>();

            _logger?.LogInformation("Prowlarr returned {Count} results for query '{Query}'", results.Count, query);

            // Filter and transform results, logging any that are skipped
            var validResults = new List<TorrentResult>();
            foreach (var r in results)
            {
                var infoHash = ExtractInfoHash(r);
                var magnetLink = BuildMagnetLink(r);

                if (string.IsNullOrWhiteSpace(infoHash) && string.IsNullOrWhiteSpace(r.DownloadUrl))
                {
                    _logger?.LogDebug(
                        "Skipping Prowlarr result '{Title}' - no valid InfoHash, MagnetLink, or DownloadUrl (InfoHash={InfoHash}, MagnetUrl={MagnetUrl}, DownloadUrl={DownloadUrl})",
                        r.Title, r.InfoHash, r.MagnetUrl, r.DownloadUrl);
                    continue;
                }

                validResults.Add(new TorrentResult
                {
                    InfoHash = infoHash,
                    Title = r.Title ?? "Unknown",
                    Size = r.Size,
                    Seeders = r.Seeders,
                    Leechers = r.Leechers,
                    MagnetLink = !string.IsNullOrEmpty(magnetLink) ? magnetLink : r.DownloadUrl, // Fallback to DownloadUrl as MagnetLink for Id purposes
                    IndexerName = r.Indexer ?? "Prowlarr"
                });
            }

            _logger?.LogInformation("Prowlarr: {ValidCount}/{TotalCount} results have valid magnet links for query '{Query}'",
                validResults.Count, results.Count, query);

            return validResults;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search Prowlarr for query '{Query}'", query);
            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Extracts info hash from Prowlarr result (from InfoHash field or magnet link).
    /// Returns empty string if no valid 40-character hex info hash can be extracted.
    /// </summary>
    private static string ExtractInfoHash(ProwlarrSearchResult result)
    {
        // Try InfoHash field first - validate it's a proper 40-char hex string
        if (!string.IsNullOrWhiteSpace(result.InfoHash))
        {
            var hash = result.InfoHash.Trim().ToUpperInvariant();
            if (IsValidInfoHash(hash))
                return hash;
        }

        // Try extracting from MagnetUrl (only if it's actually a magnet link)
        if (!string.IsNullOrWhiteSpace(result.MagnetUrl) &&
            result.MagnetUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            // Extract from magnet:?xt=urn:btih:HASH
            var match = System.Text.RegularExpressions.Regex.Match(
                result.MagnetUrl,
                @"btih:([a-fA-F0-9]{40})",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value.ToUpperInvariant();
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds a magnet link from Prowlarr result.
    /// Only returns valid magnet links - never download URLs.
    /// </summary>
    private static string BuildMagnetLink(ProwlarrSearchResult result)
    {
        // Use existing magnet URL if available AND it's actually a magnet link
        // (Some indexers put download URLs in the MagnetUrl field)
        if (!string.IsNullOrWhiteSpace(result.MagnetUrl) &&
            result.MagnetUrl.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            return result.MagnetUrl;
        }

        // Build from info hash and title
        if (!string.IsNullOrWhiteSpace(result.InfoHash) && IsValidInfoHash(result.InfoHash))
        {
            var hash = result.InfoHash.ToUpperInvariant();
            var title = Uri.EscapeDataString(result.Title ?? "Unknown");
            return $"magnet:?xt=urn:btih:{hash}&dn={title}";
        }

        return string.Empty;
    }

    /// <summary>
    /// Validates that an info hash is a proper 40-character hex string.
    /// </summary>
    private static bool IsValidInfoHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 40)
            return false;

        return hash.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
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

        // Check if Prowlarr is enabled - if so, use it as the primary source
        var (prowlarrEnabled, _, _) = GetProwlarrConfig();
        if (prowlarrEnabled)
        {
            _logger?.LogDebug("Using Prowlarr as primary indexer source");
            tasks.Add(SearchProwlarrAsync(query, cancellationToken));
        }
        else
        {
            // Query all enabled indexers in parallel (fallback mode)
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
        }

        if (tasks.Count == 0)
        {
            _logger?.LogWarning("No indexers available for search. Configure Prowlarr or add indexers.");
            return Array.Empty<TorrentResult>();
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

        _logger?.LogInformation("Merged {Count} unique results from {SourceCount} sources for query '{Query}'",
            merged.Count, tasks.Count, query);

        // Enrich results with TMDB metadata (Phase 4)
        try
        {
            var enriched = await _tmdbClient.EnrichResultsAsync(merged, cancellationToken);
            _logger?.LogDebug("Enriched {Count} results with TMDB metadata", enriched.Count);
            return enriched;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enrich results with TMDB metadata, returning raw results");
            return merged;
        }
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

/// <summary>
/// Represents a Prowlarr indexer from the API response.
/// </summary>
internal class ProwlarrIndexer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enable { get; set; }
    public bool SupportsSearch { get; set; }
    public string Protocol { get; set; } = string.Empty;
}

/// <summary>
/// Represents a search result from Prowlarr's search API.
/// </summary>
internal class ProwlarrSearchResult
{
    public string? Title { get; set; }
    public string? InfoHash { get; set; }
    public string? MagnetUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public long Size { get; set; }
    public int Seeders { get; set; }
    public int Leechers { get; set; }
    public string? Indexer { get; set; }
}

