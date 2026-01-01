namespace TunnelFin.Models;

/// <summary>
/// Represents a multi-hop anonymity circuit through the Tribler network.
/// Implements onion routing with 1-3 hops for privacy-preserving torrent streaming.
/// </summary>
public class AnonymityCircuit
{
    /// <summary>
    /// Unique identifier for this circuit.
    /// </summary>
    public Guid CircuitId { get; set; }

    /// <summary>
    /// The circuit ID assigned by the IPv8 protocol (4-byte unsigned integer).
    /// </summary>
    public uint IPv8CircuitId { get; set; }

    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState State { get; set; }

    /// <summary>
    /// Number of hops in this circuit (1-3).
    /// </summary>
    public int HopCount { get; set; }

    /// <summary>
    /// List of relay node public keys in the circuit path (in order from entry to exit).
    /// Each key is a 32-byte Ed25519 public key encoded as base64.
    /// </summary>
    public List<string> RelayNodes { get; set; } = new();

    /// <summary>
    /// Timestamp when the circuit was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last activity on this circuit.
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// Total bytes sent through this circuit.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Total bytes received through this circuit.
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Round-trip time (RTT) in milliseconds for the circuit.
    /// Measured during circuit establishment.
    /// </summary>
    public double RoundTripTimeMs { get; set; }

    /// <summary>
    /// Whether this circuit is currently in use by an active stream.
    /// </summary>
    public bool IsInUse { get; set; }

    /// <summary>
    /// The stream ID using this circuit (if any).
    /// </summary>
    public Guid? StreamId { get; set; }

    /// <summary>
    /// Error message if the circuit failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the circuit will expire (circuits have a limited lifetime).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Whether the circuit has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Whether the circuit is ready for use.
    /// </summary>
    public bool IsReady => State == CircuitState.Established && !IsExpired;
}

