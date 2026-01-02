using TunnelFin.Models;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Represents a multi-hop anonymity circuit through the Tribler network.
/// Manages circuit state, hop tracking, and encryption keys.
/// </summary>
public class Circuit : IDisposable
{
    private readonly List<HopNode> _hops = new();
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this circuit (internal GUID).
    /// </summary>
    public Guid CircuitId { get; }

    /// <summary>
    /// IPv8 protocol circuit ID (4-byte unsigned integer).
    /// </summary>
    public uint IPv8CircuitId { get; }

    /// <summary>
    /// Current state of the circuit.
    /// </summary>
    public CircuitState State { get; private set; }

    /// <summary>
    /// Target number of hops for this circuit (1-3).
    /// </summary>
    public int TargetHopCount { get; }

    /// <summary>
    /// Current number of established hops.
    /// </summary>
    public int CurrentHopCount => _hops.Count;

    /// <summary>
    /// Whether the circuit is fully established (all hops complete).
    /// </summary>
    public bool IsEstablished => State == CircuitState.Established && CurrentHopCount == TargetHopCount;

    /// <summary>
    /// Timestamp when the circuit was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Timestamp of the last activity on this circuit.
    /// </summary>
    public DateTime LastActivityAt { get; private set; }

    /// <summary>
    /// Total bytes sent through this circuit.
    /// </summary>
    public long BytesSent { get; private set; }

    /// <summary>
    /// Total bytes received through this circuit.
    /// </summary>
    public long BytesReceived { get; private set; }

    /// <summary>
    /// Round-trip time (RTT) in milliseconds for the circuit.
    /// </summary>
    public double RoundTripTimeMs { get; private set; }

    /// <summary>
    /// Error message if the circuit failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Timestamp when the circuit will expire.
    /// </summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>
    /// Whether the circuit has expired.
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    /// <summary>
    /// Read-only list of hops in this circuit.
    /// </summary>
    public IReadOnlyList<HopNode> Hops => _hops.AsReadOnly();

    /// <summary>
    /// Creates a new circuit.
    /// </summary>
    /// <param name="ipv8CircuitId">IPv8 protocol circuit ID.</param>
    /// <param name="targetHopCount">Target number of hops (1-3).</param>
    /// <param name="lifetimeSeconds">Circuit lifetime in seconds.</param>
    public Circuit(uint ipv8CircuitId, int targetHopCount, int lifetimeSeconds = 600)
    {
        if (targetHopCount < 1 || targetHopCount > 3)
            throw new ArgumentOutOfRangeException(nameof(targetHopCount), "Hop count must be 1-3");
        if (lifetimeSeconds < 60)
            throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), "Lifetime must be at least 60 seconds");

        CircuitId = Guid.NewGuid();
        IPv8CircuitId = ipv8CircuitId;
        TargetHopCount = targetHopCount;
        State = CircuitState.Creating;
        CreatedAt = DateTime.UtcNow;
        LastActivityAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddSeconds(lifetimeSeconds);
    }

    /// <summary>
    /// Adds a hop to the circuit.
    /// </summary>
    /// <param name="hop">The hop node to add.</param>
    public void AddHop(HopNode hop)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));
        if (hop == null)
            throw new ArgumentNullException(nameof(hop));
        if (CurrentHopCount >= TargetHopCount)
            throw new InvalidOperationException($"Circuit already has {TargetHopCount} hops");
        if (State != CircuitState.Creating)
            throw new InvalidOperationException($"Cannot add hops to circuit in state {State}");

        _hops.Add(hop);
        LastActivityAt = DateTime.UtcNow;

        // If all hops are added and key exchange is complete, mark as established
        if (CurrentHopCount == TargetHopCount && _hops.All(h => h.IsKeyExchangeComplete))
        {
            State = CircuitState.Established;
        }
    }

    /// <summary>
    /// Marks the circuit as established.
    /// </summary>
    public void MarkEstablished()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));
        if (CurrentHopCount != TargetHopCount)
            throw new InvalidOperationException($"Circuit has {CurrentHopCount} hops, expected {TargetHopCount}");

        State = CircuitState.Established;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the circuit as failed with an error message.
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));

        State = CircuitState.Failed;
        ErrorMessage = errorMessage;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records bytes sent through this circuit.
    /// </summary>
    public void RecordBytesSent(long bytes)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be non-negative");

        BytesSent += bytes;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records bytes received through this circuit.
    /// </summary>
    public void RecordBytesReceived(long bytes)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));
        if (bytes < 0)
            throw new ArgumentOutOfRangeException(nameof(bytes), "Bytes must be non-negative");

        BytesReceived += bytes;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records round-trip time for this circuit.
    /// </summary>
    public void RecordRoundTripTime(double rttMs)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));
        if (rttMs < 0)
            throw new ArgumentOutOfRangeException(nameof(rttMs), "RTT must be non-negative");

        RoundTripTimeMs = rttMs;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the expiration time for this circuit.
    /// </summary>
    public void SetExpiration(DateTime expiresAt)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Circuit));

        ExpiresAt = expiresAt;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var hop in _hops)
        {
            hop.Dispose();
        }
        _hops.Clear();

        State = CircuitState.Closed;
        _disposed = true;
    }
}

