namespace TunnelFin.Networking.IPv8;

/// <summary>
/// Represents a peer in the IPv8 network.
/// Stores peer information, connection state, and handshake status.
/// </summary>
public class Peer
{
    /// <summary>
    /// Public key of the peer (32 bytes, Ed25519).
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// IPv4 address of the peer (big-endian uint32).
    /// </summary>
    public uint IPv4Address { get; set; }

    /// <summary>
    /// Port number of the peer (big-endian ushort).
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// Whether the handshake with this peer has been completed.
    /// </summary>
    public bool IsHandshakeComplete { get; set; }

    /// <summary>
    /// Timestamp when this peer was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; }

    /// <summary>
    /// Timestamp of the last communication with this peer.
    /// </summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// Round-trip time (RTT) to this peer in milliseconds.
    /// </summary>
    public double RttMs { get; set; }

    /// <summary>
    /// Whether this peer is suitable for use as a relay node.
    /// </summary>
    public bool IsRelayCandidate { get; set; }

    /// <summary>
    /// Estimated bandwidth of this peer in bytes/second.
    /// </summary>
    public long EstimatedBandwidth { get; set; }

    /// <summary>
    /// Number of successful interactions with this peer.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed interactions with this peer.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Peer reliability score (0.0 - 1.0).
    /// </summary>
    public double ReliabilityScore => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount)
        : 0.5;

    /// <summary>
    /// RTT variance in milliseconds (for jitter calculation).
    /// </summary>
    public double RttVariance { get; set; }

    /// <summary>
    /// Protocol version supported by this peer.
    /// </summary>
    public ProtocolVersion? ProtocolVersion { get; set; }

    /// <summary>
    /// NAT type of this peer (if known).
    /// </summary>
    public NatType NatType { get; set; } = NatType.Unknown;

    /// <summary>
    /// Creates a new peer.
    /// </summary>
    /// <param name="publicKey">Public key of the peer (32 bytes).</param>
    /// <param name="ipv4Address">IPv4 address (big-endian uint32).</param>
    /// <param name="port">Port number (big-endian ushort).</param>
    public Peer(byte[] publicKey, uint ipv4Address, ushort port)
    {
        if (publicKey == null)
            throw new ArgumentNullException(nameof(publicKey));
        if (publicKey.Length != 32)
            throw new ArgumentException("Public key must be exactly 32 bytes", nameof(publicKey));

        PublicKey = publicKey;
        IPv4Address = ipv4Address;
        Port = port;
        DiscoveredAt = DateTime.UtcNow;
        LastSeenAt = DateTime.UtcNow;
        IsRelayCandidate = true; // Assume candidate until proven otherwise
    }

    /// <summary>
    /// Updates the last seen timestamp.
    /// </summary>
    public void UpdateLastSeen()
    {
        LastSeenAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a successful interaction with this peer.
    /// </summary>
    public void RecordSuccess()
    {
        SuccessCount++;
        UpdateLastSeen();
    }

    /// <summary>
    /// Records a failed interaction with this peer.
    /// </summary>
    public void RecordFailure()
    {
        FailureCount++;
        UpdateLastSeen();
    }

    /// <summary>
    /// Gets a unique identifier for this peer based on public key.
    /// </summary>
    public string GetPeerId()
    {
        return Convert.ToBase64String(PublicKey);
    }

    /// <summary>
    /// Gets the socket address as a string (for logging).
    /// </summary>
    public string GetSocketAddress()
    {
        // Convert big-endian uint32 to IPv4 string
        var bytes = BitConverter.GetBytes(IPv4Address);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        var ip = new System.Net.IPAddress(bytes);
        return $"{ip}:{Port}";
    }

    public override string ToString()
    {
        return $"Peer({GetSocketAddress()}, RTT={RttMs:F1}ms, Reliability={ReliabilityScore:P0})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Peer other)
            return false;
        return PublicKey.SequenceEqual(other.PublicKey);
    }

    public override int GetHashCode()
    {
        return BitConverter.ToInt32(PublicKey, 0);
    }
}

