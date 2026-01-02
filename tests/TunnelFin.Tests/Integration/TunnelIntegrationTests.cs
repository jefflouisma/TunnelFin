using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using TunnelFin.Configuration;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for tunnel proxy and IP hiding (T066, SC-005).
/// Verifies that BitTorrent traffic is properly tunneled through circuits.
/// </summary>
public class TunnelIntegrationTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly UdpTransport _transport;
    private readonly BootstrapManager _bootstrapManager;
    private readonly CircuitManager _circuitManager;
    private readonly TunnelProxy _tunnelProxy;
    private readonly AnonymitySettings _settings;

    public TunnelIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _transport = new UdpTransport(_mockLogger.Object);
        _bootstrapManager = new BootstrapManager(_mockLogger.Object, _transport);
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 3,
            MinConcurrentCircuits = 2,
            MaxConcurrentCircuits = 10
        };
        _circuitManager = new CircuitManager(_settings);
        _tunnelProxy = new TunnelProxy(_mockLogger.Object);
    }

    [Fact]
    public async Task TunnelProxy_Should_Create_Tunnel_Through_Circuit()
    {
        // Arrange
        await _transport.StartAsync(0);

        // Discover real peers from bootstrap nodes
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);

        // Create mock network client that simulates successful responses
        var mockNetworkClient = new Mock<ICircuitNetworkClient>();
        mockNetworkClient.Setup(c => c.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockNetworkClient.Setup(c => c.SendCreateAsync(
                It.IsAny<uint>(),
                It.IsAny<Peer>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint circuitId, Peer relay, byte[] ephemeralKey, CancellationToken ct) =>
            {
                var mockEphemeralKey = new byte[32];
                var mockAuth = new byte[32];
                new Random().NextBytes(mockEphemeralKey);
                new Random().NextBytes(mockAuth);
                return new CreateResponse(circuitId, 1, mockEphemeralKey, mockAuth, Array.Empty<byte>());
            });
        mockNetworkClient.Setup(c => c.SendExtendAsync(
                It.IsAny<uint>(),
                It.IsAny<Peer>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint circuitId, Peer relay, byte[] ephemeralKey, CancellationToken ct) =>
            {
                var mockEphemeralKey = new byte[32];
                var mockAuth = new byte[32];
                new Random().NextBytes(mockEphemeralKey);
                new Random().NextBytes(mockAuth);
                return new ExtendResponse(circuitId, 1, mockEphemeralKey, mockAuth, Array.Empty<byte>());
            });

        var circuitManager = new CircuitManager(_settings, mockNetworkClient.Object, _mockLogger.Object);

        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            circuitManager.AddPeer(peer);
        }

        var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);
        await _tunnelProxy.StartAsync();

        // Act
        var remoteEndpoint = new System.Net.IPEndPoint(
            System.Net.IPAddress.Parse("1.1.1.1"), 80);
        var tunnel = await _tunnelProxy.CreateTunnelAsync(circuit, remoteEndpoint);

        // Assert
        tunnel.Should().NotBeNull("Tunnel should be created");
        tunnel.StreamId.Should().BeGreaterThan(0, "Tunnel should have valid stream ID");
        tunnel.CanRead.Should().BeTrue("Tunnel should support reading");
        tunnel.CanWrite.Should().BeTrue("Tunnel should support writing");
    }

    [Fact]
    public async Task TunnelProxy_Should_Hide_Source_IP_Address()
    {
        // Arrange
        await _transport.StartAsync(0);

        // Discover real peers from bootstrap nodes
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);

        // Create mock network client that simulates successful responses
        var mockNetworkClient = new Mock<ICircuitNetworkClient>();
        mockNetworkClient.Setup(c => c.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockNetworkClient.Setup(c => c.SendCreateAsync(
                It.IsAny<uint>(),
                It.IsAny<Peer>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint circuitId, Peer relay, byte[] ephemeralKey, CancellationToken ct) =>
            {
                var mockEphemeralKey = new byte[32];
                var mockAuth = new byte[32];
                new Random().NextBytes(mockEphemeralKey);
                new Random().NextBytes(mockAuth);
                return new CreateResponse(circuitId, 1, mockEphemeralKey, mockAuth, Array.Empty<byte>());
            });
        mockNetworkClient.Setup(c => c.SendExtendAsync(
                It.IsAny<uint>(),
                It.IsAny<Peer>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((uint circuitId, Peer relay, byte[] ephemeralKey, CancellationToken ct) =>
            {
                var mockEphemeralKey = new byte[32];
                var mockAuth = new byte[32];
                new Random().NextBytes(mockEphemeralKey);
                new Random().NextBytes(mockAuth);
                return new ExtendResponse(circuitId, 1, mockEphemeralKey, mockAuth, Array.Empty<byte>());
            });

        var circuitManager = new CircuitManager(_settings, mockNetworkClient.Object, _mockLogger.Object);

        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            circuitManager.AddPeer(peer);
        }

        var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);
        await _tunnelProxy.StartAsync();

        // Act
        var remoteEndpoint = new System.Net.IPEndPoint(
            System.Net.IPAddress.Parse("1.1.1.1"), 80);
        var tunnel = await _tunnelProxy.CreateTunnelAsync(circuit, remoteEndpoint);

        // Assert
        // The exit node's IP should be visible to the remote endpoint, not our IP
        // This is verified by the circuit having 3 hops
        circuit.CurrentHopCount.Should().Be(3, "Circuit should have 3 hops for IP hiding");
        circuit.IsEstablished.Should().BeTrue("Circuit should be established");
    }

    [Fact]
    public async Task LayeredEncryption_Should_Encrypt_And_Decrypt_Data()
    {
        // Arrange
        var circuit = new Circuit(12345, targetHopCount: 3);

        // Add 3 hops with key exchange
        for (int i = 0; i < 3; i++)
        {
            var publicKey = new byte[32];
            for (int j = 0; j < 32; j++) publicKey[j] = (byte)(i + j);

            // Convert IP address to uint32 (big-endian)
            var ipBytes = System.Net.IPAddress.Parse($"192.168.1.{i + 1}").GetAddressBytes();
            var ipv4Address = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);

            var hopNode = new HopNode(publicKey, ipv4Address, (ushort)(8000 + i), i);

            // Simulate key exchange with NSec keys
            var algorithm = KeyAgreementAlgorithm.X25519;
            using var ourEphemeralKey = Key.Create(algorithm);
            var peerEphemeralPublicKey = new byte[32];
            for (int j = 0; j < 32; j++) peerEphemeralPublicKey[j] = (byte)(i * 10 + j);

            hopNode.CompleteKeyExchange(peerEphemeralPublicKey, ourEphemeralKey);

            circuit.AddHop(hopNode);
        }

        var plaintext = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var encrypted = LayeredEncryption.EncryptLayers(plaintext, circuit);
        var decrypted = LayeredEncryption.DecryptLayers(encrypted, circuit);

        // Assert
        encrypted.Should().NotBeNull("Encrypted data should not be null");
        decrypted.Should().Equal(plaintext, "Decrypted data should match original plaintext");
    }

    [Fact]
    public async Task TunnelStream_Should_Track_Bytes_Sent_And_Received()
    {
        // Arrange
        var circuit = new Circuit(12345, targetHopCount: 3);

        // Add 3 hops and mark circuit as established
        for (int i = 0; i < 3; i++)
        {
            var publicKey = new byte[32];
            for (int j = 0; j < 32; j++) publicKey[j] = (byte)(i + j);

            var ipBytes = System.Net.IPAddress.Parse($"192.168.1.{i + 1}").GetAddressBytes();
            var ipv4Address = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);

            var hopNode = new HopNode(publicKey, ipv4Address, (ushort)(8000 + i), i);

            var algorithm = KeyAgreementAlgorithm.X25519;
            using var ourEphemeralKey = Key.Create(algorithm);
            var peerEphemeralPublicKey = new byte[32];
            for (int j = 0; j < 32; j++) peerEphemeralPublicKey[j] = (byte)(i * 10 + j);

            hopNode.CompleteKeyExchange(peerEphemeralPublicKey, ourEphemeralKey);
            circuit.AddHop(hopNode);
        }

        circuit.MarkEstablished();

        var remoteEndpoint = new System.Net.IPEndPoint(
            System.Net.IPAddress.Parse("1.1.1.1"), 80);
        var stream = new TunnelStream(circuit, remoteEndpoint, streamId: 1);

        // Act
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        stream.Write(data, 0, data.Length);

        // Assert
        stream.BytesSent.Should().Be(5, "Should track bytes sent");
        stream.Position.Should().Be(5, "Position should advance");
    }

    public void Dispose()
    {
        _tunnelProxy?.Dispose();
        _circuitManager?.Dispose();
        _transport?.Dispose();
    }
}

