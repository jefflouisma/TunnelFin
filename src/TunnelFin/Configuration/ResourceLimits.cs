namespace TunnelFin.Configuration;

/// <summary>
/// Configuration settings for resource limits and quotas.
/// Controls maximum concurrent streams, cache size, and search result limits.
/// </summary>
public class ResourceLimits
{
    /// <summary>
    /// Maximum number of concurrent torrent streams allowed.
    /// </summary>
    public int MaxConcurrentStreams { get; set; } = 3;

    /// <summary>
    /// Maximum cache size in bytes (default: 10GB).
    /// </summary>
    public long MaxCacheSize { get; set; } = 10737418240L; // 10GB

    /// <summary>
    /// Maximum number of search results to return per query.
    /// </summary>
    public int MaxSearchResults { get; set; } = 100;

    /// <summary>
    /// Maximum number of indexers to query concurrently.
    /// </summary>
    public int MaxConcurrentIndexerQueries { get; set; } = 5;

    /// <summary>
    /// Search timeout in seconds.
    /// </summary>
    public int SearchTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Stream initialization timeout in seconds.
    /// </summary>
    public int StreamInitializationTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum time to wait for torrent metadata in seconds.
    /// </summary>
    public int MetadataDownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of pieces to buffer ahead of playback position.
    /// </summary>
    public int MaxBufferPieces { get; set; } = 20;

    /// <summary>
    /// Minimum number of pieces to buffer before starting playback.
    /// </summary>
    public int MinBufferPieces { get; set; } = 10;

    /// <summary>
    /// Maximum download speed in bytes per second (0 = unlimited).
    /// </summary>
    public long MaxDownloadSpeed { get; set; } = 0;

    /// <summary>
    /// Maximum upload speed in bytes per second (0 = unlimited).
    /// </summary>
    public long MaxUploadSpeed { get; set; } = 0;

    /// <summary>
    /// Maximum number of peer connections per torrent.
    /// </summary>
    public int MaxPeerConnections { get; set; } = 50;

    /// <summary>
    /// Maximum total number of peer connections across all torrents.
    /// </summary>
    public int MaxTotalPeerConnections { get; set; } = 200;

    /// <summary>
    /// Whether to enable disk cache for torrent data.
    /// </summary>
    public bool EnableDiskCache { get; set; } = true;

    /// <summary>
    /// Disk cache size in bytes (default: 1GB).
    /// </summary>
    public long DiskCacheSize { get; set; } = 1073741824L; // 1GB

    /// <summary>
    /// Whether to automatically clean up old cache files.
    /// </summary>
    public bool AutoCleanupCache { get; set; } = true;

    /// <summary>
    /// Maximum age of cache files in days before cleanup.
    /// </summary>
    public int CacheMaxAgeDays { get; set; } = 7;

    /// <summary>
    /// Validates the resource limits.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (MaxConcurrentStreams < 1 || MaxConcurrentStreams > 10)
            errors.Add("MaxConcurrentStreams must be between 1 and 10");

        if (MaxCacheSize < 1073741824L) // 1GB minimum
            errors.Add("MaxCacheSize must be at least 1GB (1073741824 bytes)");

        if (MaxSearchResults < 10 || MaxSearchResults > 1000)
            errors.Add("MaxSearchResults must be between 10 and 1000");

        if (MaxConcurrentIndexerQueries < 1 || MaxConcurrentIndexerQueries > 20)
            errors.Add("MaxConcurrentIndexerQueries must be between 1 and 20");

        if (SearchTimeoutSeconds < 5 || SearchTimeoutSeconds > 300)
            errors.Add("SearchTimeoutSeconds must be between 5 and 300");

        if (StreamInitializationTimeoutSeconds < 10 || StreamInitializationTimeoutSeconds > 600)
            errors.Add("StreamInitializationTimeoutSeconds must be between 10 and 600");

        if (MetadataDownloadTimeoutSeconds < 5 || MetadataDownloadTimeoutSeconds > 300)
            errors.Add("MetadataDownloadTimeoutSeconds must be between 5 and 300");

        if (MaxBufferPieces < 5 || MaxBufferPieces > 100)
            errors.Add("MaxBufferPieces must be between 5 and 100");

        if (MinBufferPieces < 1 || MinBufferPieces > MaxBufferPieces)
            errors.Add($"MinBufferPieces must be between 1 and {MaxBufferPieces}");

        if (MaxPeerConnections < 10 || MaxPeerConnections > 500)
            errors.Add("MaxPeerConnections must be between 10 and 500");

        if (MaxTotalPeerConnections < MaxPeerConnections)
            errors.Add("MaxTotalPeerConnections must be at least MaxPeerConnections");

        if (DiskCacheSize < 104857600L) // 100MB minimum
            errors.Add("DiskCacheSize must be at least 100MB (104857600 bytes)");

        if (CacheMaxAgeDays < 1)
            errors.Add("CacheMaxAgeDays must be at least 1");

        return errors.Count == 0;
    }
}

