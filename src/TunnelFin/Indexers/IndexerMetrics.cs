namespace TunnelFin.Indexers;

/// <summary>
/// IndexerMetrics tracks indexer response times (T098).
/// Implements FR-046: Track and expose indexer response time metrics for each configured indexer.
/// </summary>
public class IndexerMetrics
{
    private readonly Dictionary<string, IndexerStats> _indexerStats = new();
    private readonly object _lock = new();

    /// <summary>
    /// Records an indexer request (FR-046).
    /// </summary>
    public void RecordRequest(string indexerName, TimeSpan responseTime, bool success)
    {
        if (string.IsNullOrWhiteSpace(indexerName))
            throw new ArgumentException("Indexer name cannot be empty", nameof(indexerName));

        lock (_lock)
        {
            if (!_indexerStats.ContainsKey(indexerName))
            {
                _indexerStats[indexerName] = new IndexerStats();
            }

            var stats = _indexerStats[indexerName];
            stats.TotalRequests++;
            stats.ResponseTimes.Add(responseTime);
            
            if (success)
                stats.SuccessfulRequests++;

            if (responseTime > stats.PeakResponseTime)
                stats.PeakResponseTime = responseTime;
        }
    }

    /// <summary>
    /// Gets average response time for an indexer (FR-046).
    /// </summary>
    public TimeSpan GetAverageResponseTime(string indexerName)
    {
        lock (_lock)
        {
            if (!_indexerStats.TryGetValue(indexerName, out var stats) || stats.ResponseTimes.Count == 0)
                return TimeSpan.Zero;

            var totalTicks = stats.ResponseTimes.Sum(t => t.Ticks);
            return TimeSpan.FromTicks(totalTicks / stats.ResponseTimes.Count);
        }
    }

    /// <summary>
    /// Gets success rate for an indexer.
    /// </summary>
    public double GetSuccessRate(string indexerName)
    {
        lock (_lock)
        {
            if (!_indexerStats.TryGetValue(indexerName, out var stats) || stats.TotalRequests == 0)
                return 0;

            return (double)stats.SuccessfulRequests / stats.TotalRequests;
        }
    }

    /// <summary>
    /// Gets total requests for an indexer.
    /// </summary>
    public int GetTotalRequests(string indexerName)
    {
        lock (_lock)
        {
            return _indexerStats.TryGetValue(indexerName, out var stats) 
                ? stats.TotalRequests 
                : 0;
        }
    }

    /// <summary>
    /// Gets peak response time for an indexer.
    /// </summary>
    public TimeSpan GetPeakResponseTime(string indexerName)
    {
        lock (_lock)
        {
            return _indexerStats.TryGetValue(indexerName, out var stats) 
                ? stats.PeakResponseTime 
                : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Gets all indexer statistics.
    /// </summary>
    public Dictionary<string, IndexerStatsSnapshot> GetAllIndexerStats()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, IndexerStatsSnapshot>();
            
            foreach (var (name, stats) in _indexerStats)
            {
                result[name] = new IndexerStatsSnapshot
                {
                    TotalRequests = stats.TotalRequests,
                    SuccessfulRequests = stats.SuccessfulRequests,
                    AverageResponseTime = GetAverageResponseTime(name),
                    PeakResponseTime = stats.PeakResponseTime
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Resets metrics for a specific indexer.
    /// </summary>
    public void Reset(string indexerName)
    {
        lock (_lock)
        {
            _indexerStats.Remove(indexerName);
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void ResetAll()
    {
        lock (_lock)
        {
            _indexerStats.Clear();
        }
    }
}

/// <summary>
/// Internal indexer statistics.
/// </summary>
internal class IndexerStats
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public List<TimeSpan> ResponseTimes { get; set; } = new();
    public TimeSpan PeakResponseTime { get; set; }
}

/// <summary>
/// Indexer statistics snapshot.
/// </summary>
public class IndexerStatsSnapshot
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan PeakResponseTime { get; set; }
}

