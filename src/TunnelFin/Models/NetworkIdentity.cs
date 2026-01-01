namespace TunnelFin.Models;

/// <summary>
/// Represents a network identity for IPv8 protocol communication.
/// Contains Ed25519 cryptographic keys for signing and encryption.
/// </summary>
public class NetworkIdentity
{
    /// <summary>
    /// Unique identifier for this identity.
    /// </summary>
    public Guid IdentityId { get; set; }

    /// <summary>
    /// User-friendly name for this identity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Ed25519 public key (32 bytes) encoded as base64.
    /// Used for identity verification and signature validation.
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Ed25519 private key seed (32 bytes) encoded as base64.
    /// MUST be kept secret and encrypted at rest.
    /// This is the PyNaCl-compatible seed format (not the expanded 64-byte private key).
    /// </summary>
    public string PrivateKeySeed { get; set; } = string.Empty;

    /// <summary>
    /// IPv8 peer ID derived from the public key.
    /// This is the SHA-1 hash of the public key, used as the peer identifier in the network.
    /// </summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the default identity for the plugin.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Timestamp when this identity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this identity was last used.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Number of circuits created with this identity.
    /// </summary>
    public int CircuitCount { get; set; }

    /// <summary>
    /// Total bytes sent using this identity.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Total bytes received using this identity.
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Whether this identity is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Notes or description for this identity.
    /// </summary>
    public string? Notes { get; set; }
}

