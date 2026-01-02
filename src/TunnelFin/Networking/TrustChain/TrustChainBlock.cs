namespace TunnelFin.Networking.TrustChain;

/// <summary>
/// Represents a TrustChain block for bandwidth accounting (FR-050).
/// Used to track proportional relay contributions in the IPv8 network.
/// </summary>
public class TrustChainBlock
{
    /// <summary>
    /// Gets or sets the creator's Ed25519 public key (32 bytes).
    /// </summary>
    public byte[] CreatorPublicKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the link (counterparty) Ed25519 public key (32 bytes).
    /// </summary>
    public byte[] LinkPublicKey { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the sequence number (monotonically increasing).
    /// </summary>
    public uint SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the SHA-3 hash of the previous block (32 bytes).
    /// All zeros for genesis block.
    /// </summary>
    public byte[] PreviousHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the timestamp in milliseconds since Unix epoch.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the message content (variable length).
    /// Contains bandwidth accounting data.
    /// </summary>
    public byte[] Message { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Gets or sets the Ed25519 signature (64 bytes).
    /// Computed over fields 1-7 in exact byte order.
    /// </summary>
    public byte[] Signature { get; set; } = Array.Empty<byte>();
}

