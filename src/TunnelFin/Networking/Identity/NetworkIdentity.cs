using System.Security.Cryptography;

namespace TunnelFin.Networking.Identity;

/// <summary>
/// Represents a network identity for IPv8 protocol (FR-004, FR-049).
/// Wraps Ed25519KeyPair and provides peer ID derivation.
/// </summary>
public class NetworkIdentity
{
    private readonly Ed25519KeyPair _keyPair;
    private readonly string _peerId;

    /// <summary>
    /// Gets the Ed25519 public key (32 bytes).
    /// </summary>
    public byte[] PublicKey => _keyPair.PublicKeyBytes;

    /// <summary>
    /// Gets the peer ID (SHA-1 hash of public key as hex string).
    /// </summary>
    public string PeerId => _peerId;

    /// <summary>
    /// Initializes a new instance of the NetworkIdentity class with a new random keypair.
    /// </summary>
    public NetworkIdentity()
    {
        _keyPair = new Ed25519KeyPair();
        _peerId = DerivePeerId(_keyPair.PublicKeyBytes);
    }

    /// <summary>
    /// Initializes a new instance of the NetworkIdentity class from a seed.
    /// </summary>
    /// <param name="seed">32-byte seed for deterministic key generation.</param>
    public NetworkIdentity(byte[] seed)
    {
        _keyPair = new Ed25519KeyPair(seed);
        _peerId = DerivePeerId(_keyPair.PublicKeyBytes);
    }

    /// <summary>
    /// Signs a message using the Ed25519 private key.
    /// </summary>
    /// <param name="message">Message to sign.</param>
    /// <returns>64-byte Ed25519 signature.</returns>
    public byte[] Sign(byte[] message)
    {
        return _keyPair.Sign(message);
    }

    /// <summary>
    /// Verifies a signature using the Ed25519 public key.
    /// </summary>
    /// <param name="message">Original message.</param>
    /// <param name="signature">64-byte Ed25519 signature.</param>
    /// <returns>True if signature is valid.</returns>
    public bool Verify(byte[] message, byte[] signature)
    {
        return _keyPair.Verify(message, signature);
    }

    /// <summary>
    /// Gets the underlying Ed25519KeyPair.
    /// </summary>
    /// <returns>Ed25519KeyPair instance.</returns>
    public Ed25519KeyPair GetKeyPair()
    {
        return _keyPair;
    }

    /// <summary>
    /// Returns the peer ID as a string.
    /// </summary>
    /// <returns>Peer ID (SHA-1 hash of public key as hex string).</returns>
    public override string ToString()
    {
        return _peerId;
    }

    /// <summary>
    /// Derives a peer ID from a public key using SHA-1 hash.
    /// </summary>
    /// <param name="publicKey">32-byte Ed25519 public key.</param>
    /// <returns>Peer ID as 40-character hex string.</returns>
    private static string DerivePeerId(byte[] publicKey)
    {
        // Peer ID is SHA-1 hash of public key (20 bytes) as hex string (40 chars)
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(publicKey);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

