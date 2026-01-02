namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Response to an EXTEND message (T039).
/// Contains the relay's ephemeral public key and authentication data.
/// </summary>
public class ExtendResponse
{
    /// <summary>
    /// Circuit identifier.
    /// </summary>
    public uint CircuitId { get; }

    /// <summary>
    /// Request identifier (matches the EXTEND message).
    /// </summary>
    public ushort Identifier { get; }

    /// <summary>
    /// Relay's ephemeral public key for key exchange (32 bytes, Curve25519).
    /// </summary>
    public byte[] EphemeralPublicKey { get; }

    /// <summary>
    /// Authentication data (32 bytes).
    /// </summary>
    public byte[] Auth { get; }

    /// <summary>
    /// Encrypted candidate peers (optional).
    /// </summary>
    public byte[] CandidatesEncrypted { get; }

    /// <summary>
    /// Timestamp when the response was received.
    /// </summary>
    public DateTime ReceivedAt { get; }

    /// <summary>
    /// Creates a new ExtendResponse.
    /// </summary>
    /// <param name="circuitId">Circuit identifier.</param>
    /// <param name="identifier">Request identifier.</param>
    /// <param name="ephemeralPublicKey">Relay's ephemeral public key (32 bytes).</param>
    /// <param name="auth">Authentication data (32 bytes).</param>
    /// <param name="candidatesEncrypted">Encrypted candidate peers.</param>
    public ExtendResponse(
        uint circuitId,
        ushort identifier,
        byte[] ephemeralPublicKey,
        byte[] auth,
        byte[] candidatesEncrypted)
    {
        if (ephemeralPublicKey == null || ephemeralPublicKey.Length != 32)
            throw new ArgumentException("Ephemeral public key must be 32 bytes", nameof(ephemeralPublicKey));
        if (auth == null || auth.Length != 32)
            throw new ArgumentException("Auth must be 32 bytes", nameof(auth));

        CircuitId = circuitId;
        Identifier = identifier;
        EphemeralPublicKey = ephemeralPublicKey;
        Auth = auth;
        CandidatesEncrypted = candidatesEncrypted ?? Array.Empty<byte>();
        ReceivedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a string representation of the response.
    /// </summary>
    public override string ToString()
    {
        return $"ExtendResponse(CircuitId={CircuitId}, Identifier={Identifier}, ReceivedAt={ReceivedAt:O})";
    }
}

