namespace TunnelFin.Networking;

/// <summary>
/// CircuitMetrics tracks circuit health metrics (T097).
/// Implements FR-045: Track and expose circuit health metrics (active circuits, hop distribution, failures).
/// </summary>
public class CircuitMetrics
{
    private int _activeCircuitsCount = 0;
    private readonly Dictionary<int, int> _hopDistribution = new();
    private readonly Dictionary<string, int> _failureReasons = new();
    private readonly Dictionary<Guid, DateTime> _circuitStartTimes = new();
    private readonly List<TimeSpan> _completedCircuitLifetimes = new();
    private readonly object _lock = new();
    private int _totalCircuitFailures = 0;
    private int _totalCircuitsCreated = 0;

    /// <summary>
    /// Gets the current number of active circuits (FR-045).
    /// </summary>
    public int ActiveCircuitsCount
    {
        get
        {
            lock (_lock)
            {
                return _activeCircuitsCount;
            }
        }
    }

    /// <summary>
    /// Gets total circuit failures (FR-045).
    /// </summary>
    public int TotalCircuitFailures
    {
        get
        {
            lock (_lock)
            {
                return _totalCircuitFailures;
            }
        }
    }

    /// <summary>
    /// Records a circuit creation.
    /// </summary>
    public void RecordCircuitCreated(int hopCount, Guid? circuitId = null)
    {
        lock (_lock)
        {
            _activeCircuitsCount++;
            _totalCircuitsCreated++;
            
            if (!_hopDistribution.ContainsKey(hopCount))
                _hopDistribution[hopCount] = 0;
            _hopDistribution[hopCount]++;

            if (circuitId.HasValue)
            {
                _circuitStartTimes[circuitId.Value] = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Records a circuit closure.
    /// </summary>
    public void RecordCircuitClosed(Guid? circuitId = null)
    {
        lock (_lock)
        {
            if (_activeCircuitsCount > 0)
                _activeCircuitsCount--;

            if (circuitId.HasValue && _circuitStartTimes.TryGetValue(circuitId.Value, out var startTime))
            {
                var lifetime = DateTime.UtcNow - startTime;
                _completedCircuitLifetimes.Add(lifetime);
                _circuitStartTimes.Remove(circuitId.Value);
            }
        }
    }

    /// <summary>
    /// Records a circuit failure (FR-045).
    /// </summary>
    public void RecordCircuitFailure(string reason)
    {
        lock (_lock)
        {
            _totalCircuitFailures++;
            
            if (!_failureReasons.ContainsKey(reason))
                _failureReasons[reason] = 0;
            _failureReasons[reason]++;
        }
    }

    /// <summary>
    /// Gets hop count distribution (FR-045).
    /// </summary>
    public Dictionary<int, int> GetHopDistribution()
    {
        lock (_lock)
        {
            return new Dictionary<int, int>(_hopDistribution);
        }
    }

    /// <summary>
    /// Gets failure reasons breakdown (FR-045).
    /// </summary>
    public Dictionary<string, int> GetFailureReasons()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_failureReasons);
        }
    }

    /// <summary>
    /// Gets circuit success rate.
    /// </summary>
    public double GetCircuitSuccessRate()
    {
        lock (_lock)
        {
            var totalAttempts = _totalCircuitsCreated + _totalCircuitFailures;
            return totalAttempts > 0 ? (double)_totalCircuitsCreated / totalAttempts : 0;
        }
    }

    /// <summary>
    /// Gets average circuit lifetime.
    /// </summary>
    public TimeSpan GetAverageCircuitLifetime()
    {
        lock (_lock)
        {
            if (_completedCircuitLifetimes.Count == 0)
                return TimeSpan.Zero;

            var totalTicks = _completedCircuitLifetimes.Sum(d => d.Ticks);
            return TimeSpan.FromTicks(totalTicks / _completedCircuitLifetimes.Count);
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _activeCircuitsCount = 0;
            _hopDistribution.Clear();
            _failureReasons.Clear();
            _circuitStartTimes.Clear();
            _completedCircuitLifetimes.Clear();
            _totalCircuitFailures = 0;
            _totalCircuitsCreated = 0;
        }
    }
}

