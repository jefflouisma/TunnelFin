using NSec.Cryptography;
using System.Text;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Represents a single hop (relay node) in an anonymity circuit.
/// Manages peer information, shared secrets, and encryption/decryption for this hop.
/// </summary>
public class HopNode : IDisposable
{
    private SharedSecret? _sharedSecret;
    private bool _disposed;
    private Key? _encryptionKey;
    private ulong _encryptionNonce = 0;
    private ulong _decryptionNonce = 0;
    private readonly object _nonceLock = new();

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
        // The shared secret is used to derive encryption keys via HKDF in EnsureEncryptionKey()
        // Encryption/decryption uses ChaCha20-Poly1305 AEAD cipher
    }

    /// <summary>
    /// Encrypts data for transmission through this hop.
    /// Uses ChaCha20-Poly1305 AEAD cipher with the shared secret.
    /// </summary>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <returns>Encrypted data with authentication tag.</returns>
    public byte[] Encrypt(byte[] plaintext)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HopNode));
        if (!IsKeyExchangeComplete)
            throw new InvalidOperationException("Key exchange must be completed before encryption");
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));

        // Ensure encryption key is derived
        EnsureEncryptionKey();

        // Get next nonce (counter-based to avoid reuse)
        ulong nonce;
        lock (_nonceLock)
        {
            nonce = _encryptionNonce++;
        }

        // Create nonce buffer (12 bytes for ChaCha20-Poly1305)
        var nonceBytes = new byte[12];
        BitConverter.GetBytes(nonce).CopyTo(nonceBytes, 0);

        // Encrypt using ChaCha20-Poly1305
        var algorithm = AeadAlgorithm.ChaCha20Poly1305;
        var ciphertext = algorithm.Encrypt(_encryptionKey!, nonceBytes, null, plaintext);

        // Prepend nonce to ciphertext for transmission
        var result = new byte[nonceBytes.Length + ciphertext.Length];
        nonceBytes.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonceBytes.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data received from this hop.
    /// Uses ChaCha20-Poly1305 AEAD cipher with the shared secret.
    /// </summary>
    /// <param name="ciphertext">Data to decrypt (includes nonce prefix).</param>
    /// <returns>Decrypted data.</returns>
    public byte[] Decrypt(byte[] ciphertext)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HopNode));
        if (!IsKeyExchangeComplete)
            throw new InvalidOperationException("Key exchange must be completed before decryption");
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));

        // Ensure encryption key is derived
        EnsureEncryptionKey();

        // Extract nonce from ciphertext (first 12 bytes)
        if (ciphertext.Length < 12)
            throw new ArgumentException("Ciphertext too short to contain nonce", nameof(ciphertext));

        var nonceBytes = new byte[12];
        Array.Copy(ciphertext, 0, nonceBytes, 0, 12);

        // Extract actual ciphertext (remaining bytes)
        var actualCiphertext = new byte[ciphertext.Length - 12];
        Array.Copy(ciphertext, 12, actualCiphertext, 0, actualCiphertext.Length);

        // Decrypt using ChaCha20-Poly1305
        var algorithm = AeadAlgorithm.ChaCha20Poly1305;
        var plaintext = algorithm.Decrypt(_encryptionKey!, nonceBytes, null, actualCiphertext);

        return plaintext;
    }

    /// <summary>
    /// Derives encryption key from shared secret using HKDF.
    /// </summary>
    private void EnsureEncryptionKey()
    {
        if (_encryptionKey != null)
            return;

        if (_sharedSecret == null)
            throw new InvalidOperationException("Shared secret not available");

        // Derive encryption key using HKDF-SHA256
        var kdf = KeyDerivationAlgorithm.HkdfSha256;
        var context = Encoding.UTF8.GetBytes($"hop-encryption-{HopIndex}");
        _encryptionKey = kdf.DeriveKey(
            _sharedSecret,
            null,
            context,
            AeadAlgorithm.ChaCha20Poly1305);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _sharedSecret?.Dispose();
        _encryptionKey?.Dispose();
        _disposed = true;
    }
}

