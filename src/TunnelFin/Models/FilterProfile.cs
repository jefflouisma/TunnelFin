namespace TunnelFin.Models;

/// <summary>
/// Represents a user-defined filter profile for torrent search results.
/// Allows filtering by quality, size, seeders, codec, audio, language, etc.
/// </summary>
public class FilterProfile
{
    /// <summary>
    /// Unique identifier for this filter profile.
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// User-friendly name for this profile (e.g., "High Quality Movies", "Anime 1080p").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this profile filters for.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this profile is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Content types this profile applies to (Movie, TVShow, Anime).
    /// Empty list means applies to all types.
    /// </summary>
    public List<ContentType> ContentTypes { get; set; } = new();

    /// <summary>
    /// Minimum number of seeders required.
    /// </summary>
    public int? MinSeeders { get; set; }

    /// <summary>
    /// Maximum number of seeders (for filtering out fake/honeypot torrents).
    /// </summary>
    public int? MaxSeeders { get; set; }

    /// <summary>
    /// Minimum file size in bytes.
    /// </summary>
    public long? MinSize { get; set; }

    /// <summary>
    /// Maximum file size in bytes.
    /// </summary>
    public long? MaxSize { get; set; }

    /// <summary>
    /// Allowed quality values (e.g., ["1080p", "720p"]).
    /// Empty list means all qualities allowed.
    /// </summary>
    public List<string> AllowedQualities { get; set; } = new();

    /// <summary>
    /// Blocked quality values (e.g., ["CAM", "TS"]).
    /// </summary>
    public List<string> BlockedQualities { get; set; } = new();

    /// <summary>
    /// Allowed codecs (e.g., ["x265", "HEVC"]).
    /// Empty list means all codecs allowed.
    /// </summary>
    public List<string> AllowedCodecs { get; set; } = new();

    /// <summary>
    /// Blocked codecs.
    /// </summary>
    public List<string> BlockedCodecs { get; set; } = new();

    /// <summary>
    /// Allowed audio formats (e.g., ["AAC", "DTS"]).
    /// Empty list means all audio formats allowed.
    /// </summary>
    public List<string> AllowedAudioFormats { get; set; } = new();

    /// <summary>
    /// Blocked audio formats.
    /// </summary>
    public List<string> BlockedAudioFormats { get; set; } = new();

    /// <summary>
    /// Allowed languages (e.g., ["English", "Japanese"]).
    /// Empty list means all languages allowed.
    /// </summary>
    public List<string> AllowedLanguages { get; set; } = new();

    /// <summary>
    /// Blocked languages.
    /// </summary>
    public List<string> BlockedLanguages { get; set; } = new();

    /// <summary>
    /// Allowed release groups (e.g., ["RARBG", "YTS"]).
    /// Empty list means all groups allowed.
    /// </summary>
    public List<string> AllowedReleaseGroups { get; set; } = new();

    /// <summary>
    /// Blocked release groups.
    /// </summary>
    public List<string> BlockedReleaseGroups { get; set; } = new();

    /// <summary>
    /// Keywords that must be present in the title.
    /// </summary>
    public List<string> RequiredKeywords { get; set; } = new();

    /// <summary>
    /// Keywords that must NOT be present in the title.
    /// </summary>
    public List<string> ExcludedKeywords { get; set; } = new();

    /// <summary>
    /// Priority order for this profile (lower number = higher priority).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Timestamp when this profile was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this profile was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}

