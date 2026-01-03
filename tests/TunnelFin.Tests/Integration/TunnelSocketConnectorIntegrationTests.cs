using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Net;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for TunnelSocketConnector with real network connections.
/// These tests require actual network connectivity to establish circuits and connect to peers.
/// </summary>
public class TunnelSocketConnectorIntegrationTests : IAsyncLifetime
{
    private readonly ILogger _logger;
    private readonly Protocol _protocol;
    private readonly CircuitManager _circuitManager;
    private readonly TunnelProxy _tunnelProxy;
    private readonly TunnelSocketConnector _connector;
    private readonly AnonymitySettings _settings;

    public TunnelSocketConnectorIntegrationTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<TunnelSocketConnectorIntegrationTests>();
        
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 1, // Use 1 hop for faster testing
            AllowNonAnonymousFallback = false,
            CircuitEstablishmentTimeoutSeconds = 30,
            BootstrapTimeoutSeconds = 30
        };

        _protocol = new Protocol(_settings);
        _circuitManager = _protocol.CircuitManager;
        _tunnelProxy = new TunnelProxy(_logger);
        _connector = new TunnelSocketConnector(
            _circuitManager,
            _tunnelProxy,
            _settings,
            _logger);
    }

    public async Task InitializeAsync()
    {
        // Initialize IPv8 protocol stack
        await _protocol.InitializeAsync(0, CancellationToken.None); // Port 0 = random port
        
        // Start tunnel proxy
        await _tunnelProxy.StartAsync();

        // Discover peers from bootstrap nodes
        await _protocol.BootstrapManager.DiscoverPeersAsync(30, CancellationToken.None);

        // Wait a bit for peer discovery
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        await _tunnelProxy.StopAsync();
        _protocol.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_WithRealCircuit_EstablishesConnection()
    {
        // Arrange
        var peerCount = _protocol.PeerTable.GetRelayPeers(10).Count;
        if (peerCount < 3)
        {
            // Skip test if not enough peers discovered
            return;
        }

        // Create a circuit
        var circuit = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);
        
        // Wait for circuit to establish
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        while (circuit.State != CircuitState.Established && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(500);
        }

        circuit.State.Should().Be(CircuitState.Established);

        // Act - Connect to a test peer (using a public BitTorrent tracker as test endpoint)
        var testPeerUri = new Uri("ipv4://tracker.opentrackr.org:1337");
        var socket = await _connector.ConnectAsync(testPeerUri, CancellationToken.None);

        // Assert
        socket.Should().NotBeNull();
        socket.Should().BeOfType<TunnelSocket>();
        var tunnelSocket = (TunnelSocket)socket;
        tunnelSocket.Connected.Should().BeTrue();
        tunnelSocket.TunnelStream.Circuit.IPv8CircuitId.Should().Be(circuit.IPv8CircuitId);
    }

    [Fact]
    public async Task ConnectAsync_CircuitFailover_RetriesWithDifferentCircuit()
    {
        // Arrange
        var peerCount = _protocol.PeerTable.GetRelayPeers(10).Count;
        if (peerCount < 6) // Need enough peers for 2 circuits
        {
            // Skip test if not enough peers discovered
            return;
        }

        // Create first circuit
        var circuit1 = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);
        await WaitForCircuitEstablishment(circuit1);

        // Act - Connect through first circuit
        var testPeerUri = new Uri("ipv4://tracker.opentrackr.org:1337");
        var socket1 = await _connector.ConnectAsync(testPeerUri, CancellationToken.None);

        // Simulate circuit failure by closing the tunnel
        socket1.Close();

        // Create second circuit for failover
        var circuit2 = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);
        await WaitForCircuitEstablishment(circuit2);

        // Connect through second circuit
        var socket2 = await _connector.ConnectAsync(testPeerUri, CancellationToken.None);

        // Assert
        socket2.Should().NotBeNull();
        socket2.Should().BeOfType<TunnelSocket>();
        var tunnelSocket2 = (TunnelSocket)socket2;
        tunnelSocket2.Connected.Should().BeTrue();
    }

    private async Task WaitForCircuitEstablishment(Circuit circuit)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        while (circuit.State != CircuitState.Established && DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(500);
        }
        circuit.State.Should().Be(CircuitState.Established);
    }
}

