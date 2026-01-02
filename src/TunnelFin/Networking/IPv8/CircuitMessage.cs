using System.Buffers.Binary;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// Circuit message serialization and parsing for IPv8 protocol.
/// Handles CREATE/CREATED/EXTEND/EXTENDED messages with big-endian byte order.
/// </summary>
public static class CircuitMessage
{
    /// <summary>
    /// Serializes a CREATE message.
    /// Structure: Circuit ID (4 bytes) + Identifier (2 bytes) + Node public key (2-byte length + data) + Ephemeral key (2-byte length + data)
    /// NOTE: Identifier is 16-bit (ushort) per py-ipv8 format, not 32-bit!
    /// </summary>
    public static byte[] SerializeCreate(uint circuitId, ushort identifier, byte[] nodePublicKey, byte[] ephemeralKey)
    {
        if (nodePublicKey == null || nodePublicKey.Length != 32)
            throw new ArgumentException("Node public key must be 32 bytes", nameof(nodePublicKey));
        if (ephemeralKey == null || ephemeralKey.Length != 32)
            throw new ArgumentException("Ephemeral key must be 32 bytes", nameof(ephemeralKey));

        // Calculate total size: 4 (circuit ID) + 2 (identifier) + 2 + 32 (node key) + 2 + 32 (ephemeral key) = 74 bytes
        var buffer = new byte[4 + 2 + 2 + 32 + 2 + 32];
        var offset = 0;

        // Circuit ID (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), circuitId);
        offset += 4;

        // Identifier (2 bytes, big-endian) - NOTE: 16-bit, not 32-bit!
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), identifier);
        offset += 2;

        // Node public key (2-byte length + 32 bytes)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), 32);
        offset += 2;
        nodePublicKey.CopyTo(buffer, offset);
        offset += 32;

        // Ephemeral key (2-byte length + 32 bytes)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), 32);
        offset += 2;
        ephemeralKey.CopyTo(buffer, offset);

        return buffer;
    }

    /// <summary>
    /// Serializes a CREATED message.
    /// Structure: Circuit ID (4 bytes) + Identifier (2 bytes) + Ephemeral key (2-byte length + data) + Auth (FIXED 32 bytes) + Candidates (remaining bytes)
    /// NOTE: Identifier is 16-bit (ushort) per py-ipv8 format!
    /// NOTE: Auth is FIXED 32 bytes (format: '32s'), candidates_enc is raw bytes at end!
    /// </summary>
    public static byte[] SerializeCreated(uint circuitId, ushort identifier, byte[] ephemeralKey, byte[] auth, byte[] candidatesEnc)
    {
        if (ephemeralKey == null || ephemeralKey.Length != 32)
            throw new ArgumentException("Ephemeral key must be 32 bytes", nameof(ephemeralKey));
        if (auth == null || auth.Length != 32)
            throw new ArgumentException("Auth must be 32 bytes", nameof(auth));
        if (candidatesEnc == null)
            throw new ArgumentNullException(nameof(candidatesEnc));

        var buffer = new byte[4 + 2 + 2 + 32 + 32 + candidatesEnc.Length];
        var offset = 0;

        // Circuit ID (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), circuitId);
        offset += 4;

        // Identifier (2 bytes, big-endian)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), identifier);
        offset += 2;

        // Ephemeral key (2-byte length + 32 bytes)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), 32);
        offset += 2;
        ephemeralKey.CopyTo(buffer, offset);
        offset += 32;

        // Auth (FIXED 32 bytes, no length prefix!)
        auth.CopyTo(buffer, offset);
        offset += 32;

        // Candidates encoded (remaining bytes, no length prefix!)
        candidatesEnc.CopyTo(buffer, offset);

        return buffer;
    }

    /// <summary>
    /// Serializes an EXTEND message.
    /// Structure: Circuit ID (4 bytes) + Node public key (2-byte length + data) + Node address (4 bytes IPv4 + 2 bytes port) + Identifier (2 bytes)
    /// NOTE: Identifier is 16-bit (ushort) per py-ipv8 format!
    /// </summary>
    public static byte[] SerializeExtend(uint circuitId, byte[] nodePublicKey, uint ipv4Address, ushort port, ushort identifier)
    {
        if (nodePublicKey == null || nodePublicKey.Length != 32)
            throw new ArgumentException("Node public key must be 32 bytes", nameof(nodePublicKey));

        var buffer = new byte[4 + 2 + 32 + 4 + 2 + 2];
        var offset = 0;

        // Circuit ID (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), circuitId);
        offset += 4;

        // Node public key (2-byte length + 32 bytes)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), 32);
        offset += 2;
        nodePublicKey.CopyTo(buffer, offset);
        offset += 32;

        // Node address: IPv4 (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), ipv4Address);
        offset += 4;

        // Node address: Port (2 bytes, big-endian)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), port);
        offset += 2;

        // Identifier (2 bytes, big-endian) - NOTE: 16-bit, not 32-bit!
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), identifier);

        return buffer;
    }

    /// <summary>
    /// Serializes an EXTENDED message.
    /// Structure: Circuit ID (4 bytes) + Identifier (2 bytes) + Ephemeral key (2-byte length + data) + Auth (variable) + Candidates (variable)
    /// </summary>
    public static byte[] SerializeExtended(uint circuitId, ushort identifier, byte[] ephemeralKey, byte[] auth, byte[] candidatesEnc)
    {
        // EXTENDED has same structure as CREATED
        return SerializeCreated(circuitId, identifier, ephemeralKey, auth, candidatesEnc);
    }

    /// <summary>
    /// Extracts the circuit ID from a circuit message.
    /// </summary>
    public static uint ExtractCircuitId(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4)
            throw new ArgumentException("Message too short to contain circuit ID", nameof(message));

        return BinaryPrimitives.ReadUInt32BigEndian(message);
    }

    /// <summary>
    /// Parses a CREATE message.
    /// </summary>
    public static (uint circuitId, ushort identifier, byte[] nodePublicKey, byte[] ephemeralKey) ParseCreate(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 2 + 2 + 32 + 2 + 32)
            throw new ArgumentException("Message too short for CREATE format", nameof(message));

        var offset = 0;

        // Circuit ID (4 bytes)
        var circuitId = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Identifier (2 bytes) - NOTE: 16-bit, not 32-bit!
        var identifier = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;

        // Node public key (2-byte length + data)
        var nodeKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (nodeKeyLength != 32)
            throw new ArgumentException($"Invalid node public key length: {nodeKeyLength}, expected 32");
        var nodePublicKey = message.Slice(offset, 32).ToArray();
        offset += 32;

        // Ephemeral key (2-byte length + data)
        var ephemeralKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (ephemeralKeyLength != 32)
            throw new ArgumentException($"Invalid ephemeral key length: {ephemeralKeyLength}, expected 32");
        var ephemeralKey = message.Slice(offset, 32).ToArray();

        return (circuitId, identifier, nodePublicKey, ephemeralKey);
    }

    /// <summary>
    /// Parses a CREATED message.
    /// </summary>
    public static (uint circuitId, ushort identifier, byte[] ephemeralKey, byte[] auth, byte[] candidatesEnc) ParseCreated(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 2 + 2 + 32 + 32)
            throw new ArgumentException("Message too short for CREATED format", nameof(message));

        var offset = 0;

        // Circuit ID (4 bytes)
        var circuitId = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Identifier (2 bytes)
        var identifier = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;

        // Ephemeral key (2-byte length + data)
        var ephemeralKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (ephemeralKeyLength != 32)
            throw new ArgumentException($"Invalid ephemeral key length: {ephemeralKeyLength}, expected 32");
        var ephemeralKey = message.Slice(offset, 32).ToArray();
        offset += 32;

        // Auth (FIXED 32 bytes, no length prefix!)
        var auth = message.Slice(offset, 32).ToArray();
        offset += 32;

        // Candidates encoded (remaining bytes)
        var candidatesEnc = message.Slice(offset).ToArray();

        return (circuitId, identifier, ephemeralKey, auth, candidatesEnc);
    }

    /// <summary>
    /// Parses an EXTEND message.
    /// </summary>
    public static (uint circuitId, byte[] nodePublicKey, uint ipv4Address, ushort port, ushort identifier) ParseExtend(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 2 + 32 + 4 + 2 + 2)
            throw new ArgumentException("Message too short for EXTEND format", nameof(message));

        var offset = 0;

        // Circuit ID (4 bytes)
        var circuitId = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Node public key (2-byte length + data)
        var nodeKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (nodeKeyLength != 32)
            throw new ArgumentException($"Invalid node public key length: {nodeKeyLength}, expected 32");
        var nodePublicKey = message.Slice(offset, 32).ToArray();
        offset += 32;

        // Node address: IPv4 (4 bytes)
        var ipv4Address = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Node address: Port (2 bytes)
        var port = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;

        // Identifier (2 bytes) - NOTE: 16-bit, not 32-bit!
        var identifier = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));

        return (circuitId, nodePublicKey, ipv4Address, port, identifier);
    }

    /// <summary>
    /// Parses an EXTENDED message.
    /// </summary>
    public static (uint circuitId, ushort identifier, byte[] ephemeralKey, byte[] auth, byte[] candidatesEnc) ParseExtended(ReadOnlySpan<byte> message)
    {
        // EXTENDED has same structure as CREATED
        return ParseCreated(message);
    }
}
