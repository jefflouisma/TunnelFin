namespace TunnelFin.Models;

/// <summary>
/// Represents an active streaming session for a torrent file.
/// Lifecycle: Created when user starts playback, deleted when stream ends or times out (30 min idle).
/// Storage: In-memory dictionary keyed by SessionId.
/// </summary>
public class StreamSession
{
    /// <summary>
    /// Unique identifier for this stream session.
    /// </summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// InfoHash of torrent being streamed.
    /// </summary>
    public required string InfoHash { get; init; }

    /// <summary>
    /// Path of file within torrent being streamed.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// ID of circuit used for peer connections (null if direct).
    /// </summary>
    public Guid? CircuitId { get; set; }

    /// <summary>
    /// Current buffer state.
    /// </summary>
    public required BufferStatus BufferStatus { get; set; }

    /// <summary>
    /// Current byte offset in file (for seeking).
    /// </summary>
    public long PlaybackPosition { get; set; }

    /// <summary>
    /// Timestamp when stream started.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of last HTTP request (for idle timeout).
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// HTTP URL for this stream (e.g., "/stream/{sessionId}").
    /// </summary>
    public required string StreamUrl { get; init; }

    /// <summary>
    /// Reference to MonoTorrent TorrentManager instance.
    /// </summary>
    public object? TorrentManager { get; set; }

    /// <summary>
    /// Validates the stream session according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InfoHash) || InfoHash.Length != 40)
            throw new ArgumentException("InfoHash must be exactly 40 hexadecimal characters", nameof(InfoHash));

        if (string.IsNullOrWhiteSpace(FilePath))
            throw new ArgumentException("FilePath must not be empty", nameof(FilePath));

        if (PlaybackPosition < 0)
            throw new ArgumentException("PlaybackPosition must be non-negative", nameof(PlaybackPosition));

        if (string.IsNullOrWhiteSpace(StreamUrl))
            throw new ArgumentException("StreamUrl must not be empty", nameof(StreamUrl));
    }
}

