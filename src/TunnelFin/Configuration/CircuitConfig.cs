namespace TunnelFin.Configuration;

/// <summary>
/// Configuration for Tribler circuit routing.
/// Lifecycle: Created by user in plugin settings, persists in Jellyfin configuration.
/// Storage: Jellyfin's PluginConfiguration (XML file in config directory).
/// </summary>
public class CircuitConfig
{
    /// <summary>
    /// Number of hops in circuit (default: 3 for anonymity).
    /// </summary>
    public int HopCount { get; set; } = 3;

    /// <summary>
    /// Timeout for circuit establishment in seconds (default: 30).
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Number of circuits to maintain in pool (default: 5).
    /// </summary>
    public int PoolSize { get; set; } = 5;

    /// <summary>
    /// Whether to allow non-anonymous fallback when circuits unavailable (default: false).
    /// </summary>
    public bool AllowNonAnonymousFallback { get; set; } = false;

    /// <summary>
    /// Minimum latency threshold in milliseconds for healthy circuit (default: 500).
    /// </summary>
    public double MinLatencyMs { get; set; } = 500.0;

    /// <summary>
    /// Maximum latency threshold in milliseconds for healthy circuit (default: 5000).
    /// </summary>
    public double MaxLatencyMs { get; set; } = 5000.0;

    /// <summary>
    /// Health check interval in seconds (default: 60).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Validates the circuit configuration according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (HopCount < 1 || HopCount > 10)
            throw new ArgumentException("HopCount must be between 1 and 10", nameof(HopCount));

        if (Timeout < 5 || Timeout > 120)
            throw new ArgumentException("Timeout must be between 5 and 120 seconds", nameof(Timeout));

        if (PoolSize < 1 || PoolSize > 20)
            throw new ArgumentException("PoolSize must be between 1 and 20", nameof(PoolSize));

        if (MinLatencyMs < 0 || MinLatencyMs > MaxLatencyMs)
            throw new ArgumentException("MinLatencyMs must be non-negative and less than MaxLatencyMs", nameof(MinLatencyMs));

        if (MaxLatencyMs < 100 || MaxLatencyMs > 30000)
            throw new ArgumentException("MaxLatencyMs must be between 100 and 30000", nameof(MaxLatencyMs));

        if (HealthCheckIntervalSeconds < 10 || HealthCheckIntervalSeconds > 600)
            throw new ArgumentException("HealthCheckIntervalSeconds must be between 10 and 600", nameof(HealthCheckIntervalSeconds));
    }
}

