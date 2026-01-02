using System.Text;

namespace TunnelFin.Discovery;

/// <summary>
/// CacheMetrics tracks cache hit rates (T099).
/// Implements FR-047: Track and expose cache hit rate metrics (metadata cache, torrent data cache).
/// </summary>
public class CacheMetrics
{
    private long _metadataCacheHits = 0;
    private long _metadataCacheMisses = 0;
    private long _torrentCacheHits = 0;
    private long _torrentCacheMisses = 0;
    private readonly object _lock = new();

    /// <summary>
    /// Gets metadata cache hits (FR-047).
    /// </summary>
    public long MetadataCacheHits
    {
        get
        {
            lock (_lock)
            {
                return _metadataCacheHits;
            }
        }
    }

    /// <summary>
    /// Gets metadata cache misses (FR-047).
    /// </summary>
    public long MetadataCacheMisses
    {
        get
        {
            lock (_lock)
            {
                return _metadataCacheMisses;
            }
        }
    }

    /// <summary>
    /// Gets torrent cache hits (FR-047).
    /// </summary>
    public long TorrentCacheHits
    {
        get
        {
            lock (_lock)
            {
                return _torrentCacheHits;
            }
        }
    }

    /// <summary>
    /// Gets torrent cache misses (FR-047).
    /// </summary>
    public long TorrentCacheMisses
    {
        get
        {
            lock (_lock)
            {
                return _torrentCacheMisses;
            }
        }
    }

    /// <summary>
    /// Records a metadata cache hit.
    /// </summary>
    public void RecordMetadataCacheHit()
    {
        lock (_lock)
        {
            _metadataCacheHits++;
        }
    }

    /// <summary>
    /// Records a metadata cache miss.
    /// </summary>
    public void RecordMetadataCacheMiss()
    {
        lock (_lock)
        {
            _metadataCacheMisses++;
        }
    }

    /// <summary>
    /// Records a torrent cache hit.
    /// </summary>
    public void RecordTorrentCacheHit()
    {
        lock (_lock)
        {
            _torrentCacheHits++;
        }
    }

    /// <summary>
    /// Records a torrent cache miss.
    /// </summary>
    public void RecordTorrentCacheMiss()
    {
        lock (_lock)
        {
            _torrentCacheMisses++;
        }
    }

    /// <summary>
    /// Gets metadata cache hit rate (FR-047).
    /// </summary>
    public double GetMetadataCacheHitRate()
    {
        lock (_lock)
        {
            var total = _metadataCacheHits + _metadataCacheMisses;
            return total > 0 ? (double)_metadataCacheHits / total : 0;
        }
    }

    /// <summary>
    /// Gets torrent cache hit rate (FR-047).
    /// </summary>
    public double GetTorrentCacheHitRate()
    {
        lock (_lock)
        {
            var total = _torrentCacheHits + _torrentCacheMisses;
            return total > 0 ? (double)_torrentCacheHits / total : 0;
        }
    }

    /// <summary>
    /// Gets overall cache hit rate (FR-047).
    /// </summary>
    public double GetOverallCacheHitRate()
    {
        lock (_lock)
        {
            var totalHits = _metadataCacheHits + _torrentCacheHits;
            var totalRequests = totalHits + _metadataCacheMisses + _torrentCacheMisses;
            return totalRequests > 0 ? (double)totalHits / totalRequests : 0;
        }
    }

    /// <summary>
    /// Gets cache summary string.
    /// </summary>
    public string GetCacheSummary()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Metadata Cache: {_metadataCacheHits} hits, {_metadataCacheMisses} misses ({GetMetadataCacheHitRate():P0} hit rate)");
            sb.AppendLine($"Torrent Cache: {_torrentCacheHits} hits, {_torrentCacheMisses} misses ({GetTorrentCacheHitRate():P0} hit rate)");
            sb.AppendLine($"Overall: {GetOverallCacheHitRate():P0} hit rate");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _metadataCacheHits = 0;
            _metadataCacheMisses = 0;
            _torrentCacheHits = 0;
            _torrentCacheMisses = 0;
        }
    }
}

