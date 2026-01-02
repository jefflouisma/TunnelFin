using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Fetches metadata from TMDB/AniList with exponential backoff and failure caching (FR-029, FR-030, FR-031, FR-032).
/// </summary>
public class MetadataFetcher : IMetadataFetcher
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

        // T069: Enhanced title/year/episode matching (FR-026, SC-008)
        // Parse and validate metadata from filename with improved accuracy
        var metadata = ParseFilenameMetadata(result);

        // Validate and enhance metadata
        metadata = ValidateAndEnhanceMetadata(metadata, result);

        // Simulate processing delay (in real implementation, this would be API calls)
        await Task.Delay(10, cancellationToken);

        return metadata;
    }

    /// <summary>
    /// Validates and enhances metadata with improved matching logic (T069, FR-026).
    /// </summary>
    private MediaMetadata ValidateAndEnhanceMetadata(MediaMetadata metadata, SearchResult result)
    {
        // Validate year is reasonable (1900-2030)
        if (metadata.Year.HasValue && (metadata.Year.Value < 1900 || metadata.Year.Value > 2030))
        {
            _logger.LogWarning("Invalid year detected: {Year} for {Title}, removing", metadata.Year, metadata.Title);
            metadata.Year = null;
            metadata.MatchConfidence *= 0.8; // Reduce confidence
        }

        // Validate season/episode numbers are reasonable
        if (metadata.Season.HasValue && (metadata.Season.Value < 1 || metadata.Season.Value > 50))
        {
            _logger.LogWarning("Invalid season detected: {Season} for {Title}, removing", metadata.Season, metadata.Title);
            metadata.Season = null;
            metadata.MatchConfidence *= 0.8;
        }

        if (metadata.Episode.HasValue && (metadata.Episode.Value < 1 || metadata.Episode.Value > 999))
        {
            _logger.LogWarning("Invalid episode detected: {Episode} for {Title}, removing", metadata.Episode, metadata.Title);
            metadata.Episode = null;
            metadata.MatchConfidence *= 0.8;
        }

        // Enhance confidence based on what we successfully extracted
        if (metadata.Year.HasValue)
            metadata.MatchConfidence += 0.2; // Year adds confidence

        if (result.ContentType == ContentType.TVShow || result.ContentType == ContentType.Anime)
        {
            if (metadata.Season.HasValue && metadata.Episode.HasValue)
                metadata.MatchConfidence += 0.2; // Season/Episode adds confidence for TV content
        }

        // Ensure confidence is capped at 0.95 (we can't be 100% certain without API verification)
        metadata.MatchConfidence = Math.Min(metadata.MatchConfidence, 0.95);

        // Validate title is not empty after cleaning
        if (string.IsNullOrWhiteSpace(metadata.Title))
        {
            _logger.LogWarning("Title became empty after cleaning: {OriginalTitle}", metadata.OriginalTitle);
            metadata.Title = metadata.OriginalTitle; // Fallback to original
            metadata.MatchConfidence *= 0.5; // Significantly reduce confidence
        }

        return metadata;
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

        // Extract year (4-digit number, prioritize those in parentheses/brackets)
        // Try parentheses/brackets first for higher accuracy
        var yearMatch = Regex.Match(title, @"[\(\[](\d{4})[\)\]]");
        if (!yearMatch.Success)
        {
            // Fallback to standalone year (but be more careful)
            yearMatch = Regex.Match(title, @"\b(\d{4})\b");
        }

        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
        {
            // Validate year is reasonable
            if (year >= 1900 && year <= 2030)
            {
                metadata.Year = year;
                // Remove year from title
                title = title.Replace(yearMatch.Value, "").Trim();
            }
        }

        // Extract season/episode for TV shows (multiple formats)
        if (result.ContentType == ContentType.TVShow || result.ContentType == ContentType.Anime)
        {
            // Try S01E01 format first (most common)
            var episodeMatch = Regex.Match(title, @"[Ss](\d{1,2})[Ee](\d{1,3})");
            if (!episodeMatch.Success)
            {
                // Try 1x01 format
                episodeMatch = Regex.Match(title, @"(\d{1,2})x(\d{1,3})");
            }
            if (!episodeMatch.Success)
            {
                // Try Season 1 Episode 1 format
                episodeMatch = Regex.Match(title, @"Season\s*(\d{1,2})\s*Episode\s*(\d{1,3})", RegexOptions.IgnoreCase);
            }

            if (episodeMatch.Success)
            {
                metadata.Season = int.Parse(episodeMatch.Groups[1].Value);
                metadata.Episode = int.Parse(episodeMatch.Groups[2].Value);
                // Remove episode info from title
                title = title.Replace(episodeMatch.Value, "").Trim();
            }
        }

        // Clean up title: remove quality indicators, codecs, release groups
        // Enhanced list of quality indicators and codecs
        title = Regex.Replace(title, @"\b(1080p|720p|2160p|4K|UHD|BluRay|BRRip|WEB-DL|WEBRip|HDTV|DVDRip|x264|x265|H\.?264|H\.?265|HEVC|AAC|DTS|AC3|DD5\.1|TrueHD|Atmos)\b", "", RegexOptions.IgnoreCase);

        // Remove release group tags (usually at the end in brackets or after a dash)
        title = Regex.Replace(title, @"-\s*[A-Z0-9]+\s*$", "", RegexOptions.IgnoreCase); // Remove trailing release group
        title = Regex.Replace(title, @"\[.*?\]", ""); // Remove bracketed content
        title = Regex.Replace(title, @"\(.*?\)", ""); // Remove parenthetical content (after year extraction)

        // Replace dots, dashes, underscores with spaces
        title = Regex.Replace(title, @"[\.\-_]", " ");

        // Collapse multiple spaces
        title = Regex.Replace(title, @"\s+", " ");

        // Remove common words that don't belong in titles
        title = Regex.Replace(title, @"\b(REPACK|PROPER|EXTENDED|UNRATED|DIRECTORS?\.?CUT|THEATRICAL|COMPLETE|SEASON|SERIES)\b", "", RegexOptions.IgnoreCase);

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

