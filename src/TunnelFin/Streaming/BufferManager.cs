using System.Collections.Concurrent;
using TunnelFin.Models;

namespace TunnelFin.Streaming;

/// <summary>
/// Manages buffer status tracking for active streams (FR-010, SC-003).
/// Ensures >10s buffer requirement is met before playback starts.
/// </summary>
public class BufferManager
{
    private readonly ConcurrentDictionary<Guid, BufferState> _buffers = new();
    private readonly int _minimumBufferSeconds;

    /// <summary>
    /// Minimum buffer size in seconds required before playback can start.
    /// </summary>
    public int MinimumBufferSeconds => _minimumBufferSeconds;

    /// <summary>
    /// Initializes a new instance of the BufferManager class.
    /// </summary>
    /// <param name="minimumBufferSeconds">Minimum buffer size in seconds (default: 10).</param>
    public BufferManager(int minimumBufferSeconds = 10)
    {
        if (minimumBufferSeconds < 0)
            throw new ArgumentException("Minimum buffer seconds must be non-negative", nameof(minimumBufferSeconds));

        _minimumBufferSeconds = minimumBufferSeconds;
    }

    /// <summary>
    /// Gets the current buffer status for a stream.
    /// Creates a new buffer state if the stream doesn't exist.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <returns>Current buffer status.</returns>
    public BufferStatus GetBufferStatus(Guid streamId)
    {
        var state = _buffers.GetOrAdd(streamId, _ => new BufferState());

        return new BufferStatus
        {
            BufferedRanges = state.BufferedRanges,
            PrebufferComplete = state.BufferedSeconds >= _minimumBufferSeconds,
            CurrentBufferedBytes = state.BufferedBytes,
            DownloadRate = state.DownloadSpeedBytesPerSecond,
            LastUpdated = state.LastUpdated
        };
    }

    /// <summary>
    /// Updates the buffer state for a stream based on downloaded data.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <param name="bufferedBytes">Total number of bytes currently buffered.</param>
    /// <param name="downloadSpeedBytesPerSecond">Current download speed in bytes per second.</param>
    public void UpdateBuffer(Guid streamId, long bufferedBytes, long downloadSpeedBytesPerSecond)
    {
        var state = _buffers.GetOrAdd(streamId, _ => new BufferState());

        state.BufferedBytes = bufferedBytes;
        state.DownloadSpeedBytesPerSecond = downloadSpeedBytesPerSecond;

        // Calculate buffered seconds based on download speed
        // If speed is 0, we can't calculate time, so treat as 0 seconds
        if (downloadSpeedBytesPerSecond > 0)
        {
            state.BufferedSeconds = (double)bufferedBytes / downloadSpeedBytesPerSecond;
        }
        else
        {
            state.BufferedSeconds = 0;
        }

        // Update buffering state
        state.IsBuffering = state.BufferedSeconds < _minimumBufferSeconds;
        state.LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the playback position, reducing the buffered data.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <param name="consumedBytes">Number of bytes consumed by playback.</param>
    public void UpdatePlaybackPosition(Guid streamId, long consumedBytes)
    {
        if (_buffers.TryGetValue(streamId, out var state))
        {
            state.BufferedBytes = Math.Max(0, state.BufferedBytes - consumedBytes);

            // Recalculate buffered seconds
            if (state.DownloadSpeedBytesPerSecond > 0)
            {
                state.BufferedSeconds = (double)state.BufferedBytes / state.DownloadSpeedBytesPerSecond;
            }
            else
            {
                state.BufferedSeconds = 0;
            }

            state.IsBuffering = state.BufferedSeconds < _minimumBufferSeconds;
            state.LastUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes buffer tracking for a stream.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    public void RemoveStream(Guid streamId)
    {
        _buffers.TryRemove(streamId, out _);
    }

    /// <summary>
    /// Internal state for tracking buffer information.
    /// </summary>
    private class BufferState
    {
        public List<(long Start, long End)> BufferedRanges { get; set; } = new();
        public long BufferedBytes { get; set; }
        public long DownloadSpeedBytesPerSecond { get; set; }
        public double BufferedSeconds { get; set; }
        public bool IsBuffering { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

