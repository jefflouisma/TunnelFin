namespace TunnelFin.Streaming;

/// <summary>
/// StreamMetrics tracks active streams and stream statistics (T095).
/// Implements FR-043: Track and expose active streams count metric.
/// </summary>
public class StreamMetrics
{
    private readonly HashSet<Guid> _activeStreams = new();
    private readonly Dictionary<Guid, DateTime> _streamStartTimes = new();
    private readonly List<TimeSpan> _completedStreamDurations = new();
    private readonly object _lock = new();
    private int _totalStreamsStarted = 0;

    /// <summary>
    /// Gets the current number of active streams (FR-043).
    /// </summary>
    public int ActiveStreamsCount
    {
        get
        {
            lock (_lock)
            {
                return _activeStreams.Count;
            }
        }
    }

    /// <summary>
    /// Increments the active streams counter.
    /// </summary>
    public void IncrementActiveStreams()
    {
        lock (_lock)
        {
            _activeStreams.Add(Guid.NewGuid());
        }
    }

    /// <summary>
    /// Decrements the active streams counter.
    /// </summary>
    public void DecrementActiveStreams()
    {
        lock (_lock)
        {
            if (_activeStreams.Count > 0)
            {
                _activeStreams.Remove(_activeStreams.First());
            }
        }
    }

    /// <summary>
    /// Records a stream start.
    /// </summary>
    public void RecordStreamStart(Guid streamId)
    {
        lock (_lock)
        {
            _activeStreams.Add(streamId);
            _streamStartTimes[streamId] = DateTime.UtcNow;
            _totalStreamsStarted++;
        }
    }

    /// <summary>
    /// Records a stream end.
    /// </summary>
    public void RecordStreamEnd(Guid streamId)
    {
        lock (_lock)
        {
            _activeStreams.Remove(streamId);
            
            if (_streamStartTimes.TryGetValue(streamId, out var startTime))
            {
                var duration = DateTime.UtcNow - startTime;
                _completedStreamDurations.Add(duration);
                _streamStartTimes.Remove(streamId);
            }
        }
    }

    /// <summary>
    /// Checks if a stream is active.
    /// </summary>
    public bool IsStreamActive(Guid streamId)
    {
        lock (_lock)
        {
            return _activeStreams.Contains(streamId);
        }
    }

    /// <summary>
    /// Gets total streams started (cumulative).
    /// </summary>
    public int GetTotalStreamsStarted()
    {
        lock (_lock)
        {
            return _totalStreamsStarted;
        }
    }

    /// <summary>
    /// Gets average stream duration.
    /// </summary>
    public TimeSpan GetAverageStreamDuration()
    {
        lock (_lock)
        {
            if (_completedStreamDurations.Count == 0)
                return TimeSpan.Zero;

            var totalTicks = _completedStreamDurations.Sum(d => d.Ticks);
            return TimeSpan.FromTicks(totalTicks / _completedStreamDurations.Count);
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _activeStreams.Clear();
            _streamStartTimes.Clear();
            _completedStreamDurations.Clear();
            _totalStreamsStarted = 0;
        }
    }
}

