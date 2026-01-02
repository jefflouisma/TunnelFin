namespace TunnelFin.Configuration;

/// <summary>
/// Configuration settings for anonymity and circuit management.
/// Controls how the plugin establishes and manages multi-hop circuits through the Tribler network.
/// </summary>
public class AnonymitySettings
{
    /// <summary>
    /// Default number of hops for anonymity circuits (1-3).
    /// Higher hop count provides more anonymity but increases latency.
    /// </summary>
    public int DefaultHopCount { get; set; } = 3;

    /// <summary>
    /// Minimum number of hops allowed (must be >= 1).
    /// </summary>
    public int MinHopCount { get; set; } = 1;

    /// <summary>
    /// Maximum number of hops allowed (must be <= 3).
    /// </summary>
    public int MaxHopCount { get; set; } = 3;

    /// <summary>
    /// Whether to allow non-anonymous fallback if circuit creation fails.
    /// If false, streaming will fail if anonymity cannot be established.
    /// </summary>
    public bool AllowNonAnonymousFallback { get; set; } = false;

    /// <summary>
    /// Enable bandwidth contribution/relay (FR-005).
    /// Default: true (proportional contribution).
    /// </summary>
    public bool EnableBandwidthContribution { get; set; } = true;

    /// <summary>
    /// Maximum time to wait for circuit establishment in seconds.
    /// </summary>
    public int CircuitEstablishmentTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent circuits to maintain.
    /// </summary>
    public int MaxConcurrentCircuits { get; set; } = 10;

    /// <summary>
    /// Circuit lifetime in seconds before automatic renewal.
    /// Circuits are periodically rotated for enhanced privacy.
    /// </summary>
    public int CircuitLifetimeSeconds { get; set; } = 600; // 10 minutes

    /// <summary>
    /// Whether to automatically rotate circuits for active streams.
    /// </summary>
    public bool AutoRotateCircuits { get; set; } = true;

    /// <summary>
    /// Minimum number of relay nodes required in the network before allowing circuit creation.
    /// </summary>
    public int MinRelayNodes { get; set; } = 10;

    /// <summary>
    /// Whether to prefer relay nodes with high bandwidth.
    /// </summary>
    public bool PreferHighBandwidthRelays { get; set; } = true;

    /// <summary>
    /// Whether to prefer relay nodes with low latency.
    /// </summary>
    public bool PreferLowLatencyRelays { get; set; } = true;

    /// <summary>
    /// Maximum acceptable round-trip time (RTT) for a circuit in milliseconds.
    /// Circuits with higher RTT will be rejected.
    /// </summary>
    public int MaxCircuitRttMs { get; set; } = 1000;

    /// <summary>
    /// Whether to enable circuit health monitoring.
    /// </summary>
    public bool EnableCircuitHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Interval for circuit health checks in seconds.
    /// </summary>
    public int CircuitHealthCheckIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to log circuit creation and management events.
    /// Note: Logs will exclude PII and content titles per FR-037, FR-041.
    /// </summary>
    public bool EnableCircuitLogging { get; set; } = true;

    /// <summary>
    /// UDP port to bind for IPv8 protocol (T060).
    /// 0 = random available port (default, per py-ipv8 behavior).
    /// </summary>
    public ushort UdpPort { get; set; } = 0;

    /// <summary>
    /// Bootstrap discovery timeout in seconds (T060).
    /// Maximum time to wait for bootstrap nodes to respond.
    /// </summary>
    public int BootstrapTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Circuit heartbeat interval in seconds (T060).
    /// How often to send keepalive messages to maintain circuits.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum number of concurrent circuits to maintain (T040).
    /// </summary>
    public int MinConcurrentCircuits { get; set; } = 2;

    /// <summary>
    /// Minimum relay reliability score (0.0-1.0) for circuit selection (T040).
    /// Relays with lower reliability will not be used.
    /// </summary>
    public double MinRelayReliability { get; set; } = 0.7;

    /// <summary>
    /// Sets the default hop count with validation (T087).
    /// </summary>
    /// <param name="hopCount">Hop count (1-3)</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when hop count is outside valid range</exception>
    public void SetHopCount(int hopCount)
    {
        if (hopCount < MinHopCount || hopCount > MaxHopCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hopCount),
                $"Hop count must be between {MinHopCount} and {MaxHopCount}");
        }

        DefaultHopCount = hopCount;
    }

    /// <summary>
    /// Gets the effective hop count for circuit creation (T087).
    /// </summary>
    /// <returns>Current default hop count</returns>
    public int GetEffectiveHopCount()
    {
        return DefaultHopCount;
    }

    /// <summary>
    /// Validates the anonymity settings (T084).
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate(out List<string> errors)
    {
        return IsValid(out errors);
    }

    /// <summary>
    /// Validates the anonymity settings.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (DefaultHopCount < MinHopCount || DefaultHopCount > MaxHopCount)
            errors.Add($"DefaultHopCount must be between {MinHopCount} and {MaxHopCount}");

        if (MinHopCount < 1)
            errors.Add("MinHopCount must be at least 1");

        if (MaxHopCount > 3)
            errors.Add("MaxHopCount cannot exceed 3");

        if (CircuitEstablishmentTimeoutSeconds < 5)
            errors.Add("CircuitEstablishmentTimeoutSeconds must be at least 5");

        if (MaxConcurrentCircuits < 1)
            errors.Add("MaxConcurrentCircuits must be at least 1");

        if (CircuitLifetimeSeconds < 60)
            errors.Add("CircuitLifetimeSeconds must be at least 60");

        if (MinRelayNodes < 3)
            errors.Add("MinRelayNodes must be at least 3");

        if (MaxCircuitRttMs < 100)
            errors.Add("MaxCircuitRttMs must be at least 100");

        if (CircuitHealthCheckIntervalSeconds < 10)
            errors.Add("CircuitHealthCheckIntervalSeconds must be at least 10");

        if (BootstrapTimeoutSeconds < 5)
            errors.Add("BootstrapTimeoutSeconds must be at least 5");

        if (HeartbeatIntervalSeconds < 10)
            errors.Add("HeartbeatIntervalSeconds must be at least 10");

        if (MinConcurrentCircuits < 1)
            errors.Add("MinConcurrentCircuits must be at least 1");

        if (MinRelayReliability < 0.0 || MinRelayReliability > 1.0)
            errors.Add("MinRelayReliability must be between 0.0 and 1.0");

        return errors.Count == 0;
    }
}

