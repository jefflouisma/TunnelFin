using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Unit.Networking;

public class CircuitConnectionPoolTests
{
    private readonly CircuitManager _circuitManager;
    private readonly ILogger _logger;
    private readonly AnonymitySettings _settings;
    private readonly CircuitConnectionPool _pool;

    public CircuitConnectionPoolTests()
    {
        _settings = new AnonymitySettings
        {
            MaxConcurrentCircuits = 10,
            MinConcurrentCircuits = 2,
            DefaultHopCount = 1
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<CircuitConnectionPoolTests>();
        _circuitManager = new CircuitManager(_settings);

        // Add peers for circuit creation
        for (int i = 0; i < 5; i++)
        {
            var publicKey = new byte[32];
            publicKey[0] = (byte)(i + 1);
            var peer = new Peer(publicKey, (uint)(0x7F000001 + i), (ushort)(8000 + i));
            peer.IsHandshakeComplete = true;
            peer.IsRelayCandidate = true;
            _circuitManager.AddPeer(peer);
        }

        _pool = new CircuitConnectionPool(
            _circuitManager,
            _settings,
            _logger);
    }

    [Fact]
    public async Task GetConnectionAsync_PoolHasHealthyCircuit_ReturnsCircuitFromPool()
    {
        // Arrange
        var circuit = CreateEstablishedCircuit();

        // Add circuit to manager using reflection
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit.IPv8CircuitId] = circuit;

        // Act
        var result = await _pool.GetConnectionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IPv8CircuitId.Should().Be(circuit.IPv8CircuitId);
    }

    [Fact]
    public async Task GetConnectionAsync_PoolEmpty_CreatesNewCircuit()
    {
        // Arrange
        // Add a peer so circuit creation can succeed
        var peer = new Peer(new byte[32], 0x7F000001, 8000);
        peer.IsHandshakeComplete = true;
        peer.IsRelayCandidate = true;
        _circuitManager.AddPeer(peer);

        // Act
        var result = await _pool.GetConnectionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(CircuitState.Established);
    }

    [Fact]
    public async Task ReturnConnection_HealthyCircuit_ReturnsToPool()
    {
        // Arrange
        var circuit = await _pool.GetConnectionAsync(CancellationToken.None);

        // Act
        _pool.ReturnConnection(circuit, healthy: true);

        // Assert
        // Circuit should be available in pool for next GetConnectionAsync
        _pool.AvailableCount.Should().Be(1);
        _pool.InUseCount.Should().Be(0);
    }

    [Fact]
    public async Task ReturnConnection_UnhealthyCircuit_DisposesCircuit()
    {
        // Arrange
        var circuit = await _pool.GetConnectionAsync(CancellationToken.None);

        // Act
        _pool.ReturnConnection(circuit, healthy: false);

        // Assert
        // Circuit should NOT be available in pool
        _pool.AvailableCount.Should().Be(0);
        _pool.InUseCount.Should().Be(0);
    }

    [Fact]
    public async Task GetConnectionAsync_MaxConcurrentReached_WaitsForAvailableCircuit()
    {
        // Arrange
        _settings.MaxConcurrentCircuits = 2;

        // Add peers for circuit creation
        for (int i = 0; i < 3; i++)
        {
            var publicKey = new byte[32];
            publicKey[0] = (byte)(i + 1);
            var peer = new Peer(publicKey, (uint)(0x7F000001 + i), (ushort)(8000 + i));
            peer.IsHandshakeComplete = true;
            peer.IsRelayCandidate = true;
            _circuitManager.AddPeer(peer);
        }

        // Act - Get both circuits
        var result1 = await _pool.GetConnectionAsync(CancellationToken.None);
        var result2 = await _pool.GetConnectionAsync(CancellationToken.None);

        // Try to get a third (should wait for semaphore, then fail when trying to create circuit)
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var act = async () => await _pool.GetConnectionAsync(cts.Token);

        // Assert
        // Should timeout waiting for semaphore OR get InvalidOperationException from CircuitManager
        await act.Should().ThrowAsync<Exception>()
            .Where(ex => ex is OperationCanceledException || ex is InvalidOperationException);
    }

    [Fact]
    public async Task GetConnectionAsync_AllCircuitsUnhealthy_CreatesNewCircuit()
    {
        // Arrange
        var expiredCircuit = CreateExpiredCircuit();

        // Add expired circuit to manager
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[expiredCircuit.IPv8CircuitId] = expiredCircuit;

        // Add a peer so new circuit creation can succeed
        var peer = new Peer(new byte[32], 0x7F000001, 8000);
        peer.IsHandshakeComplete = true;
        peer.IsRelayCandidate = true;
        _circuitManager.AddPeer(peer);

        // Act
        var result = await _pool.GetConnectionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.State.Should().Be(CircuitState.Established);
        result.IPv8CircuitId.Should().NotBe(expiredCircuit.IPv8CircuitId);
    }

    [Fact]
    public void ReturnConnection_NullCircuit_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _pool.ReturnConnection(null!, healthy: true);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private Circuit CreateEstablishedCircuit()
    {
        var circuit = new Circuit((uint)Random.Shared.Next(1, 100000), 3, 600);

        // Add 3 hops to make it established
        for (int i = 0; i < 3; i++)
        {
            // Create ephemeral key pair for this hop
            var ephemeralPrivateKey = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519);
            var ephemeralPublicKey = ephemeralPrivateKey.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

            var publicKey = new byte[32];
            publicKey[0] = (byte)(i + 1);
            var hop = new HopNode(publicKey, (uint)(0x7F000001 + i), (ushort)(8000 + i), i);
            hop.CompleteKeyExchange(ephemeralPublicKey, ephemeralPrivateKey);
            circuit.AddHop(hop);
        }

        return circuit;
    }

    private Circuit CreateExpiredCircuit()
    {
        var circuit = new Circuit(12345, 1, 60); // 60 second lifetime (minimum)

        // Add 1 hop to make it valid
        var ephemeralPrivateKey = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519);
        var ephemeralPublicKey = ephemeralPrivateKey.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

        var publicKey = new byte[32];
        publicKey[0] = 99;
        var hop = new HopNode(publicKey, 0x7F000001, 8000, 0);
        hop.CompleteKeyExchange(ephemeralPublicKey, ephemeralPrivateKey);
        circuit.AddHop(hop);

        // Manually set expiration to past using reflection (property with private setter)
        var expiresAtProperty = typeof(Circuit).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(circuit, DateTime.UtcNow.AddSeconds(-1));

        return circuit;
    }
}

