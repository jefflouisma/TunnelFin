namespace TunnelFin.Models;

/// <summary>
/// Represents real-time health metrics for an active stream (FR-011).
/// </summary>
public class StreamHealth
{
    /// <summary>
    /// Unique identifier for the stream.
    /// </summary>
    public Guid StreamId { get; set; }

    /// <summary>
    /// Number of connected peers providing data.
    /// </summary>
    public int PeerCount { get; set; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// Current buffer size in seconds (FR-010, SC-003).
    /// </summary>
    public double BufferSeconds { get; set; }

    /// <summary>
    /// Whether the stream is currently buffering.
    /// </summary>
    public bool IsBuffering { get; set; }

    /// <summary>
    /// Whether the stream is ready for playback (minimum buffer reached).
    /// </summary>
    public bool IsReadyForPlayback { get; set; }

    /// <summary>
    /// Download progress as a percentage (0-100).
    /// </summary>
    public double DownloadProgress { get; set; }

    /// <summary>
    /// Timestamp of the last update.
    /// </summary>
    public DateTime LastUpdated { get; set; }
}

