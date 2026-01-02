using Microsoft.Extensions.Logging;
using TunnelFin.Discovery;
using TunnelFin.Models;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Provides torrent search functionality for Jellyfin integration (FR-027).
/// Enhanced with full metadata support (T070).
/// </summary>
public class TunnelFinSearchProvider
{
    private readonly ILogger _logger;
    private readonly SearchEngine? _searchEngine;
    private bool _isNetworkAvailable;

    /// <summary>
    /// Indicates whether the Tribler network is currently available (FR-039).
    /// </summary>
    public bool IsNetworkAvailable => _isNetworkAvailable;

    /// <summary>
    /// Initializes a new instance of the TunnelFinSearchProvider class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="searchEngine">Optional search engine for full metadata support.</param>
    public TunnelFinSearchProvider(ILogger logger, SearchEngine? searchEngine = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _searchEngine = searchEngine;
        _isNetworkAvailable = false;
    }

    /// <summary>
    /// Searches for torrent content across configured indexers.
    /// Returns results within 5 seconds (SC-004).
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="contentType">Content type for profile selection.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with metadata.</returns>
    public async Task<SearchResponse> SearchAsync(
        string query,
        ContentType contentType = ContentType.Movie,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (limit < 1 || limit > 100)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100");

        var startTime = DateTime.UtcNow;

        try
        {
            List<SearchResult> results;

            if (_searchEngine != null)
            {
                // T070: Full search implementation with metadata
                // 1. Query all enabled indexers via SearchEngine
                // 2. Deduplicate results
                // 3. Fetch metadata from TMDB/AniList
                // 4. Apply filters and sorting
                results = await _searchEngine.SearchAsync(query, contentType, cancellationToken);

                // Apply limit
                if (results.Count > limit)
                {
                    results = results.Take(limit).ToList();
                }
            }
            else
            {
                // Fallback: Placeholder implementation without SearchEngine
                results = new List<SearchResult>();
                await Task.Delay(10, cancellationToken);
            }

            var searchDuration = DateTime.UtcNow - startTime;

            return new SearchResponse
            {
                Results = results,
                TotalResults = results.Count,
                SearchDuration = searchDuration,
                IndexersQueried = new List<string>() // TODO: Get from SearchEngine
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Search cancelled for query: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Checks if the Tribler network is available (FR-039).
    /// Updates the IsNetworkAvailable property.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if network is available.</returns>
    public async Task<bool> CheckNetworkAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: T048 - Implement actual network availability check
            // 1. Check if we have any active circuits
            // 2. Verify we can reach Tribler network peers
            // 3. Test basic connectivity

            await Task.Delay(10, cancellationToken);

            // Placeholder - assume network is available
            _isNetworkAvailable = true;

            return _isNetworkAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Network availability check failed");
            _isNetworkAvailable = false;
            return false;
        }
    }

    /// <summary>
    /// Gets the play button color for a search result based on anonymity availability (FR-034).
    /// </summary>
    /// <param name="result">Search result to check.</param>
    /// <returns>Play button color (Green for anonymous, Orange for non-anonymous).</returns>
    public PlayButtonColor GetPlayButtonColor(SearchResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        // Green = anonymous (available on Tribler network)
        // Orange = non-anonymous (only available on standard BitTorrent)

        if (_isNetworkAvailable && result.Seeders > 0)
        {
            // TODO: T047 - Implement actual availability check
            // Check if this torrent is available on Tribler network
            // For now, assume all torrents with seeders are available anonymously
            return PlayButtonColor.Green;
        }

        return PlayButtonColor.Orange;
    }
}

/// <summary>
/// Represents a search response with results and metadata.
/// </summary>
public class SearchResponse
{
    public List<SearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public TimeSpan SearchDuration { get; set; }
    public List<string> IndexersQueried { get; set; } = new();
}

/// <summary>
/// Play button color indicating anonymity status (FR-034).
/// </summary>
public enum PlayButtonColor
{
    /// <summary>
    /// Green - Stream will be routed through anonymous Tribler network.
    /// </summary>
    Green,

    /// <summary>
    /// Orange - Stream is only available on standard BitTorrent (exposes IP).
    /// </summary>
    Orange
}

