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
    /// Structure: Circuit ID (4 bytes) + Identifier (4 bytes) + Node public key (2-byte length + data) + Ephemeral key (2-byte length + data)
    /// </summary>
    public static byte[] SerializeCreate(uint circuitId, uint identifier, byte[] nodePublicKey, byte[] ephemeralKey)
    {
        if (nodePublicKey == null || nodePublicKey.Length != 32)
            throw new ArgumentException("Node public key must be 32 bytes", nameof(nodePublicKey));
        if (ephemeralKey == null || ephemeralKey.Length != 32)
            throw new ArgumentException("Ephemeral key must be 32 bytes", nameof(ephemeralKey));

        // Calculate total size: 4 (circuit ID) + 4 (identifier) + 2 + 32 (node key) + 2 + 32 (ephemeral key)
        var buffer = new byte[4 + 4 + 2 + 32 + 2 + 32];
        var offset = 0;

        // Circuit ID (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), circuitId);
        offset += 4;

        // Identifier (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), identifier);
        offset += 4;

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
    /// Structure: Circuit ID (4 bytes) + Ephemeral key (2-byte length + data) + Auth (2-byte length + data) + Candidate list (2-byte length + data)
    /// </summary>
    public static byte[] SerializeCreated(uint circuitId, byte[] ephemeralKey, byte[] auth, byte[] candidateList)
    {
        if (ephemeralKey == null || ephemeralKey.Length != 32)
            throw new ArgumentException("Ephemeral key must be 32 bytes", nameof(ephemeralKey));
        if (auth == null)
            throw new ArgumentNullException(nameof(auth));
        if (candidateList == null)
            throw new ArgumentNullException(nameof(candidateList));

        var buffer = new byte[4 + 2 + 32 + 2 + auth.Length + 2 + candidateList.Length];
        var offset = 0;

        // Circuit ID (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), circuitId);
        offset += 4;

        // Ephemeral key (2-byte length + 32 bytes)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), 32);
        offset += 2;
        ephemeralKey.CopyTo(buffer, offset);
        offset += 32;

        // Auth (2-byte length + data)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)auth.Length);
        offset += 2;
        auth.CopyTo(buffer, offset);
        offset += auth.Length;

        // Candidate list (2-byte length + data)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)candidateList.Length);
        offset += 2;
        candidateList.CopyTo(buffer, offset);

        return buffer;
    }

    /// <summary>
    /// Serializes an EXTEND message.
    /// Structure: Circuit ID (4 bytes) + Node public key (2-byte length + data) + Node address (4 bytes IPv4 + 2 bytes port) + Identifier (4 bytes)
    /// </summary>
    public static byte[] SerializeExtend(uint circuitId, byte[] nodePublicKey, uint ipv4Address, ushort port, uint identifier)
    {
        if (nodePublicKey == null || nodePublicKey.Length != 32)
            throw new ArgumentException("Node public key must be 32 bytes", nameof(nodePublicKey));

        var buffer = new byte[4 + 2 + 32 + 4 + 2 + 4];
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

        // Identifier (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), identifier);

        return buffer;
    }

    /// <summary>
    /// Serializes an EXTENDED message.
    /// Structure: Circuit ID (4 bytes) + Ephemeral key (2-byte length + data) + Auth (2-byte length + data) + Candidate list (2-byte length + data)
    /// </summary>
    public static byte[] SerializeExtended(uint circuitId, byte[] ephemeralKey, byte[] auth, byte[] candidateList)
    {
        // EXTENDED has same structure as CREATED
        return SerializeCreated(circuitId, ephemeralKey, auth, candidateList);
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
    public static (uint circuitId, uint identifier, byte[] nodePublicKey, byte[] ephemeralKey) ParseCreate(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 4 + 2 + 32 + 2 + 32)
            throw new ArgumentException("Message too short for CREATE format", nameof(message));

        var offset = 0;

        // Circuit ID (4 bytes)
        var circuitId = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Identifier (4 bytes)
        var identifier = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

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
    public static (uint circuitId, byte[] ephemeralKey, byte[] auth, byte[] candidateList) ParseCreated(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 2 + 32)
            throw new ArgumentException("Message too short for CREATED format", nameof(message));

        var offset = 0;

        // Circuit ID (4 bytes)
        var circuitId = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));
        offset += 4;

        // Ephemeral key (2-byte length + data)
        var ephemeralKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (ephemeralKeyLength != 32)
            throw new ArgumentException($"Invalid ephemeral key length: {ephemeralKeyLength}, expected 32");
        var ephemeralKey = message.Slice(offset, 32).ToArray();
        offset += 32;

        // Auth (2-byte length + data)
        if (offset + 2 > message.Length)
            throw new ArgumentException("Message too short for auth field");
        var authLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (offset + authLength > message.Length)
            throw new ArgumentException($"Message too short for auth data: expected {authLength} bytes");
        var auth = message.Slice(offset, authLength).ToArray();
        offset += authLength;

        // Candidate list (2-byte length + data)
        if (offset + 2 > message.Length)
            throw new ArgumentException("Message too short for candidate list field");
        var candidateListLength = BinaryPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        offset += 2;
        if (offset + candidateListLength > message.Length)
            throw new ArgumentException($"Message too short for candidate list: expected {candidateListLength} bytes");
        var candidateList = message.Slice(offset, candidateListLength).ToArray();

        return (circuitId, ephemeralKey, auth, candidateList);
    }

    /// <summary>
    /// Parses an EXTEND message.
    /// </summary>
    public static (uint circuitId, byte[] nodePublicKey, uint ipv4Address, ushort port, uint identifier) ParseExtend(ReadOnlySpan<byte> message)
    {
        if (message.Length < 4 + 2 + 32 + 4 + 2 + 4)
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

        // Identifier (4 bytes)
        var identifier = BinaryPrimitives.ReadUInt32BigEndian(message.Slice(offset, 4));

        return (circuitId, nodePublicKey, ipv4Address, port, identifier);
    }

    /// <summary>
    /// Parses an EXTENDED message.
    /// </summary>
    public static (uint circuitId, byte[] ephemeralKey, byte[] auth, byte[] candidateList) ParseExtended(ReadOnlySpan<byte> message)
    {
        // EXTENDED has same structure as CREATED
        return ParseCreated(message);
    }
}
