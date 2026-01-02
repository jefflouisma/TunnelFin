namespace TunnelFin.Models;

/// <summary>
/// Represents the current status of a torrent download.
/// Used for tracking download progress and health metrics (FR-011).
/// </summary>
public class TorrentStatus
{
    /// <summary>
    /// Unique identifier for the torrent.
    /// </summary>
    public Guid TorrentId { get; set; }

    /// <summary>
    /// Download progress as a percentage (0-100).
    /// </summary>
    public double DownloadProgress { get; set; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// Current upload speed in bytes per second.
    /// </summary>
    public long UploadSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// Number of connected peers.
    /// </summary>
    public int PeerCount { get; set; }

    /// <summary>
    /// Total size of the torrent in bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Number of bytes downloaded so far.
    /// </summary>
    public long DownloadedBytes { get; set; }

    /// <summary>
    /// Current state of the torrent (e.g., Downloading, Seeding, Stopped).
    /// </summary>
    public TorrentState State { get; set; }

    /// <summary>
    /// Timestamp when the torrent was added.
    /// </summary>
    public DateTime AddedAt { get; set; }
}

/// <summary>
/// Represents the state of a torrent.
/// </summary>
public enum TorrentState
{
    /// <summary>
    /// Torrent is initializing (fetching metadata).
    /// </summary>
    Initializing,

    /// <summary>
    /// Torrent is actively downloading.
    /// </summary>
    Downloading,

    /// <summary>
    /// Torrent is seeding (upload only).
    /// </summary>
    Seeding,

    /// <summary>
    /// Torrent is paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Torrent has stopped.
    /// </summary>
    Stopped,

    /// <summary>
    /// Torrent encountered an error.
    /// </summary>
    Error
}

