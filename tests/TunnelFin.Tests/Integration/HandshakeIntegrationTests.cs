using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for IPv8 handshake protocol (T064, SC-002, SC-003).
/// Tests real peer handshake with introduction-request/response messages.
/// </summary>
public class HandshakeIntegrationTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly UdpTransport _transport;
    private readonly NetworkIdentity _identity;

    public HandshakeIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _transport = new UdpTransport(_mockLogger.Object);
        _identity = new NetworkIdentity();
    }

    [Fact(Skip = "Requires real network connectivity to IPv8 bootstrap nodes")]
    public async Task Handshake_Should_Complete_With_Real_Peer()
    {
        // Arrange
        await _transport.StartAsync(0);
        var handshake = new Handshake(_identity, _transport, _mockLogger.Object);

        // Discover real peers from bootstrap nodes
        var bootstrapManager = new BootstrapManager(_mockLogger.Object, _transport);
        await bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);

        var peer = bootstrapManager.PeerTable.Peers.Values.FirstOrDefault();
        peer.Should().NotBeNull("Should have discovered at least one peer");

        // Act
        // Convert uint32 IPv4Address to IPAddress
        var ipBytes = BitConverter.GetBytes(peer!.IPv4Address);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(ipBytes);
        var endpoint = new System.Net.IPEndPoint(
            new System.Net.IPAddress(ipBytes),
            peer.Port);

        var success = await handshake.SendIntroductionRequestAsync(
            endpoint,
            (_transport.LocalEndPoint!.Address.ToString(), _transport.LocalEndPoint.Port),
            (_transport.LocalEndPoint.Address.ToString(), _transport.LocalEndPoint.Port),
            identifier: 12345);

        // Assert
        success.Should().BeTrue("Handshake should send introduction-request successfully");
    }

    [Fact(Skip = "Requires real network connectivity to IPv8 bootstrap nodes")]
    public async Task Handshake_Should_Exchange_Messages_With_Bootstrap_Node()
    {
        // Arrange
        await _transport.StartAsync(0);
        var handshake = new Handshake(_identity, _transport, _mockLogger.Object);

        // Use a real bootstrap node
        var bootstrapNodes = BootstrapNode.GetDefaultNodes();
        var bootstrapNode = bootstrapNodes.First();
        var endpoint = bootstrapNode.GetEndPoint();

        // Act
        var success = await handshake.SendIntroductionRequestAsync(
            endpoint,
            (_transport.LocalEndPoint!.Address.ToString(), _transport.LocalEndPoint.Port),
            (_transport.LocalEndPoint.Address.ToString(), _transport.LocalEndPoint.Port),
            identifier: 54321);

        // Assert
        success.Should().BeTrue("Should successfully send introduction-request to bootstrap node");
    }

    [Fact]
    public async Task Handshake_Should_Create_Valid_IntroductionRequest_Message()
    {
        // Arrange
        var handshake = new Handshake(_identity);
        var destinationAddress = ("192.168.1.100", 8000);
        var sourceLanAddress = ("192.168.1.50", 8001);
        var sourceWanAddress = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        // Act
        var message = handshake.CreateIntroductionRequest(
            destinationAddress,
            sourceLanAddress,
            sourceWanAddress,
            identifier);

        // Assert
        message.Should().NotBeNull();
        message.Length.Should().Be(21, "Introduction-request payload should be 21 bytes (3 addresses + bits + identifier)");
    }

    [Fact]
    public async Task Handshake_Should_Sign_Messages_With_Ed25519()
    {
        // Arrange
        var handshake = new Handshake(_identity);
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var signature = _identity.Sign(payload);

        // Assert
        signature.Should().NotBeNull();
        signature.Length.Should().Be(64, "Ed25519 signature should be 64 bytes");
        
        // Verify signature
        var isValid = _identity.Verify(payload, signature);
        isValid.Should().BeTrue("Signature should be valid");
    }

    [Fact]
    public async Task Handshake_Should_Derive_Consistent_PeerId()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++) seed[i] = (byte)i;
        
        var identity1 = new NetworkIdentity(seed);
        var identity2 = new NetworkIdentity(seed);

        // Act & Assert
        identity1.PeerId.Should().Be(identity2.PeerId,
            "Same seed should produce same peer ID");
        identity1.PeerId.Length.Should().Be(40,
            "Peer ID should be 40-character hex string (SHA-1)");
    }

    public void Dispose()
    {
        _transport?.Dispose();
    }
}

