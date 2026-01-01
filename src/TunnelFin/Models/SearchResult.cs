namespace TunnelFin.Models;

/// <summary>
/// Represents a torrent search result from indexers.
/// Contains metadata about a discovered torrent including quality, size, and seeders.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Unique identifier for this search result.
    /// </summary>
    public Guid ResultId { get; set; }

    /// <summary>
    /// The torrent title as provided by the indexer.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The torrent info hash (40-character hex string for SHA-1).
    /// </summary>
    public string InfoHash { get; set; } = string.Empty;

    /// <summary>
    /// The magnet URI for this torrent.
    /// </summary>
    public string MagnetUri { get; set; } = string.Empty;

    /// <summary>
    /// Total size of the torrent in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Number of seeders (peers with complete file).
    /// </summary>
    public int Seeders { get; set; }

    /// <summary>
    /// Number of leechers (peers downloading).
    /// </summary>
    public int Leechers { get; set; }

    /// <summary>
    /// The indexer that provided this result.
    /// </summary>
    public string IndexerName { get; set; } = string.Empty;

    /// <summary>
    /// Type of indexer (BuiltIn or Torznab).
    /// </summary>
    public IndexerType IndexerType { get; set; }

    /// <summary>
    /// Content type (Movie, TVShow, Anime).
    /// </summary>
    public ContentType ContentType { get; set; }

    /// <summary>
    /// Detected quality (e.g., "1080p", "720p", "4K").
    /// </summary>
    public string? Quality { get; set; }

    /// <summary>
    /// Detected codec (e.g., "x264", "x265", "HEVC").
    /// </summary>
    public string? Codec { get; set; }

    /// <summary>
    /// Detected audio format (e.g., "AAC", "DTS", "Atmos").
    /// </summary>
    public string? Audio { get; set; }

    /// <summary>
    /// Detected language (e.g., "English", "Japanese").
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Release group name (e.g., "RARBG", "YTS").
    /// </summary>
    public string? ReleaseGroup { get; set; }

    /// <summary>
    /// Timestamp when this result was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; set; }

    /// <summary>
    /// Upload date from the indexer (if available).
    /// </summary>
    public DateTime? UploadedAt { get; set; }

    /// <summary>
    /// TMDB ID for movies/TV shows (if matched).
    /// </summary>
    public int? TmdbId { get; set; }

    /// <summary>
    /// AniList ID for anime (if matched).
    /// </summary>
    public int? AniListId { get; set; }

    /// <summary>
    /// Relevance score (0-100) based on search query matching.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Whether this result passes the configured filters.
    /// </summary>
    public bool PassesFilters { get; set; }

    /// <summary>
    /// List of filter attributes that matched (for debugging).
    /// </summary>
    public List<string> MatchedFilters { get; set; } = new();
}

