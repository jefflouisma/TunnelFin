using NSec.Cryptography;
using System.Security.Cryptography;

namespace TunnelFin.Networking.Identity;

/// <summary>
/// Ed25519 key pair wrapper using NSec.Cryptography.
/// Provides PyNaCl-compatible key format (32-byte seed) for cross-language compatibility (FR-049).
/// </summary>
public class Ed25519KeyPair : IDisposable
{
    private readonly Key _privateKey;
    private readonly PublicKey _publicKey;
    private bool _disposed;

    /// <summary>
    /// Gets the 32-byte public key (compressed point).
    /// </summary>
    public byte[] PublicKeyBytes => _publicKey.Export(KeyBlobFormat.RawPublicKey);

    /// <summary>
    /// Gets the 32-byte private key seed (PyNaCl to_seed() format).
    /// </summary>
    public byte[] PrivateKeySeedBytes
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Ed25519KeyPair));
            
            return _privateKey.Export(KeyBlobFormat.RawPrivateKey);
        }
    }

    /// <summary>
    /// Generates a new Ed25519 key pair with a random seed.
    /// </summary>
    public Ed25519KeyPair()
    {
        var algorithm = SignatureAlgorithm.Ed25519;
        
        // Create key with export policy to allow plaintext archiving
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
        };
        
        _privateKey = Key.Create(algorithm, creationParameters);
        _publicKey = _privateKey.PublicKey;
    }

    /// <summary>
    /// Creates an Ed25519 key pair from a 32-byte seed (PyNaCl to_seed() format).
    /// </summary>
    /// <param name="seed">32-byte private key seed.</param>
    public Ed25519KeyPair(byte[] seed)
    {
        if (seed == null)
            throw new ArgumentNullException(nameof(seed));
        
        if (seed.Length != 32)
            throw new ArgumentException("Seed must be exactly 32 bytes", nameof(seed));
        
        var algorithm = SignatureAlgorithm.Ed25519;
        
        // Import 32-byte seed using RawPrivateKey format (PyNaCl compatible)
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
        };
        
        _privateKey = Key.Import(algorithm, seed, KeyBlobFormat.RawPrivateKey, creationParameters);
        _publicKey = _privateKey.PublicKey;
    }

    /// <summary>
    /// Signs a message using Ed25519.
    /// Produces a deterministic 64-byte signature (R || S, each 32 bytes, little-endian per RFC 8032).
    /// </summary>
    /// <param name="message">Message to sign.</param>
    /// <returns>64-byte Ed25519 signature.</returns>
    public byte[] Sign(byte[] message)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Ed25519KeyPair));
        
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        var algorithm = SignatureAlgorithm.Ed25519;
        return algorithm.Sign(_privateKey, message);
    }

    /// <summary>
    /// Verifies an Ed25519 signature.
    /// </summary>
    /// <param name="message">Original message.</param>
    /// <param name="signature">64-byte signature to verify.</param>
    /// <returns>True if signature is valid, false otherwise.</returns>
    public bool Verify(byte[] message, byte[] signature)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        if (signature == null)
            throw new ArgumentNullException(nameof(signature));
        
        if (signature.Length != 64)
            throw new ArgumentException("Signature must be exactly 64 bytes", nameof(signature));
        
        var algorithm = SignatureAlgorithm.Ed25519;
        return algorithm.Verify(_publicKey, message, signature);
    }

    /// <summary>
    /// Verifies a signature using a different public key.
    /// </summary>
    /// <param name="publicKey">32-byte public key to verify against.</param>
    /// <param name="message">Original message.</param>
    /// <param name="signature">64-byte signature to verify.</param>
    /// <returns>True if signature is valid, false otherwise.</returns>
    public static bool VerifyWithPublicKey(byte[] publicKey, byte[] message, byte[] signature)
    {
        if (publicKey == null)
            throw new ArgumentNullException(nameof(publicKey));
        
        if (publicKey.Length != 32)
            throw new ArgumentException("Public key must be exactly 32 bytes", nameof(publicKey));
        
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        if (signature == null)
            throw new ArgumentNullException(nameof(signature));
        
        if (signature.Length != 64)
            throw new ArgumentException("Signature must be exactly 64 bytes", nameof(signature));
        
        var algorithm = SignatureAlgorithm.Ed25519;
        var pubKey = PublicKey.Import(algorithm, publicKey, KeyBlobFormat.RawPublicKey);
        
        return algorithm.Verify(pubKey, message, signature);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _privateKey?.Dispose();
            _disposed = true;
        }
    }
}

