namespace TunnelFin.Models;

/// <summary>
/// Represents the buffer status for a stream.
/// Lifecycle: Updated continuously during streaming, deleted when stream ends.
/// Storage: In-memory as part of StreamSession.
/// </summary>
public class BufferStatus
{
    /// <summary>
    /// List of buffered byte ranges (start, end) for the file.
    /// </summary>
    public required List<(long Start, long End)> BufferedRanges { get; set; }

    /// <summary>
    /// Whether prebuffering is complete and playback can start.
    /// </summary>
    public bool PrebufferComplete { get; set; }

    /// <summary>
    /// Current number of bytes buffered.
    /// </summary>
    public long CurrentBufferedBytes { get; set; }

    /// <summary>
    /// Current download rate in bytes per second.
    /// </summary>
    public double DownloadRate { get; set; }

    /// <summary>
    /// Timestamp of last buffer update.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validates the buffer status according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (BufferedRanges == null)
            throw new ArgumentException("BufferedRanges must not be null", nameof(BufferedRanges));

        if (CurrentBufferedBytes < 0)
            throw new ArgumentException("CurrentBufferedBytes must be non-negative", nameof(CurrentBufferedBytes));

        if (DownloadRate < 0)
            throw new ArgumentException("DownloadRate must be non-negative", nameof(DownloadRate));
    }
}

