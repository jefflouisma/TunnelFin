using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TunnelFin.Jellyfin;
using TunnelFin.Models;

namespace TunnelFin.Indexers;

/// <summary>
/// Manages multiple torrent indexers and orchestrates concurrent searches (FR-016, FR-018).
/// Aggregates results from all enabled indexers with max 5 concurrent searches.
/// </summary>
public class IndexerManager
{
    private readonly ILogger _logger;
    private readonly List<IIndexer> _indexers = new();
    private readonly int _maxConcurrentIndexers;
    private readonly SemaphoreSlim _semaphore;

    /// <summary>
    /// Initializes a new instance of the IndexerManager class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="maxConcurrentIndexers">Maximum concurrent indexer searches (default: 5 per FR-018).</param>
    public IndexerManager(ILogger logger, int maxConcurrentIndexers = 5)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (maxConcurrentIndexers < 1)
            throw new ArgumentException("Max concurrent indexers must be at least 1", nameof(maxConcurrentIndexers));

        _maxConcurrentIndexers = maxConcurrentIndexers;
        _semaphore = new SemaphoreSlim(_maxConcurrentIndexers, _maxConcurrentIndexers);
    }

    /// <summary>
    /// Adds an indexer to the manager.
    /// </summary>
    /// <param name="indexer">Indexer to add.</param>
    public void AddIndexer(IIndexer indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        if (!_indexers.Any(i => i.Name == indexer.Name))
        {
            _indexers.Add(indexer);
            _logger.LogInformation("Added indexer: {IndexerName}", indexer.Name);
        }
    }

    /// <summary>
    /// Removes an indexer from the manager.
    /// </summary>
    /// <param name="indexerName">Name of the indexer to remove.</param>
    public void RemoveIndexer(string indexerName)
    {
        var indexer = _indexers.FirstOrDefault(i => i.Name == indexerName);
        if (indexer != null)
        {
            _indexers.Remove(indexer);
            _logger.LogInformation("Removed indexer: {IndexerName}", indexerName);
        }
    }

    /// <summary>
    /// Searches all enabled indexers concurrently (FR-018).
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="contentType">Content type for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated search results from all indexers.</returns>
    public async Task<SearchResponse> SearchAsync(
        string query,
        ContentType contentType = ContentType.Movie,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var startTime = DateTime.UtcNow;
        var allResults = new ConcurrentBag<SearchResult>();
        var queriedIndexers = new ConcurrentBag<string>();

        var enabledIndexers = _indexers.Where(i => i.IsEnabled).ToList();

        _logger.LogInformation("Searching {Count} enabled indexers for query: {Query}", 
            enabledIndexers.Count, query);

        // Search all enabled indexers concurrently with semaphore limit (FR-018)
        var tasks = enabledIndexers.Select(async indexer =>
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var results = await indexer.SearchAsync(query, contentType, cancellationToken);
                
                foreach (var result in results)
                {
                    result.IndexerName = indexer.Name;
                    allResults.Add(result);
                }

                queriedIndexers.Add(indexer.Name);
                
                _logger.LogDebug("Indexer {IndexerName} returned {Count} results", 
                    indexer.Name, results.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Search cancelled for indexer: {IndexerName}", indexer.Name);
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Timeout searching indexer: {IndexerName}", indexer.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching indexer: {IndexerName}", indexer.Name);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var searchDuration = DateTime.UtcNow - startTime;

        _logger.LogInformation("Search completed in {Duration}ms with {Count} total results from {Indexers} indexers",
            searchDuration.TotalMilliseconds, allResults.Count, queriedIndexers.Count);

        return new SearchResponse
        {
            Results = allResults.ToList(),
            TotalResults = allResults.Count,
            SearchDuration = searchDuration,
            IndexersQueried = queriedIndexers.ToList()
        };
    }

    /// <summary>
    /// Gets all registered indexers.
    /// </summary>
    public IReadOnlyList<IIndexer> GetIndexers() => _indexers.AsReadOnly();

    /// <summary>
    /// Gets the count of enabled indexers.
    /// </summary>
    public int GetEnabledIndexerCount() => _indexers.Count(i => i.IsEnabled);
}

