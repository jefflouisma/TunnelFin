using System.Buffers.Binary;
using System.Net;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using TunnelFin.Core;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// IPv8 handshake protocol implementation.
/// Implements four-message discovery: introduction-request/response, puncture-request/puncture.
/// </summary>
public class Handshake
{
    private readonly NetworkIdentity _identity;
    private readonly ITransport? _transport;
    private readonly PrivacyAwareLogger? _logger;

    // Message type IDs from py-ipv8
    private const byte MSG_INTRODUCTION_REQUEST = 246;
    private const byte MSG_INTRODUCTION_RESPONSE = 245;
    private const byte MSG_PUNCTURE_REQUEST = 250;
    private const byte MSG_PUNCTURE = 249;

    // Signature algorithm
    private static readonly SignatureAlgorithm Ed25519 = SignatureAlgorithm.Ed25519;

    // IPv8 community ID for Tribler discovery community (from py-ipv8 DiscoveryCommunity)
    // unhexlify("7e313685c1912a141279f8248fc8db5899c5df5a")
    // Note: Bootstrap nodes use DiscoveryCommunity for peer discovery, not TunnelCommunity
    private static readonly byte[] TriblerCommunityId = new byte[] {
        0x7e, 0x31, 0x36, 0x85, 0xc1, 0x91, 0x2a, 0x14,
        0x12, 0x79, 0xf8, 0x24, 0x8f, 0xc8, 0xdb, 0x58,
        0x99, 0xc5, 0xdf, 0x5a
    };

