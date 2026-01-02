using Microsoft.Extensions.Logging;
using TunnelFin.Discovery;
using TunnelFin.Models;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Integrates TunnelFin metadata with Jellyfin's metadata system (FR-029).
/// Placeholder implementation - requires Jellyfin IMetadataProvider interface integration.
/// </summary>
public class MetadataProvider
{
    private readonly ILogger _logger;
    private readonly IMetadataFetcher _metadataFetcher;

    /// <summary>
    /// Initializes a new instance of the MetadataProvider class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="metadataFetcher">Metadata fetcher for TMDB/AniList integration.</param>
    public MetadataProvider(ILogger logger, IMetadataFetcher metadataFetcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metadataFetcher = metadataFetcher ?? throw new ArgumentNullException(nameof(metadataFetcher));
    }

    /// <summary>
    /// Gets metadata for a torrent item (FR-029).
    /// </summary>
    /// <param name="infoHash">Torrent info hash.</param>
    /// <param name="title">Torrent title.</param>
    /// <param name="contentType">Content type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Media metadata from TMDB/AniList.</returns>
    public async Task<MediaMetadata?> GetMetadataAsync(
        string infoHash,
        string title,
        ContentType contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(infoHash))
            throw new ArgumentException("Info hash cannot be empty", nameof(infoHash));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        _logger.LogDebug("Getting metadata for: {Title} ({InfoHash})", title, infoHash);

        try
        {
            // Create a SearchResult to fetch metadata
            var searchResult = new SearchResult
            {
                InfoHash = infoHash,
                Title = title,
                ContentType = contentType
            };

            // Fetch metadata from TMDB/AniList
            var metadata = await _metadataFetcher.FetchMetadataAsync(searchResult, cancellationToken);

            _logger.LogDebug("Metadata fetched: {Title} (Source: {Source}, Confidence: {Confidence})",
                metadata.Title, metadata.Source, metadata.MatchConfidence);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metadata for: {Title}", title);
            return null;
        }
    }

    /// <summary>
    /// Converts TunnelFin metadata to Jellyfin metadata format.
    /// </summary>
    /// <param name="metadata">TunnelFin metadata.</param>
    /// <returns>Jellyfin-compatible metadata object.</returns>
    public JellyfinMetadata ConvertToJellyfinMetadata(MediaMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        // TODO: T072 - Implement actual Jellyfin metadata conversion
        // 1. Map MediaMetadata fields to Jellyfin MetadataResult
        // 2. Convert poster/backdrop URLs to Jellyfin image format
        // 3. Map genres, cast, directors to Jellyfin format
        // 4. Set provider IDs (TMDB, AniList, IMDB)

        return new JellyfinMetadata
        {
            Title = metadata.Title,
            OriginalTitle = metadata.OriginalTitle,
            Year = metadata.Year,
            Overview = metadata.Overview,
            PosterUrl = metadata.PosterUrl,
            BackdropUrl = metadata.BackdropUrl,
            TmdbId = metadata.TmdbId,
            AniListId = metadata.AniListId,
            ImdbId = metadata.ImdbId,
            ContentRating = metadata.ContentRating,
            Rating = metadata.Rating,
            Genres = metadata.Genres,
            Cast = metadata.Cast,
            Directors = metadata.Directors,
            RuntimeMinutes = metadata.RuntimeMinutes
        };
    }

    /// <summary>
    /// Checks if metadata is available for a given item.
    /// </summary>
    /// <param name="title">Item title.</param>
    /// <param name="contentType">Content type.</param>
    /// <returns>True if metadata is likely available.</returns>
    public bool HasMetadata(string title, ContentType contentType)
    {
        // Simple heuristic: if title contains year (19xx or 20xx) or episode info, metadata is likely available
        return !string.IsNullOrWhiteSpace(title) &&
               (title.Contains("(19") || title.Contains("(20") ||
                title.Contains("[19") || title.Contains("[20") ||
                title.Contains("S0"));
    }
}

/// <summary>
/// Represents Jellyfin-compatible metadata.
/// Placeholder - will be replaced with actual Jellyfin MetadataResult.
/// </summary>
public class JellyfinMetadata
{
    public string Title { get; set; } = string.Empty;
    public string? OriginalTitle { get; set; }
    public int? Year { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public int? TmdbId { get; set; }
    public int? AniListId { get; set; }
    public string? ImdbId { get; set; }
    public string? ContentRating { get; set; }
    public double? Rating { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Cast { get; set; } = new();
    public List<string> Directors { get; set; } = new();
    public int? RuntimeMinutes { get; set; }
}

