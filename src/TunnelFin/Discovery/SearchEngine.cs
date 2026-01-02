using Microsoft.Extensions.Logging;
using TunnelFin.Indexers;
using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Orchestrates the search workflow: query distribution, deduplication, metadata fetching (FR-025, FR-026, SC-004).
/// </summary>
public class SearchEngine
{
    private readonly ILogger _logger;
    private readonly IndexerManager _indexerManager;
    private readonly Deduplicator _deduplicator;
    private readonly MetadataFetcher _metadataFetcher;

    public SearchEngine(
        ILogger logger,
        IndexerManager indexerManager,
        Deduplicator deduplicator,
        MetadataFetcher metadataFetcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexerManager = indexerManager ?? throw new ArgumentNullException(nameof(indexerManager));
        _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        _metadataFetcher = metadataFetcher ?? throw new ArgumentNullException(nameof(metadataFetcher));
    }

    /// <summary>
    /// Searches for content across all indexers with deduplication and metadata fetching (SC-004).
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="contentType">Type of content to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search results with metadata, deduplicated.</returns>
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        ContentType contentType = ContentType.Movie,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        cancellationToken.ThrowIfCancellationRequested();

        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting search: Query={Query}, ContentType={ContentType}", query, contentType);

        try
        {
            // Step 1: Query all indexers (max 5 concurrent per FR-018)
            var searchResponse = await _indexerManager.SearchAsync(query, contentType, cancellationToken);
            _logger.LogDebug("Indexer search completed: {Count} results from {IndexerCount} indexers in {Duration}ms",
                searchResponse.Results.Count, searchResponse.IndexersQueried.Count, searchResponse.SearchDuration.TotalMilliseconds);

            if (searchResponse.Results.Count == 0)
            {
                _logger.LogInformation("No results found for query: {Query}", query);
                return new List<SearchResult>();
            }

            // Step 2: Deduplicate results (FR-025, SC-007)
            var deduplicated = _deduplicator.Deduplicate(searchResponse.Results);
            _logger.LogDebug("Deduplication completed: {Original} → {Deduplicated} results",
                searchResponse.Results.Count, deduplicated.Count);

            // Step 3: Fetch metadata for each result (FR-029, SC-008)
            // Note: This is a placeholder - actual implementation would fetch metadata in parallel
            // and attach it to the SearchResult objects
            foreach (var result in deduplicated)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // TODO: Fetch metadata and attach to result
                // var metadata = await _metadataFetcher.FetchMetadataAsync(result, cancellationToken);
                // result.TmdbId = metadata.TmdbId;
                // result.AniListId = metadata.AniListId;
            }

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Search completed: {Count} results in {Duration}ms", deduplicated.Count, duration.TotalMilliseconds);

            // Ensure we meet SC-004 (5-second timeout)
            if (duration.TotalSeconds > 5)
            {
                _logger.LogWarning("Search exceeded 5-second timeout: {Duration}s", duration.TotalSeconds);
            }

            return deduplicated;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Search cancelled: Query={Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed: Query={Query}", query);
            throw;
        }
    }
}

