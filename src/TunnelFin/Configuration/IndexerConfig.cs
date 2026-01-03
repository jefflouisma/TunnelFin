namespace TunnelFin.Configuration;

/// <summary>
/// Configuration for a Torznab or HTML-based indexer.
/// Lifecycle: Created by user in plugin settings, persists in Jellyfin configuration.
/// Storage: Jellyfin's PluginConfiguration (XML file in config directory).
/// </summary>
public class IndexerConfig
{
    /// <summary>
    /// Unique identifier for this indexer.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Display name (e.g., "Jackett - 1337x", "Nyaa Direct").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of indexer (Torznab or HTML scraper).
    /// </summary>
    public required TunnelFin.Models.IndexerType Type { get; set; }

    /// <summary>
    /// Base URL for indexer (e.g., "http://localhost:9117/api/v2.0/indexers/1337x").
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// API key for Torznab indexers (not used for HTML scrapers).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Maximum requests per second (default: 1.0).
    /// </summary>
    public double RateLimitPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Whether this indexer is active (default: true).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Search priority (lower = higher priority, default: 100).
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Torznab category IDs to search (e.g., [2000, 5000] for movies/TV).
    /// </summary>
    public List<int>? Categories { get; set; }

    /// <summary>
    /// Validates the indexer configuration according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Name must not be empty", nameof(Name));

        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new ArgumentException("BaseUrl must not be empty", nameof(BaseUrl));

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || 
            (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ArgumentException("BaseUrl must be valid HTTP/HTTPS URL", nameof(BaseUrl));

        if (RateLimitPerSecond < 0.1 || RateLimitPerSecond > 10.0)
            throw new ArgumentException("RateLimitPerSecond must be between 0.1 and 10.0", nameof(RateLimitPerSecond));

        if (Priority < 0)
            throw new ArgumentException("Priority must be non-negative", nameof(Priority));

        if (Type == TunnelFin.Models.IndexerType.Torznab && string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("ApiKey required if Type == Torznab", nameof(ApiKey));
    }
}