    /// <summary>
    /// Initializes a new instance of the Handshake class.
    /// </summary>
    /// <param name="identity">Network identity for signing messages.</param>
    public Handshake(NetworkIdentity identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    /// <summary>
    /// Initializes a new instance of the Handshake class with transport.
    /// </summary>
    /// <param name="identity">Network identity for signing messages.</param>
    /// <param name="transport">Transport layer for sending/receiving messages.</param>
    /// <param name="logger">Logger for privacy-aware logging.</param>
    public Handshake(NetworkIdentity identity, ITransport transport, ILogger logger)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
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
        // Format: destination_address (6 bytes) + source_lan_address (6 bytes) + source_wan_address (6 bytes) + bits (1 byte) + identifier (2 bytes)
        // bits field: connection_type_0, connection_type_1, supports_new_style, dflag1, dflag2, tunnel, sync, advice
        var buffer = new byte[21];
        int offset = 0;

        // Destination address (4-byte IPv4 + 2-byte port, big-endian)
        WriteAddress(buffer, ref offset, destinationAddress);

        // Source LAN address
        WriteAddress(buffer, ref offset, sourceLanAddress);

        // Source WAN address
        WriteAddress(buffer, ref offset, sourceWanAddress);

        // Bits field (1 byte):
        // bit 0-1: connection_type (00 = unknown, 10 = public, 11 = symmetric-NAT)
        // bit 2: supports_new_style (1 = yes)
        // bit 3-6: reserved (0)
        // bit 7: advice (1 = request introduction to another peer)
        byte bits = 0b00000101; // connection_type=unknown(00), supports_new_style=1, advice=1
        buffer[offset++] = bits;

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
    /// Signs a message with Ed25519 signature (FR-010, FR-011).
    /// </summary>
    /// <param name="messageType">Message type byte.</param>
    /// <param name="payload">Message payload.</param>
    /// <returns>Signed message: [type(1) + payload + signature(64)].</returns>
    public byte[] SignMessage(byte messageType, byte[] payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        // Message format: [type(1) + payload + signature(64)]
        var message = new byte[1 + payload.Length + 64];
        message[0] = messageType;
        Array.Copy(payload, 0, message, 1, payload.Length);

        // Sign: type + payload
        var dataToSign = new byte[1 + payload.Length];
        dataToSign[0] = messageType;
        Array.Copy(payload, 0, dataToSign, 1, payload.Length);

        var signature = _identity.Sign(dataToSign);
        Array.Copy(signature, 0, message, 1 + payload.Length, 64);

        return message;
    }

    /// <summary>
    /// Creates a full IPv8 message with proper format for network transmission.
    /// Format: prefix (22 bytes) + msg_type (1 byte) + BinMemberAuth + GlobalTime + payload + signature
    /// </summary>
    /// <param name="messageType">Message type byte.</param>
    /// <param name="payload">The payload (e.g., IntroductionRequestPayload).</param>
    /// <returns>Full IPv8 message ready for transmission.</returns>
    private byte[] CreateIPv8Message(byte messageType, byte[] payload)
    {
        // py-ipv8 message format (from lazy_community.py _ez_pack):
        // - Byte 0: 0x00 (prefix start)
        // - Byte 1: 0x02 (version)
        // - Bytes 2-21: Community ID (20 bytes)
        // - Byte 22: Message type
        // - Bytes 23+: BinMemberAuthenticationPayload (varlenH = 2-byte length + public key)
        // - Then: GlobalTimeDistributionPayload (Q = 8-byte unsigned long)
        // - Then: Payload
        // - Last 64 bytes: Ed25519 signature

        // Get public key in LibNaCL format: "LibNaCLPK:" + pk (32 bytes) + vk (32 bytes)
        // For Ed25519, we use the public key bytes directly (32 bytes)
        var publicKeyBytes = _identity.PublicKey;

        // Create LibNaCL-style public key format
        // Format: "LibNaCLPK:" (10 bytes) + pk (32 bytes) + vk (32 bytes) = 74 bytes
        // However, for simplicity, we'll use raw Ed25519 public key (32 bytes)
        // py-ipv8 accepts various key formats
        var publicKeyBin = new byte[10 + 32 + 32]; // LibNaCLPK format
        System.Text.Encoding.ASCII.GetBytes("LibNaCLPK:").CopyTo(publicKeyBin, 0);
        publicKeyBytes.CopyTo(publicKeyBin, 10);
        publicKeyBytes.CopyTo(publicKeyBin, 42); // vk = pk for Ed25519

        // Calculate total message size
        // Prefix (22) + msg_type (1) + auth_len (2) + auth_key (74) + global_time (8) + payload + signature (64)
        int totalSize = 22 + 1 + 2 + publicKeyBin.Length + 8 + payload.Length + 64;
        var message = new byte[totalSize];
        int offset = 0;

        // Prefix: 0x00 + 0x02 + community_id
        message[offset++] = 0x00;
        message[offset++] = 0x02;
        TriblerCommunityId.CopyTo(message, offset);
        offset += 20;

        // Message type
        message[offset++] = messageType;

        // BinMemberAuthenticationPayload: varlenH (2-byte big-endian length + data)
        BinaryPrimitives.WriteUInt16BigEndian(message.AsSpan(offset), (ushort)publicKeyBin.Length);
        offset += 2;
        publicKeyBin.CopyTo(message, offset);
        offset += publicKeyBin.Length;

        // GlobalTimeDistributionPayload: Q (8-byte big-endian unsigned long)
        // Use current timestamp or a random identifier
        ulong globalTime = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        BinaryPrimitives.WriteUInt64BigEndian(message.AsSpan(offset), globalTime);
        offset += 8;

        // Payload
        payload.CopyTo(message, offset);
        offset += payload.Length;

        // Sign everything except the signature space
        var dataToSign = message.AsSpan(0, offset).ToArray();
        var signature = _identity.Sign(dataToSign);
        signature.CopyTo(message, offset);

        return message;
    }

    /// <summary>
    /// Wraps a signed message with IPv8 community prefix for network transmission.
    /// DEPRECATED: Use CreateIPv8Message instead for proper py-ipv8 compatibility.
    /// </summary>
    [Obsolete("Use CreateIPv8Message for proper py-ipv8 compatibility")]
    private byte[] WrapWithIPv8Prefix(byte messageType, byte[] signedPayload)
    {
        // Legacy format - kept for backward compatibility
        var message = new byte[24 + signedPayload.Length];
        message[0] = 0x02;
        TriblerCommunityId.CopyTo(message, 1);
        message[21] = 0x00;
        message[22] = 0x00;
        message[23] = messageType;
        signedPayload.CopyTo(message, 24);
        return message;
    }

    /// <summary>
    /// Verifies a signed message (FR-011).
    /// </summary>
    /// <param name="message">Signed message.</param>
    /// <param name="publicKey">Public key to verify against.</param>
    /// <returns>True if signature is valid, false otherwise.</returns>
    public bool VerifyMessage(byte[] message, byte[] publicKey)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (publicKey == null)
            throw new ArgumentNullException(nameof(publicKey));
        if (message.Length < 65) // At least: type(1) + signature(64)
            return false;

        try
        {
            // Extract signature (last 64 bytes)
            var signature = new byte[64];
            Array.Copy(message, message.Length - 64, signature, 0, 64);

            // Data to verify: everything except signature
            var dataToVerify = new byte[message.Length - 64];
            Array.Copy(message, 0, dataToVerify, 0, message.Length - 64);

            // Import public key and verify
            var key = PublicKey.Import(Ed25519, publicKey, KeyBlobFormat.RawPublicKey);
            return Ed25519.Verify(key, dataToVerify, signature);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends an introduction-request message over the transport (FR-009).
    /// Uses proper py-ipv8 message format for Tribler network compatibility.
    /// </summary>
    public async Task<bool> SendIntroductionRequestAsync(
        IPEndPoint destination,
        (string ip, int port) sourceLanAddress,
        (string ip, int port) sourceWanAddress,
        ushort identifier,
        CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new InvalidOperationException("Transport not configured. Use constructor with ITransport parameter.");

        var payload = CreateIntroductionRequest(
            (destination.Address.ToString(), destination.Port),
            sourceLanAddress,
            sourceWanAddress,
            identifier);

        // Use proper py-ipv8 message format
        var fullMessage = CreateIPv8Message(MSG_INTRODUCTION_REQUEST, payload);

        try
        {
            await _transport.SendAsync(fullMessage, destination, cancellationToken);
            _logger?.LogDebug("Sent introduction-request to {Destination}, identifier={Identifier}, size={Size}",
                destination, identifier, fullMessage.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send introduction-request", ex);
            return false;
        }
    }

    /// <summary>
    /// Sends a puncture-request message via an intermediary peer (FR-013).
    /// Used for NAT traversal when direct connection is not possible.
    /// </summary>
    /// <param name="intermediary">Intermediary peer to relay the puncture request.</param>
    /// <param name="targetLanAddress">Target peer's LAN address.</param>
    /// <param name="targetWanAddress">Target peer's WAN address.</param>
    /// <param name="identifier">Request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if sent successfully, false otherwise.</returns>
    public async Task<bool> SendPunctureRequestAsync(
        IPEndPoint intermediary,
        (string ip, int port) targetLanAddress,
        (string ip, int port) targetWanAddress,
        ushort identifier,
        CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new InvalidOperationException("Transport not configured. Use constructor with ITransport parameter.");

        var payload = CreatePunctureRequest(targetLanAddress, targetWanAddress, identifier);
        var fullMessage = CreateIPv8Message(MSG_PUNCTURE_REQUEST, payload);

        try
        {
            await _transport.SendAsync(fullMessage, intermediary, cancellationToken);
            _logger?.LogDebug("Sent puncture-request via {Intermediary}, identifier={Identifier}",
                intermediary, identifier);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send puncture-request", ex);
            return false;
        }
    }

    /// <summary>
    /// Sends a puncture message directly to a peer (FR-013).
    /// Sent in response to receiving a puncture-request.
    /// </summary>
    /// <param name="destination">Destination peer endpoint.</param>
    /// <param name="sourceLanAddress">Source LAN address.</param>
    /// <param name="sourceWanAddress">Source WAN address.</param>
    /// <param name="identifier">Request identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if sent successfully, false otherwise.</returns>
    public async Task<bool> SendPunctureAsync(
        IPEndPoint destination,
        (string ip, int port) sourceLanAddress,
        (string ip, int port) sourceWanAddress,
        ushort identifier,
        CancellationToken cancellationToken = default)
    {
        if (_transport == null)
            throw new InvalidOperationException("Transport not configured. Use constructor with ITransport parameter.");

        var payload = CreatePuncture(sourceLanAddress, sourceWanAddress, identifier);
        var fullMessage = CreateIPv8Message(MSG_PUNCTURE, payload);

        try
        {
            await _transport.SendAsync(fullMessage, destination, cancellationToken);
            _logger?.LogDebug("Sent puncture to {Destination}, identifier={Identifier}",
                destination, identifier);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send puncture", ex);
            return false;
        }
    }

    /// <summary>
    /// Parses an introduction-request message.
    /// </summary>
    public IntroductionRequestPayload ParseIntroductionRequest(byte[] message)
    {
        if (message.Length < 21)
            throw new ArgumentException("Invalid introduction-request message length");

        int offset = 0;
        var destinationAddress = ReadAddress(message, ref offset);
        var sourceLanAddress = ReadAddress(message, ref offset);
        var sourceWanAddress = ReadAddress(message, ref offset);

        // Skip bits field (1 byte)
        offset++;

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

