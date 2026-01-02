namespace TunnelFin.Networking.Transport;

/// <summary>
/// Transport-specific configuration settings (T061).
/// Controls UDP transport behavior, retry policies, and network timeouts.
/// </summary>
public class TransportConfiguration
{
    /// <summary>
    /// UDP port to bind (0 = random available port, per py-ipv8 behavior).
    /// </summary>
    public ushort Port { get; set; } = 0;

    /// <summary>
    /// Maximum transmission unit (MTU) for IPv4 UDP packets.
    /// Default: 1472 bytes (1500 - 20 IP header - 8 UDP header).
    /// </summary>
    public int Mtu { get; set; } = 1472;

    /// <summary>
    /// Initial retry delay in milliseconds.
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum retry delay in milliseconds.
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Jitter factor for retry delays (0.0-1.0).
    /// Default: 0.25 (25% jitter).
    /// </summary>
    public double JitterFactor { get; set; } = 0.25;

    /// <summary>
    /// Socket receive buffer size in bytes.
    /// Default: 1MB.
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Socket send buffer size in bytes.
    /// Default: 1MB.
    /// </summary>
    public int SendBufferSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Whether to enable socket reuse (SO_REUSEADDR).
    /// </summary>
    public bool EnableSocketReuse { get; set; } = true;

    /// <summary>
    /// Network operation timeout in seconds.
    /// </summary>
    public int NetworkTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Bootstrap discovery timeout in seconds.
    /// </summary>
    public int BootstrapTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Peer refresh interval in seconds.
    /// How often to refresh the peer table from bootstrap nodes.
    /// </summary>
    public int PeerRefreshIntervalSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Circuit heartbeat interval in seconds.
    /// How often to send keepalive messages to maintain circuits.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Circuit response timeout in seconds.
    /// Maximum time to wait for CREATE/EXTEND responses.
    /// </summary>
    public int CircuitResponseTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Validates the transport configuration.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();

        if (Mtu < 512 || Mtu > 65507)
            errors.Add("MTU must be between 512 and 65507 bytes");

        if (InitialRetryDelayMs < 10)
            errors.Add("InitialRetryDelayMs must be at least 10");

        if (MaxRetryDelayMs < InitialRetryDelayMs)
            errors.Add("MaxRetryDelayMs must be >= InitialRetryDelayMs");

        if (MaxRetries < 0)
            errors.Add("MaxRetries must be non-negative");

        if (JitterFactor < 0.0 || JitterFactor > 1.0)
            errors.Add("JitterFactor must be between 0.0 and 1.0");

        if (ReceiveBufferSize < 8192)
            errors.Add("ReceiveBufferSize must be at least 8192 bytes");

        if (SendBufferSize < 8192)
            errors.Add("SendBufferSize must be at least 8192 bytes");

        if (NetworkTimeoutSeconds < 1)
            errors.Add("NetworkTimeoutSeconds must be at least 1");

        if (BootstrapTimeoutSeconds < 5)
            errors.Add("BootstrapTimeoutSeconds must be at least 5");

        if (PeerRefreshIntervalSeconds < 60)
            errors.Add("PeerRefreshIntervalSeconds must be at least 60");

        if (HeartbeatIntervalSeconds < 10)
            errors.Add("HeartbeatIntervalSeconds must be at least 10");

        if (CircuitResponseTimeoutSeconds < 1)
            errors.Add("CircuitResponseTimeoutSeconds must be at least 1");

        return errors.Count == 0;
    }
}

