using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Fetches metadata from TMDB/AniList with exponential backoff and failure caching (FR-029, FR-030, FR-031, FR-032).
/// </summary>
public class MetadataFetcher
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, DateTime> _failureCache = new();
    private readonly TimeSpan _failureCacheDuration = TimeSpan.FromMinutes(5); // FR-031

    public MetadataFetcher(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetches metadata for a search result (FR-029).
    /// </summary>
    /// <param name="result">Search result to fetch metadata for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media metadata with TMDB/AniList data or filename-parsed fallback.</returns>
    public async Task<MediaMetadata> FetchMetadataAsync(
        SearchResult result,
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        cancellationToken.ThrowIfCancellationRequested();

        // Check failure cache (FR-031)
        var cacheKey = $"{result.ContentType}:{result.Title}";
        if (_failureCache.TryGetValue(cacheKey, out var failureTime))
        {
            if (DateTime.UtcNow - failureTime < _failureCacheDuration)
            {
                _logger.LogDebug("Using cached failure for: {Title}", result.Title);
                return ParseFilenameMetadata(result);
            }
            else
            {
                // Cache expired, remove it
                _failureCache.TryRemove(cacheKey, out _);
            }
        }

        // TODO: Implement actual TMDB/AniList API integration with exponential backoff (FR-030)
        // 1. Parse title and year from filename
        // 2. Query TMDB/AniList API based on content type
        // 3. Retry with exponential backoff: 1s, 2s, 4s (FR-030)
        // 4. Match title/year/episode against API results (FR-026)
        // 5. Return rich metadata with 95% success rate (SC-008)

        // Placeholder: Use filename parsing fallback (FR-032)
        await Task.Delay(10, cancellationToken);
        return ParseFilenameMetadata(result);
    }

    /// <summary>
    /// Parses basic metadata from filename as fallback (FR-032).
    /// </summary>
    private MediaMetadata ParseFilenameMetadata(SearchResult result)
    {
        var metadata = new MediaMetadata
        {
            Id = Guid.NewGuid(),
            Source = MetadataSource.Filename,
            MatchConfidence = 0.5, // Lower confidence for filename parsing
            FetchedAt = DateTime.UtcNow
        };

        var title = result.Title;

        // Extract year (4-digit number in parentheses or standalone)
        var yearMatch = Regex.Match(title, @"[\(\[]?(\d{4})[\)\]]?");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
        {
            metadata.Year = year;
            // Remove year from title
            title = title.Replace(yearMatch.Value, "").Trim();
        }

        // Extract season/episode for TV shows (S01E01 format)
        if (result.ContentType == ContentType.TVShow || result.ContentType == ContentType.Anime)
        {
            var episodeMatch = Regex.Match(title, @"[Ss](\d+)[Ee](\d+)");
            if (episodeMatch.Success)
            {
                metadata.Season = int.Parse(episodeMatch.Groups[1].Value);
                metadata.Episode = int.Parse(episodeMatch.Groups[2].Value);
                // Remove episode info from title
                title = title.Replace(episodeMatch.Value, "").Trim();
            }
        }

        // Clean up title: remove quality indicators, codecs, release groups
        title = Regex.Replace(title, @"\b(1080p|720p|2160p|4K|BluRay|WEB-DL|HDTV|x264|x265|HEVC|AAC|DTS)\b", "", RegexOptions.IgnoreCase);
        title = Regex.Replace(title, @"[\.\-_]", " "); // Replace dots, dashes, underscores with spaces
        title = Regex.Replace(title, @"\s+", " "); // Collapse multiple spaces
        title = Regex.Replace(title, @"\[.*?\]", ""); // Remove bracketed content
        title = title.Trim();

        metadata.Title = title;
        metadata.OriginalTitle = result.Title;

        _logger.LogDebug("Parsed filename metadata: Title={Title}, Year={Year}, Season={Season}, Episode={Episode}",
            metadata.Title, metadata.Year, metadata.Season, metadata.Episode);

        return metadata;
    }

    /// <summary>
    /// Clears the failure cache.
    /// </summary>
    public void ClearCache()
    {
        _failureCache.Clear();
        _logger.LogInformation("Metadata failure cache cleared");
    }

    /// <summary>
    /// Gets the number of cached failures.
    /// </summary>
    public int GetCachedFailureCount() => _failureCache.Count;
}

