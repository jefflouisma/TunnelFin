namespace TunnelFin.Configuration;

/// <summary>
/// Configuration for HTTP streaming settings.
/// Lifecycle: Created by user in plugin settings, persists in Jellyfin configuration.
/// Storage: Jellyfin's PluginConfiguration (XML file in config directory).
/// </summary>
public class StreamingConfig
{
    /// <summary>
    /// Prebuffer size in bytes before starting playback (default: 5MB).
    /// </summary>
    public long PrebufferSize { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Maximum number of concurrent streams (default: 10).
    /// </summary>
    public int MaxConcurrentStreams { get; set; } = 10;

    /// <summary>
    /// HTTP streaming URL prefix (default: "/tunnelfin/stream/").
    /// </summary>
    public string HttpStreamingPrefix { get; set; } = "/tunnelfin/stream/";

    /// <summary>
    /// Idle timeout for stream sessions in minutes (default: 30).
    /// </summary>
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Disk cache size in bytes for MonoTorrent (default: 50MB).
    /// </summary>
    public long DiskCacheBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Maximum connections per torrent (default: 50).
    /// </summary>
    public int MaxConnectionsPerTorrent { get; set; } = 50;

    /// <summary>
    /// Maximum total connections across all torrents (default: 200).
    /// </summary>
    public int MaxConnectionsTotal { get; set; } = 200;

    /// <summary>
    /// Validates the streaming configuration according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (PrebufferSize < 1024 * 1024 || PrebufferSize > 100 * 1024 * 1024)
            throw new ArgumentException("PrebufferSize must be between 1MB and 100MB", nameof(PrebufferSize));

        if (MaxConcurrentStreams < 1 || MaxConcurrentStreams > 50)
            throw new ArgumentException("MaxConcurrentStreams must be between 1 and 50", nameof(MaxConcurrentStreams));

        if (string.IsNullOrWhiteSpace(HttpStreamingPrefix))
            throw new ArgumentException("HttpStreamingPrefix must not be empty", nameof(HttpStreamingPrefix));

        if (!HttpStreamingPrefix.StartsWith("/"))
            throw new ArgumentException("HttpStreamingPrefix must start with '/'", nameof(HttpStreamingPrefix));

        if (IdleTimeoutMinutes < 5 || IdleTimeoutMinutes > 120)
            throw new ArgumentException("IdleTimeoutMinutes must be between 5 and 120", nameof(IdleTimeoutMinutes));

        if (DiskCacheBytes < 10 * 1024 * 1024 || DiskCacheBytes > 500 * 1024 * 1024)
            throw new ArgumentException("DiskCacheBytes must be between 10MB and 500MB", nameof(DiskCacheBytes));

        if (MaxConnectionsPerTorrent < 10 || MaxConnectionsPerTorrent > 200)
            throw new ArgumentException("MaxConnectionsPerTorrent must be between 10 and 200", nameof(MaxConnectionsPerTorrent));

        if (MaxConnectionsTotal < 50 || MaxConnectionsTotal > 1000)
            throw new ArgumentException("MaxConnectionsTotal must be between 50 and 1000", nameof(MaxConnectionsTotal));
    }
}

