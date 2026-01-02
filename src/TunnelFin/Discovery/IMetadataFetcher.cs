using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Interface for metadata fetching from TMDB/AniList.
/// </summary>
public interface IMetadataFetcher
{
    /// <summary>
    /// Fetches metadata for a search result.
    /// </summary>
    /// <param name="result">Search result to fetch metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media metadata with TMDB/AniList data or filename-parsed fallback.</returns>
    Task<MediaMetadata> FetchMetadataAsync(SearchResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the failure cache.
    /// </summary>
    void ClearCache();
}

