using System.Buffers.Binary;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// IPv8 message deserialization using big-endian byte order per FR-048 and ipv8-wire-format.md.
/// Parses messages from Python peers with correct byte order handling.
/// </summary>
public static class MessageDeserializer
{
    /// <summary>
    /// Reads a 32-bit unsigned integer in big-endian format.
    /// </summary>
    public static uint ReadUInt32(ReadOnlySpan<byte> buffer)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 16-bit unsigned integer in big-endian format.
    /// </summary>
    public static ushort ReadUInt16(ReadOnlySpan<byte> buffer)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    /// <summary>
    /// Reads a 64-bit unsigned integer in big-endian format.
    /// </summary>
    public static ulong ReadUInt64(ReadOnlySpan<byte> buffer)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    /// <summary>
    /// Reads a boolean from a single byte (0x00 = false, 0x01 = true).
    /// </summary>
    public static bool ReadBoolean(ReadOnlySpan<byte> buffer)
    {
        return buffer[0] != 0;
    }

    /// <summary>
    /// Reads a variable-length field with 2-byte big-endian length prefix.
    /// Returns the data and the total bytes consumed (2 + data length).
    /// </summary>
    public static (byte[] data, int bytesConsumed) ReadVariableLength(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 2)
            throw new ArgumentException("Buffer too small to read length prefix", nameof(buffer));

        var length = ReadUInt16(buffer);
        
        if (buffer.Length < 2 + length)
            throw new ArgumentException($"Buffer too small to read {length} bytes of data", nameof(buffer));

        var data = buffer.Slice(2, length).ToArray();
        return (data, 2 + length);
    }

    /// <summary>
    /// Reads an IPv4 address as a 32-bit big-endian unsigned integer.
    /// </summary>
    public static uint ReadIPv4Address(ReadOnlySpan<byte> buffer)
    {
        return ReadUInt32(buffer);
    }

    /// <summary>
    /// Reads a socket address (IPv4 + port) from 6 bytes.
    /// </summary>
    public static (uint ipAddress, ushort port) ReadSocketAddress(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 6)
            throw new ArgumentException("Buffer too small to read socket address", nameof(buffer));

        var ipAddress = ReadIPv4Address(buffer);
        var port = ReadUInt16(buffer.Slice(4));
        
        return (ipAddress, port);
    }

    /// <summary>
    /// Reads a circuit ID (4-byte big-endian unsigned integer).
    /// Circuit ID is always the first field in circuit-based messages.
    /// </summary>
    public static uint ReadCircuitId(ReadOnlySpan<byte> buffer)
    {
        return ReadUInt32(buffer);
    }

    /// <summary>
    /// Reads a timestamp (8-byte big-endian unsigned long, milliseconds since epoch).
    /// </summary>
    public static ulong ReadTimestamp(ReadOnlySpan<byte> buffer)
    {
        return ReadUInt64(buffer);
    }

    /// <summary>
    /// Reads a timestamp and converts to DateTimeOffset.
    /// </summary>
    public static DateTimeOffset ReadTimestampAsDateTimeOffset(ReadOnlySpan<byte> buffer)
    {
        var timestampMs = ReadTimestamp(buffer);
        return DateTimeOffset.FromUnixTimeMilliseconds((long)timestampMs);
    }

    /// <summary>
    /// Validates that a buffer has at least the specified minimum length.
    /// </summary>
    public static void ValidateBufferLength(ReadOnlySpan<byte> buffer, int minimumLength, string fieldName)
    {
        if (buffer.Length < minimumLength)
            throw new ArgumentException(
                $"Buffer too small to read {fieldName}: expected at least {minimumLength} bytes, got {buffer.Length}",
                nameof(buffer));
    }

    /// <summary>
    /// Parses a TrustChain block from serialized bytes.
    /// Returns the block components for signature verification.
    /// </summary>
    public static (
        byte[] creatorPublicKey,
        byte[] linkPublicKey,
        uint sequenceNumber,
        byte[] previousHash,
        ulong timestampMs,
        byte[] message,
        byte[] signature
    ) ParseTrustChainBlock(ReadOnlySpan<byte> buffer)
    {
        // Minimum size: 74 + 74 + 4 + 32 + 8 + 2 + 0 + 64 = 258 bytes
        ValidateBufferLength(buffer, 258, "TrustChain block");

        var offset = 0;

        // 1. Creator public key (74 bytes)
        var creatorPublicKey = buffer.Slice(offset, 74).ToArray();
        offset += 74;

        // 2. Link public key (74 bytes)
        var linkPublicKey = buffer.Slice(offset, 74).ToArray();
        offset += 74;

        // 3. Sequence number (4 bytes, big-endian)
        var sequenceNumber = ReadUInt32(buffer.Slice(offset));
        offset += 4;

        // 4. Previous hash (32 bytes)
        var previousHash = buffer.Slice(offset, 32).ToArray();
        offset += 32;

        // 5. Timestamp (8 bytes, big-endian)
        var timestampMs = ReadUInt64(buffer.Slice(offset));
        offset += 8;

        // 6. Message length (2 bytes, big-endian)
        var messageLength = ReadUInt16(buffer.Slice(offset));
        offset += 2;

        // 7. Message content (variable)
        ValidateBufferLength(buffer, offset + messageLength + 64, "TrustChain block with message and signature");
        var message = buffer.Slice(offset, messageLength).ToArray();
        offset += messageLength;

        // 8. Signature (64 bytes)
        var signature = buffer.Slice(offset, 64).ToArray();

        return (creatorPublicKey, linkPublicKey, sequenceNumber, previousHash, timestampMs, message, signature);
    }
}

