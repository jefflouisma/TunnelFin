namespace TunnelFin.Models;

/// <summary>
/// Represents the buffer status for a stream (FR-010, SC-003).
/// Tracks buffered data to ensure >10s buffer requirement.
/// </summary>
public class BufferStatus
{
    /// <summary>
    /// Unique identifier for the stream.
    /// </summary>
    public Guid StreamId { get; set; }

    /// <summary>
    /// Number of seconds of playback data currently buffered.
    /// </summary>
    public double BufferedSeconds { get; set; }

    /// <summary>
    /// Whether the stream is currently buffering.
    /// </summary>
    public bool IsBuffering { get; set; }

    /// <summary>
    /// Whether the stream has reached minimum buffer and is ready for playback.
    /// </summary>
    public bool IsReadyForPlayback { get; set; }

    /// <summary>
    /// Number of bytes currently buffered.
    /// </summary>
    public long BufferedBytes { get; set; }

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long DownloadSpeedBytesPerSecond { get; set; }

    /// <summary>
    /// Timestamp of the last buffer update.
    /// </summary>
    public DateTime LastUpdated { get; set; }
}

