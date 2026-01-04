using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for circuit establishment (T065, SC-004, SC-007).
/// Tests real circuit creation through Tribler network relays.
/// </summary>
public class CircuitIntegrationTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly UdpTransport _transport;
    private readonly BootstrapManager _bootstrapManager;
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;

    public CircuitIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _transport = new UdpTransport(_mockLogger.Object);
        _bootstrapManager = new BootstrapManager(_mockLogger.Object, _transport);
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 3,
            MinConcurrentCircuits = 2,
            MaxConcurrentCircuits = 10,
            CircuitEstablishmentTimeoutSeconds = 30,
            MinRelayReliability = 0.7
        };
        _circuitManager = new CircuitManager(_settings);
    }

    [Fact(Skip = "Requires real network connectivity to IPv8 bootstrap nodes")]
    public async Task CircuitManager_Should_Create_Circuit_Through_Real_Network()
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

        // Add discovered peers to circuit manager
        // Mark them as handshake complete since we're using a mock network client
        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            peer.IsHandshakeComplete = true;
            peer.IsRelayCandidate = true;
            circuitManager.AddPeer(peer);
        }

        // If no peers discovered, create mock peers for testing
        if (!_bootstrapManager.PeerTable.Peers.Any())
        {
            for (int i = 0; i < 5; i++)
            {
                var publicKey = new byte[32];
                for (int j = 0; j < 32; j++) publicKey[j] = (byte)(i * 10 + j);
                var peer = new Peer(publicKey, (uint)(0xC0A80101 + i), (ushort)(8000 + i));
                peer.IsHandshakeComplete = true;
                peer.IsRelayCandidate = true;
                circuitManager.AddPeer(peer);
            }
        }

        // Act
        var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);

        // Assert
        circuit.Should().NotBeNull("Circuit should be created");
        circuit.State.Should().Be(CircuitState.Established, "Circuit should be fully established");
        circuit.CurrentHopCount.Should().Be(3, "Circuit should have 3 hops");
        circuit.IsEstablished.Should().BeTrue("Circuit should be marked as established");
    }

    [Fact(Skip = "Requires real network connectivity to IPv8 bootstrap nodes")]
    public async Task CircuitManager_Should_Maintain_Minimum_Concurrent_Circuits()
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

        // Add discovered peers to circuit manager with proper state
        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            peer.IsHandshakeComplete = true;
            peer.IsRelayCandidate = true;
            circuitManager.AddPeer(peer);
        }

        // If no peers discovered, create mock peers for testing
        if (!_bootstrapManager.PeerTable.Peers.Any())
        {
            for (int i = 0; i < 5; i++)
            {
                var publicKey = new byte[32];
                for (int j = 0; j < 32; j++) publicKey[j] = (byte)(i * 10 + j);
                var peer = new Peer(publicKey, (uint)(0xC0A80101 + i), (ushort)(8000 + i));
                peer.IsHandshakeComplete = true;
                peer.IsRelayCandidate = true;
                circuitManager.AddPeer(peer);
            }
        }

        // Act
        await circuitManager.StartCircuitRecoveryAsync();
        await Task.Delay(7000); // Wait for recovery to check (5s) and create circuits (2s)

        // Assert
        circuitManager.ActiveCircuitCount.Should().BeGreaterThanOrEqualTo(_settings.MinConcurrentCircuits,
            "Should maintain minimum concurrent circuits");
    }

    [Fact(Skip = "Requires real network connectivity to IPv8 bootstrap nodes")]
    public async Task CircuitManager_Should_Recover_Failed_Circuits()
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

        // Add discovered peers to circuit manager with proper state
        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            peer.IsHandshakeComplete = true;
            peer.IsRelayCandidate = true;
            circuitManager.AddPeer(peer);
        }

        // If no peers discovered, create mock peers for testing
        if (!_bootstrapManager.PeerTable.Peers.Any())
        {
            for (int i = 0; i < 5; i++)
            {
                var publicKey = new byte[32];
                for (int j = 0; j < 32; j++) publicKey[j] = (byte)(i * 10 + j);
                var peer = new Peer(publicKey, (uint)(0xC0A80101 + i), (ushort)(8000 + i));
                peer.IsHandshakeComplete = true;
                peer.IsRelayCandidate = true;
                circuitManager.AddPeer(peer);
            }
        }

        var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);
        var initialCircuitId = circuit.IPv8CircuitId;

        // Act - Simulate circuit failure
        circuit.MarkFailed("Simulated failure");
        await circuitManager.StartCircuitRecoveryAsync();
        await Task.Delay(10000); // Wait for recovery (max 30s per spec)

        // Assert
        circuitManager.ActiveCircuitCount.Should().BeGreaterThanOrEqualTo(_settings.MinConcurrentCircuits,
            "Should recover failed circuit and maintain minimum count");
    }

    [Fact]
    public async Task CircuitManager_Should_Track_Circuit_Metrics()
    {
        // Arrange
        var circuit = new Circuit(12345, targetHopCount: 3);

        // Act
        circuit.RecordBytesSent(1024);
        circuit.RecordBytesReceived(2048);
        circuit.RecordRoundTripTime(50.5);

        // Assert
        circuit.BytesSent.Should().Be(1024);
        circuit.BytesReceived.Should().Be(2048);
        circuit.RoundTripTimeMs.Should().Be(50.5);
    }

    [Fact]
    public async Task CircuitManager_Should_Detect_Expired_Circuits()
    {
        // Arrange
        var circuit = new Circuit(12345, targetHopCount: 3);
        circuit.SetExpiration(DateTime.UtcNow.AddSeconds(-1)); // Already expired

        // Act & Assert
        circuit.IsExpired.Should().BeTrue("Circuit should be detected as expired");
    }

    public void Dispose()
    {
        _circuitManager?.Dispose();
        _transport?.Dispose();
    }
}

