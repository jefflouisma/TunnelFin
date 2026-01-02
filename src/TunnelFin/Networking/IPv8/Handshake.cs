using System.Buffers.Binary;
using TunnelFin.Networking.Identity;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// IPv8 handshake protocol implementation.
/// Implements four-message discovery: introduction-request/response, puncture-request/puncture.
/// </summary>
public class Handshake
{
    private readonly NetworkIdentity _identity;

    // Message type IDs from py-ipv8
    private const byte MSG_INTRODUCTION_REQUEST = 246;
    private const byte MSG_INTRODUCTION_RESPONSE = 245;
    private const byte MSG_PUNCTURE_REQUEST = 250;
    private const byte MSG_PUNCTURE = 249;

    /// <summary>
    /// Initializes a new instance of the Handshake class.
    /// </summary>
    /// <param name="identity">Network identity for signing messages.</param>
    public Handshake(NetworkIdentity identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    /// <summary>
    /// Creates an introduction-request message.
    /// </summary>
    /// <param name="destinationAddress">Destination peer address (WAN address).</param>
    /// <param name="sourceLanAddress">Source LAN address.</param>
    /// <param name="sourceWanAddress">Source WAN address.</param>
    /// <param name="identifier">Request identifier.</param>
    /// <returns>Serialized introduction-request message.</returns>
    public byte[] CreateIntroductionRequest(
        (string ip, int port) destinationAddress,
        (string ip, int port) sourceLanAddress,
        (string ip, int port) sourceWanAddress,
        ushort identifier)
    {
        // Format: destination_address (6 bytes) + source_lan_address (6 bytes) + source_wan_address (6 bytes) + identifier (2 bytes)
        var buffer = new byte[20];
        int offset = 0;

        // Destination address (4-byte IPv4 + 2-byte port, big-endian)
        WriteAddress(buffer, ref offset, destinationAddress);

        // Source LAN address
        WriteAddress(buffer, ref offset, sourceLanAddress);

        // Source WAN address
        WriteAddress(buffer, ref offset, sourceWanAddress);

        // Identifier (2-byte big-endian)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), identifier);

        return buffer;
    }

    /// <summary>
    /// Creates an introduction-response message.
    /// </summary>
    public byte[] CreateIntroductionResponse(
        (string ip, int port) destinationAddress,
        (string ip, int port) sourceLanAddress,
        (string ip, int port) sourceWanAddress,
        (string ip, int port) lanIntroductionAddress,
        (string ip, int port) wanIntroductionAddress,
        ushort identifier)
    {
        // Format: 5 addresses (30 bytes) + identifier (2 bytes)
        var buffer = new byte[32];
        int offset = 0;

        WriteAddress(buffer, ref offset, destinationAddress);
        WriteAddress(buffer, ref offset, sourceLanAddress);
        WriteAddress(buffer, ref offset, sourceWanAddress);
        WriteAddress(buffer, ref offset, lanIntroductionAddress);
        WriteAddress(buffer, ref offset, wanIntroductionAddress);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), identifier);

        return buffer;
    }

    /// <summary>
    /// Creates a puncture-request message.
    /// </summary>
    public byte[] CreatePunctureRequest(
        (string ip, int port) lanWalkerAddress,
        (string ip, int port) wanWalkerAddress,
        ushort identifier)
    {
        // Format: 2 addresses (12 bytes) + identifier (2 bytes)
        var buffer = new byte[14];
        int offset = 0;

        WriteAddress(buffer, ref offset, lanWalkerAddress);
        WriteAddress(buffer, ref offset, wanWalkerAddress);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), identifier);

        return buffer;
    }

    /// <summary>
    /// Creates a puncture message.
    /// </summary>
    public byte[] CreatePuncture(
        (string ip, int port) sourceLanAddress,
        (string ip, int port) sourceWanAddress,
        ushort identifier)
    {
        // Format: 2 addresses (12 bytes) + identifier (2 bytes)
        var buffer = new byte[14];
        int offset = 0;

        WriteAddress(buffer, ref offset, sourceLanAddress);
        WriteAddress(buffer, ref offset, sourceWanAddress);

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), identifier);

        return buffer;
    }

    /// <summary>
    /// Parses an introduction-request message.
    /// </summary>
    public IntroductionRequestPayload ParseIntroductionRequest(byte[] message)
    {
        if (message.Length < 20)
            throw new ArgumentException("Invalid introduction-request message length");

        int offset = 0;
        var destinationAddress = ReadAddress(message, ref offset);
        var sourceLanAddress = ReadAddress(message, ref offset);
        var sourceWanAddress = ReadAddress(message, ref offset);
        var identifier = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset));

        return new IntroductionRequestPayload(destinationAddress, sourceLanAddress, sourceWanAddress, identifier);
    }

    /// <summary>
    /// Parses a puncture-request message.
    /// </summary>
    public PunctureRequestPayload ParsePunctureRequest(byte[] message)
    {
        if (message.Length < 14)
            throw new ArgumentException("Invalid puncture-request message length");

        int offset = 0;
        var lanWalkerAddress = ReadAddress(message, ref offset);
        var wanWalkerAddress = ReadAddress(message, ref offset);
        var identifier = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(offset));

        return new PunctureRequestPayload(lanWalkerAddress, wanWalkerAddress, identifier);
    }

    private static void WriteAddress(byte[] buffer, ref int offset, (string ip, int port) address)
    {
        // Parse IPv4 address
        var parts = address.ip.Split('.');
        if (parts.Length != 4)
            throw new ArgumentException($"Invalid IPv4 address: {address.ip}");

        for (int i = 0; i < 4; i++)
            buffer[offset++] = byte.Parse(parts[i]);

        // Write port (2-byte big-endian)
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)address.port);
        offset += 2;
    }

    private static (string ip, int port) ReadAddress(byte[] buffer, ref int offset)
    {
        var ip = $"{buffer[offset]}.{buffer[offset + 1]}.{buffer[offset + 2]}.{buffer[offset + 3]}";
        offset += 4;

        var port = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(offset));
        offset += 2;

        return (ip, port);
    }
}

/// <summary>
/// Payload for introduction-request message.
/// </summary>
public record IntroductionRequestPayload(
    (string ip, int port) DestinationAddress,
    (string ip, int port) SourceLanAddress,
    (string ip, int port) SourceWanAddress,
    ushort Identifier);

/// <summary>
/// Payload for puncture-request message.
/// </summary>
public record PunctureRequestPayload(
    (string ip, int port) LanWalkerAddress,
    (string ip, int port) WanWalkerAddress,
    ushort Identifier);

