namespace TunnelFin.Models;

/// <summary>
/// Represents configuration for a torrent indexer (built-in or Torznab).
/// </summary>
public class IndexerConfiguration
{
    /// <summary>
    /// Unique identifier for this indexer configuration.
    /// </summary>
    public Guid IndexerId { get; set; }

    /// <summary>
    /// User-friendly name for this indexer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of indexer (BuiltIn or Torznab).
    /// </summary>
    public IndexerType Type { get; set; }

    /// <summary>
    /// Whether this indexer is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Priority order for this indexer (lower number = higher priority).
    /// Results from higher priority indexers are shown first.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Base URL for Torznab indexers (e.g., "https://indexer.example.com/api").
    /// Null for built-in indexers.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API key for Torznab indexers.
    /// Null for built-in indexers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Content types this indexer supports (Movie, TVShow, Anime).
    /// Empty list means supports all types.
    /// </summary>
    public List<ContentType> SupportedContentTypes { get; set; } = new();

    /// <summary>
    /// Maximum number of results to request from this indexer per search.
    /// </summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to verify SSL certificates for HTTPS connections.
    /// </summary>
    public bool VerifySsl { get; set; } = true;

    /// <summary>
    /// Custom HTTP headers to send with requests (for authentication, user-agent, etc.).
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Rate limit: minimum milliseconds between requests to this indexer.
    /// </summary>
    public int RateLimitMs { get; set; } = 1000;

    /// <summary>
    /// Timestamp of the last successful request to this indexer.
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Timestamp of the last failed request to this indexer.
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Error message from the last failed request.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Whether this indexer is currently healthy (not experiencing repeated failures).
    /// </summary>
    public bool IsHealthy => ConsecutiveFailures < 3;

    /// <summary>
    /// Timestamp when this indexer was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this indexer was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}

