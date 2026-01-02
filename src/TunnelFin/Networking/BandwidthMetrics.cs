namespace TunnelFin.Networking;

/// <summary>
/// BandwidthMetrics tracks bandwidth usage (T096).
/// Implements FR-044: Track and expose bandwidth usage metrics (upload and download rates).
/// </summary>
public class BandwidthMetrics
{
    private long _totalBytesDownloaded = 0;
    private long _totalBytesUploaded = 0;
    private readonly List<BandwidthSample> _downloadSamples = new();
    private readonly List<BandwidthSample> _uploadSamples = new();
    private readonly object _lock = new();
    private const int MaxSamples = 100;

    /// <summary>
    /// Gets total bytes downloaded (FR-044).
    /// </summary>
    public long TotalBytesDownloaded
    {
        get
        {
            lock (_lock)
            {
                return _totalBytesDownloaded;
            }
        }
    }

    /// <summary>
    /// Gets total bytes uploaded (FR-044).
    /// </summary>
    public long TotalBytesUploaded
    {
        get
        {
            lock (_lock)
            {
                return _totalBytesUploaded;
            }
        }
    }

    /// <summary>
    /// Records downloaded bytes.
    /// </summary>
    public void RecordDownload(long bytes)
    {
        lock (_lock)
        {
            _totalBytesDownloaded += bytes;
            _downloadSamples.Add(new BandwidthSample
            {
                Bytes = bytes,
                Timestamp = DateTime.UtcNow
            });

            if (_downloadSamples.Count > MaxSamples)
                _downloadSamples.RemoveAt(0);
        }
    }

    /// <summary>
    /// Records uploaded bytes.
    /// </summary>
    public void RecordUpload(long bytes)
    {
        lock (_lock)
        {
            _totalBytesUploaded += bytes;
            _uploadSamples.Add(new BandwidthSample
            {
                Bytes = bytes,
                Timestamp = DateTime.UtcNow
            });

            if (_uploadSamples.Count > MaxSamples)
                _uploadSamples.RemoveAt(0);
        }
    }

    /// <summary>
    /// Gets download rate in bytes per second.
    /// </summary>
    public double GetDownloadRate()
    {
        lock (_lock)
        {
            return CalculateRate(_downloadSamples);
        }
    }

    /// <summary>
    /// Gets upload rate in bytes per second.
    /// </summary>
    public double GetUploadRate()
    {
        lock (_lock)
        {
            return CalculateRate(_uploadSamples);
        }
    }

    /// <summary>
    /// Gets download rate in megabits per second.
    /// </summary>
    public double GetDownloadRateMbps()
    {
        return GetDownloadRate() * 8 / 1_000_000;
    }

    /// <summary>
    /// Gets upload rate in megabits per second.
    /// </summary>
    public double GetUploadRateMbps()
    {
        return GetUploadRate() * 8 / 1_000_000;
    }

    /// <summary>
    /// Gets peak download rate.
    /// </summary>
    public double GetPeakDownloadRate()
    {
        lock (_lock)
        {
            return _downloadSamples.Count > 0 
                ? _downloadSamples.Max(s => s.Bytes) 
                : 0;
        }
    }

    /// <summary>
    /// Gets peak upload rate.
    /// </summary>
    public double GetPeakUploadRate()
    {
        lock (_lock)
        {
            return _uploadSamples.Count > 0 
                ? _uploadSamples.Max(s => s.Bytes) 
                : 0;
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _totalBytesDownloaded = 0;
            _totalBytesUploaded = 0;
            _downloadSamples.Clear();
            _uploadSamples.Clear();
        }
    }

    private double CalculateRate(List<BandwidthSample> samples)
    {
        if (samples.Count < 2)
            return 0;

        var recentSamples = samples.TakeLast(10).ToList();
        var totalBytes = recentSamples.Sum(s => s.Bytes);
        var duration = (recentSamples.Last().Timestamp - recentSamples.First().Timestamp).TotalSeconds;

        return duration > 0 ? totalBytes / duration : 0;
    }
}

/// <summary>
/// Represents a bandwidth sample.
/// </summary>
internal class BandwidthSample
{
    public long Bytes { get; set; }
    public DateTime Timestamp { get; set; }
}

