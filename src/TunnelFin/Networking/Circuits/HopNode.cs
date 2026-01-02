using NSec.Cryptography;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Represents a single hop (relay node) in an anonymity circuit.
/// Manages peer information, shared secrets, and encryption/decryption for this hop.
/// </summary>
public class HopNode : IDisposable
{
    private SharedSecret? _sharedSecret;
    private bool _disposed;

    /// <summary>
    /// Public key of the relay peer (32 bytes, Ed25519).
    /// </summary>
    public byte[] PublicKey { get; }

    /// <summary>
    /// IPv4 address of the relay peer (big-endian uint32).
    /// </summary>
    public uint IPv4Address { get; }

    /// <summary>
    /// Port number of the relay peer (big-endian ushort).
    /// </summary>
    public ushort Port { get; }

    /// <summary>
    /// Ephemeral public key used for this hop's key exchange (32 bytes, Curve25519).
    /// </summary>
    public byte[]? EphemeralPublicKey { get; private set; }

    /// <summary>
    /// Whether the key exchange has been completed for this hop.
    /// </summary>
    public bool IsKeyExchangeComplete => _sharedSecret != null;

    /// <summary>
    /// Hop index in the circuit (0-based, 0 = entry node, 2 = exit node for 3-hop circuit).
    /// </summary>
    public int HopIndex { get; }

    /// <summary>
    /// Timestamp when this hop was added to the circuit.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Creates a new hop node.
    /// </summary>
    /// <param name="publicKey">Public key of the relay peer (32 bytes).</param>
    /// <param name="ipv4Address">IPv4 address (big-endian uint32).</param>
    /// <param name="port">Port number (big-endian ushort).</param>
    /// <param name="hopIndex">Hop index in the circuit (0-based).</param>
    public HopNode(byte[] publicKey, uint ipv4Address, ushort port, int hopIndex)
    {
        if (publicKey == null)
            throw new ArgumentNullException(nameof(publicKey));
        if (publicKey.Length != 32)
            throw new ArgumentException("Public key must be exactly 32 bytes", nameof(publicKey));
        if (hopIndex < 0 || hopIndex > 2)
            throw new ArgumentOutOfRangeException(nameof(hopIndex), "Hop index must be 0-2");

        PublicKey = publicKey;
        IPv4Address = ipv4Address;
        Port = port;
        HopIndex = hopIndex;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Completes the key exchange for this hop using the received ephemeral public key.
    /// </summary>
    /// <param name="ephemeralPublicKey">Ephemeral public key from the relay (32 bytes, Curve25519).</param>
    /// <param name="ourEphemeralPrivateKey">Our ephemeral private key for DH exchange.</param>
    public void CompleteKeyExchange(byte[] ephemeralPublicKey, Key ourEphemeralPrivateKey)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HopNode));
        if (ephemeralPublicKey == null)
            throw new ArgumentNullException(nameof(ephemeralPublicKey));
        if (ephemeralPublicKey.Length != 32)
            throw new ArgumentException("Ephemeral public key must be exactly 32 bytes", nameof(ephemeralPublicKey));
        if (ourEphemeralPrivateKey == null)
            throw new ArgumentNullException(nameof(ourEphemeralPrivateKey));

        EphemeralPublicKey = ephemeralPublicKey;

        // Perform X25519 key exchange
        var algorithm = KeyAgreementAlgorithm.X25519;
        var theirPublicKey = NSec.Cryptography.PublicKey.Import(algorithm, ephemeralPublicKey, KeyBlobFormat.RawPublicKey);

        // Derive shared secret using X25519
        _sharedSecret = algorithm.Agree(ourEphemeralPrivateKey, theirPublicKey);

        // Store shared secret for encryption/decryption
        // Note: In production, this would be used with a symmetric cipher (e.g., ChaCha20-Poly1305)
        // For now, we just store the raw shared secret (placeholder)
    }

    /// <summary>
    /// Encrypts data for transmission through this hop.
    /// Uses the shared secret derived from key exchange.
    /// </summary>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <returns>Encrypted data.</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HopNode));
        if (!IsKeyExchangeComplete)
            throw new InvalidOperationException("Key exchange must be completed before encryption");
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));

        // TODO: Implement actual encryption using shared secret
        // For now, return plaintext (placeholder for ChaCha20-Poly1305 or AES-GCM)
        return plaintext;
    }

    /// <summary>
    /// Decrypts data received from this hop.
    /// Uses the shared secret derived from key exchange.
    /// </summary>
    /// <param name="ciphertext">Data to decrypt.</param>
    /// <returns>Decrypted data.</returns>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HopNode));
        if (!IsKeyExchangeComplete)
            throw new InvalidOperationException("Key exchange must be completed before decryption");
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));

        // TODO: Implement actual decryption using shared secret
        // For now, return ciphertext (placeholder for ChaCha20-Poly1305 or AES-GCM)
        return ciphertext;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _sharedSecret?.Dispose();
        _disposed = true;
    }
}

