using System.Buffers.Binary;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// IPv8 message serialization using big-endian byte order per FR-048 and ipv8-wire-format.md.
/// All multi-byte integers use BinaryPrimitives for network byte order (big-endian).
/// </summary>
public static class MessageSerializer
{
    /// <summary>
    /// Writes a 32-bit unsigned integer in big-endian format.
    /// Used for circuit IDs, sequence numbers, identifiers.
    /// </summary>
    public static void WriteUInt32(Span<byte> buffer, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
    }

    /// <summary>
    /// Writes a 16-bit unsigned integer in big-endian format.
    /// Used for port numbers, length prefixes.
    /// </summary>
    public static void WriteUInt16(Span<byte> buffer, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
    }

    /// <summary>
    /// Writes a 64-bit unsigned integer in big-endian format.
    /// Used for timestamps (milliseconds since epoch).
    /// </summary>
    public static void WriteUInt64(Span<byte> buffer, ulong value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
    }

    /// <summary>
    /// Writes a boolean as a single byte (0x00 = false, 0x01 = true).
    /// </summary>
    public static void WriteBoolean(Span<byte> buffer, bool value)
    {
        buffer[0] = value ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Writes a variable-length field with 2-byte big-endian length prefix.
    /// </summary>
    public static int WriteVariableLength(Span<byte> buffer, ReadOnlySpan<byte> data)
    {
        if (data.Length > ushort.MaxValue)
            throw new ArgumentException($"Data length {data.Length} exceeds maximum {ushort.MaxValue}", nameof(data));

        // Write 2-byte length prefix
        WriteUInt16(buffer, (ushort)data.Length);
        
        // Write data
        data.CopyTo(buffer.Slice(2));
        
        return 2 + data.Length;
    }

    /// <summary>
    /// Writes an IPv4 address as a 32-bit big-endian unsigned integer.
    /// </summary>
    public static void WriteIPv4Address(Span<byte> buffer, uint ipAddress)
    {
        WriteUInt32(buffer, ipAddress);
    }

    /// <summary>
    /// Writes a socket address (IPv4 + port) as 6 bytes (4-byte IP + 2-byte port).
    /// </summary>
    public static void WriteSocketAddress(Span<byte> buffer, uint ipAddress, ushort port)
    {
        WriteIPv4Address(buffer, ipAddress);
        WriteUInt16(buffer.Slice(4), port);
    }

    /// <summary>
    /// Serializes a circuit ID (4-byte big-endian unsigned integer).
    /// Circuit ID is always the first field in circuit-based messages.
    /// </summary>
    public static void WriteCircuitId(Span<byte> buffer, uint circuitId)
    {
        WriteUInt32(buffer, circuitId);
    }

    /// <summary>
    /// Serializes a timestamp (8-byte big-endian unsigned long, milliseconds since epoch).
    /// </summary>
    public static void WriteTimestamp(Span<byte> buffer, ulong timestampMs)
    {
        WriteUInt64(buffer, timestampMs);
    }

    /// <summary>
    /// Serializes a timestamp from DateTimeOffset.
    /// </summary>
    public static void WriteTimestamp(Span<byte> buffer, DateTimeOffset timestamp)
    {
        var timestampMs = (ulong)timestamp.ToUnixTimeMilliseconds();
        WriteTimestamp(buffer, timestampMs);
    }

    /// <summary>
    /// Calculates the size needed for a variable-length field (2-byte prefix + data).
    /// </summary>
    public static int GetVariableLengthSize(int dataLength)
    {
        return 2 + dataLength;
    }

    /// <summary>
    /// Serializes a TrustChain block for signature verification.
    /// Exact field order per ipv8-wire-format.md:
    /// 1. Creator public key (74 bytes)
    /// 2. Link public key (74 bytes)
    /// 3. Sequence number (4 bytes, big-endian)
    /// 4. Previous hash (32 bytes)
    /// 5. Timestamp (8 bytes, big-endian)
    /// 6. Message length (2 bytes, big-endian)
    /// 7. Message content (variable)
    /// Signature (64 bytes) is computed over fields 1-7.
    /// </summary>
    public static byte[] SerializeTrustChainBlockForSigning(
        byte[] creatorPublicKey,
        byte[] linkPublicKey,
        uint sequenceNumber,
        byte[] previousHash,
        ulong timestampMs,
        byte[] message)
    {
        if (creatorPublicKey.Length != 74)
            throw new ArgumentException("Creator public key must be 74 bytes", nameof(creatorPublicKey));
        
        if (linkPublicKey.Length != 74)
            throw new ArgumentException("Link public key must be 74 bytes", nameof(linkPublicKey));
        
        if (previousHash.Length != 32)
            throw new ArgumentException("Previous hash must be 32 bytes", nameof(previousHash));
        
        if (message.Length > ushort.MaxValue)
            throw new ArgumentException($"Message length {message.Length} exceeds maximum {ushort.MaxValue}", nameof(message));

        // Calculate total size: 74 + 74 + 4 + 32 + 8 + 2 + message.Length
        var totalSize = 194 + message.Length;
        var buffer = new byte[totalSize];
        var offset = 0;

        // 1. Creator public key (74 bytes)
        creatorPublicKey.CopyTo(buffer, offset);
        offset += 74;

        // 2. Link public key (74 bytes)
        linkPublicKey.CopyTo(buffer, offset);
        offset += 74;

        // 3. Sequence number (4 bytes, big-endian)
        WriteUInt32(buffer.AsSpan(offset), sequenceNumber);
        offset += 4;

        // 4. Previous hash (32 bytes)
        previousHash.CopyTo(buffer, offset);
        offset += 32;

        // 5. Timestamp (8 bytes, big-endian)
        WriteUInt64(buffer.AsSpan(offset), timestampMs);
        offset += 8;

        // 6. Message length (2 bytes, big-endian)
        WriteUInt16(buffer.AsSpan(offset), (ushort)message.Length);
        offset += 2;

        // 7. Message content (variable)
        message.CopyTo(buffer, offset);

        return buffer;
    }
}

