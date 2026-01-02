using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Provides layered encryption/decryption for onion routing through circuit hops.
/// Implements per-hop encryption using shared secrets from key exchange.
/// </summary>
public static class LayeredEncryption
{
    /// <summary>
    /// Encrypts data for transmission through a circuit using layered encryption.
    /// Applies encryption in reverse hop order (exit → entry) for onion routing.
    /// </summary>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <param name="circuit">The circuit containing hop encryption keys.</param>
    /// <returns>Encrypted data with layers for each hop.</returns>
    public static byte[] EncryptLayers(byte[] plaintext, Circuit circuit)
    {
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));
        if (circuit.State != CircuitState.Established)
            throw new InvalidOperationException($"Circuit must be established, current state: {circuit.State}");

        var encrypted = plaintext;

        // Encrypt in reverse order: exit hop first, entry hop last
        // This ensures the entry node can decrypt the outer layer and forward to the next hop
        for (int i = circuit.Hops.Count - 1; i >= 0; i--)
        {
            var hop = circuit.Hops[i];
            encrypted = hop.Encrypt(encrypted);
        }

        return encrypted;
    }

    /// <summary>
    /// Decrypts data received from a circuit using layered decryption.
    /// Applies decryption in forward hop order (entry → exit) for onion routing.
    /// </summary>
    /// <param name="ciphertext">Encrypted data to decrypt.</param>
    /// <param name="circuit">The circuit containing hop decryption keys.</param>
    /// <returns>Decrypted plaintext data.</returns>
    public static byte[] DecryptLayers(byte[] ciphertext, Circuit circuit)
    {
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));
        if (circuit.State != CircuitState.Established)
            throw new InvalidOperationException($"Circuit must be established, current state: {circuit.State}");

        var decrypted = ciphertext;

        // Decrypt in forward order: entry hop first, exit hop last
        // This peels off each layer of encryption as data travels through the circuit
        for (int i = 0; i < circuit.Hops.Count; i++)
        {
            var hop = circuit.Hops[i];
            decrypted = hop.Decrypt(decrypted);
        }

        return decrypted;
    }

    /// <summary>
    /// Encrypts data for a specific hop in the circuit.
    /// Used for hop-by-hop encryption when data is already partially encrypted.
    /// </summary>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <param name="circuit">The circuit containing hop encryption keys.</param>
    /// <param name="hopIndex">The hop index to encrypt for (0-based).</param>
    /// <returns>Encrypted data for the specified hop.</returns>
    public static byte[] EncryptForHop(byte[] plaintext, Circuit circuit, int hopIndex)
    {
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));
        if (hopIndex < 0 || hopIndex >= circuit.Hops.Count)
            throw new ArgumentOutOfRangeException(nameof(hopIndex), $"Hop index must be 0-{circuit.Hops.Count - 1}");

        var hop = circuit.Hops[hopIndex];
        return hop.Encrypt(plaintext);
    }

    /// <summary>
    /// Decrypts data from a specific hop in the circuit.
    /// Used for hop-by-hop decryption when data is partially encrypted.
    /// </summary>
    /// <param name="ciphertext">Encrypted data to decrypt.</param>
    /// <param name="circuit">The circuit containing hop decryption keys.</param>
    /// <param name="hopIndex">The hop index to decrypt from (0-based).</param>
    /// <returns>Decrypted data from the specified hop.</returns>
    public static byte[] DecryptFromHop(byte[] ciphertext, Circuit circuit, int hopIndex)
    {
        if (ciphertext == null)
            throw new ArgumentNullException(nameof(ciphertext));
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));
        if (hopIndex < 0 || hopIndex >= circuit.Hops.Count)
            throw new ArgumentOutOfRangeException(nameof(hopIndex), $"Hop index must be 0-{circuit.Hops.Count - 1}");

        var hop = circuit.Hops[hopIndex];
        return hop.Decrypt(ciphertext);
    }
}

