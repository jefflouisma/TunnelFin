using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Circuits;
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

    [Fact(Skip = "Requires live network connection to Tribler network")]
    public async Task CircuitManager_Should_Create_Circuit_Through_Real_Network()
    {
        // Arrange
        await _transport.StartAsync(0);
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 30);

        var networkClient = new CircuitNetworkClient(_transport, _mockLogger.Object);
        await networkClient.StartAsync();
        var circuitManager = new CircuitManager(_settings, networkClient, _mockLogger.Object);

        // Add discovered peers to circuit manager
        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            circuitManager.AddPeer(peer);
        }

        // Act
        var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);

        // Assert
        circuit.Should().NotBeNull("Circuit should be created");
        circuit.State.Should().Be(CircuitState.Established, "Circuit should be fully established");
        circuit.CurrentHopCount.Should().Be(3, "Circuit should have 3 hops");
        circuit.IsEstablished.Should().BeTrue("Circuit should be marked as established");
    }

    [Fact(Skip = "Requires live network connection to Tribler network")]
    public async Task CircuitManager_Should_Maintain_Minimum_Concurrent_Circuits()
    {
        // Arrange
        await _transport.StartAsync(0);
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 30);

        var networkClient = new CircuitNetworkClient(_transport, _mockLogger.Object);
        await networkClient.StartAsync();
        var circuitManager = new CircuitManager(_settings, networkClient, _mockLogger.Object);

        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            circuitManager.AddPeer(peer);
        }

        // Act
        await circuitManager.StartCircuitRecoveryAsync();
        await Task.Delay(5000); // Wait for recovery to create circuits

        // Assert
        circuitManager.ActiveCircuitCount.Should().BeGreaterThanOrEqualTo(_settings.MinConcurrentCircuits,
            "Should maintain minimum concurrent circuits");
    }

    [Fact(Skip = "Requires live network connection to Tribler network")]
    public async Task CircuitManager_Should_Recover_Failed_Circuits()
    {
        // Arrange
        await _transport.StartAsync(0);
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 30);

        var networkClient = new CircuitNetworkClient(_transport, _mockLogger.Object);
        await networkClient.StartAsync();
        var circuitManager = new CircuitManager(_settings, networkClient, _mockLogger.Object);

        foreach (var peer in _bootstrapManager.PeerTable.Peers.Values)
        {
            circuitManager.AddPeer(peer);
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

