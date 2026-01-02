namespace TunnelFin.Networking;

/// <summary>
/// Tracks bandwidth usage for proportional relay contribution (FR-005, SC-010).
/// Ensures network fairness by tracking download vs relay ratios.
/// </summary>
public class BandwidthTracker
{
    private long _totalDownloadedBytes;
    private long _totalUploadedBytes;
    private long _totalRelayedBytes;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the total bytes downloaded by this node.
    /// </summary>
    public long TotalDownloadedBytes
    {
        get
        {
            lock (_lock)
                return _totalDownloadedBytes;
        }
    }

    /// <summary>
    /// Gets the total bytes uploaded by this node (seeding).
    /// </summary>
    public long TotalUploadedBytes
    {
        get
        {
            lock (_lock)
                return _totalUploadedBytes;
        }
    }

    /// <summary>
    /// Gets the total bytes relayed for other peers (circuit relay).
    /// </summary>
    public long TotalRelayedBytes
    {
        get
        {
            lock (_lock)
                return _totalRelayedBytes;
        }
    }

    /// <summary>
    /// Records bytes downloaded.
    /// </summary>
    /// <param name="bytes">Number of bytes downloaded.</param>
    public void RecordDownload(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be non-negative");

        lock (_lock)
            _totalDownloadedBytes += bytes;
    }

    /// <summary>
    /// Records bytes uploaded (seeding).
    /// </summary>
    /// <param name="bytes">Number of bytes uploaded.</param>
    public void RecordUpload(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be non-negative");

        lock (_lock)
            _totalUploadedBytes += bytes;
    }

    /// <summary>
    /// Records bytes relayed for other peers.
    /// </summary>
    /// <param name="bytes">Number of bytes relayed.</param>
    public void RecordRelay(long bytes)
    {
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be non-negative");

        lock (_lock)
            _totalRelayedBytes += bytes;
    }

    /// <summary>
    /// Gets the relay ratio (relayed / downloaded).
    /// </summary>
    /// <returns>Ratio between 0.0 and infinity. 1.0 means proportional contribution.</returns>
    public double GetRelayRatio()
    {
        lock (_lock)
        {
            if (_totalDownloadedBytes == 0)
                return 0.0;

            return (double)_totalRelayedBytes / _totalDownloadedBytes;
        }
    }

    /// <summary>
    /// Gets the number of bytes that still need to be relayed to achieve proportional contribution.
    /// </summary>
    /// <returns>Number of bytes to relay. 0 if already proportional or over-contributing.</returns>
    public long GetRequiredRelayBytes()
    {
        lock (_lock)
        {
            long required = _totalDownloadedBytes - _totalRelayedBytes;
            return Math.Max(0, required);
        }
    }

    /// <summary>
    /// Checks if relay contribution is proportional within a threshold (FR-005, SC-010).
    /// </summary>
    /// <param name="threshold">Acceptable deviation from 1.0 ratio (default 0.05 = 5%).</param>
    /// <returns>True if relay ratio is within threshold of 1.0.</returns>
    public bool IsProportional(double threshold = 0.05)
    {
        if (threshold < 0 || threshold > 1)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Threshold must be between 0 and 1");

        var ratio = GetRelayRatio();
        return Math.Abs(ratio - 1.0) <= threshold;
    }
}

