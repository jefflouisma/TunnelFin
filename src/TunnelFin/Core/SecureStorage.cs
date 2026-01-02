using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TunnelFin.Core;

/// <summary>
/// SecureStorage provides encrypted storage for sensitive data (T092).
/// Implements FR-038: Secure key storage for Ed25519 private keys.
/// Uses Jellyfin's encrypted configuration storage.
/// </summary>
public class SecureStorage
{
    private readonly string _storagePath;
    private readonly byte[] _encryptionKey;

    /// <summary>
    /// Initializes secure storage with encryption key.
    /// </summary>
    /// <param name="storagePath">Path to encrypted storage file</param>
    /// <param name="encryptionKey">32-byte encryption key (derived from Jellyfin instance ID)</param>
    public SecureStorage(string storagePath, byte[] encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be empty", nameof(storagePath));
        
        if (encryptionKey == null || encryptionKey.Length != 32)
            throw new ArgumentException("Encryption key must be 32 bytes", nameof(encryptionKey));

        _storagePath = storagePath;
        _encryptionKey = encryptionKey;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Stores Ed25519 private key securely (FR-038).
    /// </summary>
    /// <param name="privateKey">32-byte Ed25519 private key</param>
    public void StorePrivateKey(byte[] privateKey)
    {
        if (privateKey == null || privateKey.Length != 32)
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));

        var data = new Dictionary<string, string>
        {
            { "ed25519_private_key", Convert.ToBase64String(privateKey) },
            { "created_at", DateTime.UtcNow.ToString("O") }
        };

        var json = JsonSerializer.Serialize(data);
        var encrypted = Encrypt(Encoding.UTF8.GetBytes(json));
        File.WriteAllBytes(_storagePath, encrypted);
    }

    /// <summary>
    /// Retrieves Ed25519 private key from secure storage (FR-038).
    /// </summary>
    /// <returns>32-byte Ed25519 private key, or null if not found</returns>
    public byte[]? RetrievePrivateKey()
    {
        if (!File.Exists(_storagePath))
            return null;

        try
        {
            var encrypted = File.ReadAllBytes(_storagePath);
            var decrypted = Decrypt(encrypted);
            var json = Encoding.UTF8.GetString(decrypted);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (data != null && data.TryGetValue("ed25519_private_key", out var base64Key))
            {
                return Convert.FromBase64String(base64Key);
            }

            return null;
        }
        catch (CryptographicException)
        {
            // Decryption failed - possibly corrupted or wrong key
            return null;
        }
    }

    /// <summary>
    /// Deletes stored private key.
    /// </summary>
    public void DeletePrivateKey()
    {
        if (File.Exists(_storagePath))
        {
            File.Delete(_storagePath);
        }
    }

    /// <summary>
    /// Checks if a private key is stored.
    /// </summary>
    public bool HasPrivateKey()
    {
        return File.Exists(_storagePath);
    }

    /// <summary>
    /// Encrypts data using AES-256-GCM.
    /// </summary>
    private byte[] Encrypt(byte[] plaintext)
    {
        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Format: nonce (12 bytes) + tag (16 bytes) + ciphertext
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

        return result;
    }

    /// <summary>
    /// Decrypts data using AES-256-GCM.
    /// </summary>
    private byte[] Decrypt(byte[] encrypted)
    {
        if (encrypted.Length < AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize)
            throw new CryptographicException("Invalid encrypted data");

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[encrypted.Length - nonce.Length - tag.Length];

        Buffer.BlockCopy(encrypted, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(encrypted, nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(encrypted, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}

