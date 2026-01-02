namespace TunnelFin.Models;

/// <summary>
/// Represents metadata fetched from TMDB or AniList (FR-029).
/// Contains rich media information for display in Jellyfin.
/// </summary>
public class MediaMetadata
{
    /// <summary>
    /// Unique identifier for this metadata record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The matched title from TMDB/AniList.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Original title (if different from localized title).
    /// </summary>
    public string? OriginalTitle { get; set; }

    /// <summary>
    /// Release year for movies, first air date year for TV shows.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Plot summary/overview.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    /// Poster image URL.
    /// </summary>
    public string? PosterUrl { get; set; }

    /// <summary>
    /// Backdrop/fanart image URL.
    /// </summary>
    public string? BackdropUrl { get; set; }

    /// <summary>
    /// TMDB ID (for movies and TV shows).
    /// </summary>
    public int? TmdbId { get; set; }

    /// <summary>
    /// AniList ID (for anime).
    /// </summary>
    public int? AniListId { get; set; }

    /// <summary>
    /// IMDb ID (if available).
    /// </summary>
    public string? ImdbId { get; set; }

    /// <summary>
    /// Content rating (e.g., "PG-13", "TV-MA").
    /// </summary>
    public string? ContentRating { get; set; }

    /// <summary>
    /// Average user rating (0-10 scale).
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Number of votes/ratings.
    /// </summary>
    public int? VoteCount { get; set; }

    /// <summary>
    /// Genres (e.g., ["Action", "Sci-Fi"]).
    /// </summary>
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Cast members (actor names).
    /// </summary>
    public List<string> Cast { get; set; } = new();

    /// <summary>
    /// Director(s) for movies.
    /// </summary>
    public List<string> Directors { get; set; } = new();

    /// <summary>
    /// Runtime in minutes.
    /// </summary>
    public int? RuntimeMinutes { get; set; }

    /// <summary>
    /// Season number (for TV shows).
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Episode number (for TV shows).
    /// </summary>
    public int? Episode { get; set; }

    /// <summary>
    /// Episode title (for TV shows).
    /// </summary>
    public string? EpisodeTitle { get; set; }

    /// <summary>
    /// Source of this metadata (TMDB, AniList, or Filename).
    /// </summary>
    public MetadataSource Source { get; set; }

    /// <summary>
    /// Confidence score (0.0-1.0) for the match.
    /// </summary>
    public double MatchConfidence { get; set; }

    /// <summary>
    /// Timestamp when this metadata was fetched.
    /// </summary>
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Source of metadata.
/// </summary>
public enum MetadataSource
{
    /// <summary>
    /// Metadata from The Movie Database (TMDB).
    /// </summary>
    TMDB,

    /// <summary>
    /// Metadata from AniList (anime database).
    /// </summary>
    AniList,

    /// <summary>
    /// Metadata parsed from filename (fallback per FR-032).
    /// </summary>
    Filename
}

