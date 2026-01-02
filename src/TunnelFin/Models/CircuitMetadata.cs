namespace TunnelFin.Models;

/// <summary>
/// Represents metadata for a Tribler circuit used for anonymous peer connections.
/// Lifecycle: Created when circuit is established, deleted when circuit fails or is idle.
/// Storage: In-memory dictionary keyed by CircuitId.
/// </summary>
public class CircuitMetadata
{
    /// <summary>
    /// Unique identifier for this circuit.
    /// </summary>
    public Guid CircuitId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// List of peer IDs forming the circuit path (hop nodes).
    /// </summary>
    public required List<string> HopNodes { get; init; }

    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState State { get; set; } = CircuitState.Creating;

    /// <summary>
    /// Whether the circuit is healthy and ready for connections.
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Average round-trip latency in milliseconds.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Number of active connections using this circuit.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Timestamp when circuit was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of last health check.
    /// </summary>
    public DateTime LastHealthCheckAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total bytes transferred through this circuit.
    /// </summary>
    public long BytesTransferred { get; set; }

    /// <summary>
    /// Validates the circuit metadata according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (HopNodes == null || HopNodes.Count == 0)
            throw new ArgumentException("HopNodes must not be empty", nameof(HopNodes));

        if (AverageLatency < 0)
            throw new ArgumentException("AverageLatency must be non-negative", nameof(AverageLatency));

        if (ActiveConnections < 0)
            throw new ArgumentException("ActiveConnections must be non-negative", nameof(ActiveConnections));

        if (BytesTransferred < 0)
            throw new ArgumentException("BytesTransferred must be non-negative", nameof(BytesTransferred));
    }
}

