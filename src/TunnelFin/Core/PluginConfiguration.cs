using MediaBrowser.Model.Plugins;

namespace TunnelFin.Core;

/// <summary>
/// Configuration for TunnelFin plugin (T010)
/// Inherits from BasePluginConfiguration for Jellyfin integration
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Maximum number of concurrent torrent streams (FR-013)
    /// Default: 3 streams
    /// </summary>
    public int MaxConcurrentStreams { get; set; } = 3;

    /// <summary>
    /// Maximum cache size in bytes (FR-014)
    /// Default: 10GB (10737418240 bytes)
    /// </summary>
    public long MaxCacheSize { get; set; } = 10737418240L; // 10GB

    /// <summary>
    /// Maximum concurrent indexer searches (FR-018)
    /// Default: 5 concurrent searches
    /// </summary>
    public int MaxConcurrentSearches { get; set; } = 5;

    /// <summary>
    /// Default hop count for anonymity circuits (FR-003, FR-006)
    /// Range: 1-3 hops
    /// Default: 3 hops for maximum privacy
    /// </summary>
    public int DefaultHopCount { get; set; } = 3;

    /// <summary>
    /// Minimum hop count allowed (FR-003)
    /// </summary>
    public int MinHopCount { get; set; } = 1;

    /// <summary>
    /// Maximum hop count allowed (FR-003)
    /// </summary>
    public int MaxHopCount { get; set; } = 3;

    /// <summary>
    /// Enable bandwidth contribution/relay (FR-005)
    /// Default: true (proportional contribution)
    /// </summary>
    public bool EnableBandwidthContribution { get; set; } = true;

    /// <summary>
    /// Allow non-anonymous fallback when Tribler network unavailable (FR-036, FR-040)
    /// Default: false (require explicit consent per session)
    /// </summary>
    public bool AllowNonAnonymousFallback { get; set; } = false;

    /// <summary>
    /// Stream initialization timeout in seconds (FR-012)
    /// Default: 60 seconds
    /// </summary>
    public int StreamInitializationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Circuit establishment timeout in seconds (FR-040)
    /// Default: 30 seconds
    /// </summary>
    public int CircuitEstablishmentTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum buffer duration in seconds (FR-010)
    /// Default: 10 seconds
    /// </summary>
    public int MinimumBufferSeconds { get; set; } = 10;

    /// <summary>
    /// Search result cache duration in minutes (FR-026)
    /// Default: 10 minutes (range: 5-15 minutes)
    /// </summary>
    public int SearchCacheDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Metadata failure cache duration in minutes (FR-031)
    /// Default: 5 minutes
    /// </summary>
    public int MetadataFailureCacheDurationMinutes { get; set; } = 5;

    /// <summary>
    /// Logging level: Minimal or Verbose (FR-037)
    /// Default: Minimal (privacy-aware, no PII or content titles)
    /// </summary>
    public LoggingLevel LoggingLevel { get; set; } = LoggingLevel.Minimal;

    /// <summary>
    /// Enable scheduled catalog sync (FR-033)
    /// Default: false
    /// </summary>
    public bool EnableScheduledCatalogSync { get; set; } = false;

    /// <summary>
    /// Catalog sync interval in hours (FR-033)
    /// Default: 24 hours
    /// </summary>
    public int CatalogSyncIntervalHours { get; set; } = 24;

    /// <summary>
    /// Built-in indexers configuration (FR-016)
    /// </summary>
    public List<IndexerConfig> BuiltInIndexers { get; set; } = new()
    {
        new IndexerConfig { Name = "1337x", Enabled = true, Url = "https://1337x.to" },
        new IndexerConfig { Name = "Nyaa", Enabled = true, Url = "https://nyaa.si" },
        new IndexerConfig { Name = "RARBG", Enabled = true, Url = "https://rarbg.to" }
    };

    /// <summary>
    /// Custom Torznab indexers (FR-017)
    /// </summary>
    public List<TorznabIndexerConfig> CustomIndexers { get; set; } = new();

    /// <summary>
    /// Prowlarr URL for indexer aggregation (e.g., "http://192.168.1.10:9696")
    /// </summary>
    public string ProwlarrUrl { get; set; } = string.Empty;

    /// <summary>
    /// Prowlarr API key for authentication
    /// </summary>
    public string ProwlarrApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Whether Prowlarr integration is enabled
    /// </summary>
    public bool ProwlarrEnabled { get; set; } = false;

    /// <summary>
    /// TMDB API key for metadata fetching (FR-029)
    /// </summary>
    public string? TmdbApiKey { get; set; }

    /// <summary>
    /// AniList client ID for anime metadata (FR-029)
    /// </summary>
    public string? AniListClientId { get; set; }

    /// <summary>
    /// Validate configuration values
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (MaxConcurrentStreams < 1 || MaxConcurrentStreams > 10)
            errors.Add("MaxConcurrentStreams must be between 1 and 10");

        if (MaxCacheSize < 1073741824L) // 1GB minimum
            errors.Add("MaxCacheSize must be at least 1GB");

        if (DefaultHopCount < MinHopCount || DefaultHopCount > MaxHopCount)
            errors.Add($"DefaultHopCount must be between {MinHopCount} and {MaxHopCount}");

        if (StreamInitializationTimeoutSeconds < 10 || StreamInitializationTimeoutSeconds > 300)
            errors.Add("StreamInitializationTimeoutSeconds must be between 10 and 300");

        return errors.Count == 0;
    }
}

/// <summary>
/// Logging level enumeration (FR-037)
/// </summary>
public enum LoggingLevel
{
    Minimal,  // Privacy-aware, no PII or content titles
    Verbose   // Detailed logging for debugging
}

/// <summary>
/// Built-in indexer configuration
/// </summary>
public class IndexerConfig
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Custom Torznab indexer configuration (FR-017)
/// </summary>
public class TorznabIndexerConfig : IndexerConfig
{
    public string ApiKey { get; set; } = string.Empty;
}

