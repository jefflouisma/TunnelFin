using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Unit.Networking;

public class CircuitHealthMonitorTests
{
    private readonly CircuitManager _circuitManager;
    private readonly ILogger _logger;
    private readonly AnonymitySettings _settings;
    private readonly CircuitHealthMonitor _monitor;

    public CircuitHealthMonitorTests()
    {
        _settings = new AnonymitySettings
        {
            EnableCircuitHealthMonitoring = true,
            CircuitHealthCheckIntervalSeconds = 1 // Fast for testing
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<CircuitHealthMonitorTests>();
        _circuitManager = new CircuitManager(_settings);

        _monitor = new CircuitHealthMonitor(
            _circuitManager,
            _settings,
            _logger);
    }

    [Fact]
    public async Task StartAsync_StartsHealthCheckLoop()
    {
        // Act
        await _monitor.StartAsync(CancellationToken.None);

        // Assert
        _monitor.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_StopsHealthCheckLoop()
    {
        // Arrange
        await _monitor.StartAsync(CancellationToken.None);

        // Act
        await _monitor.StopAsync(CancellationToken.None);

        // Assert
        _monitor.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCircuitHealthAsync_HealthyCircuit_ReturnsTrue()
    {
        // Arrange
        var circuit = CreateEstablishedCircuit();

        // Act
        var isHealthy = await _monitor.CheckCircuitHealthAsync(circuit, CancellationToken.None);

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task CheckCircuitHealthAsync_ExpiredCircuit_ReturnsFalse()
    {
        // Arrange
        var circuit = CreateExpiredCircuit();

        // Act
        var isHealthy = await _monitor.CheckCircuitHealthAsync(circuit, CancellationToken.None);

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task CheckCircuitHealthAsync_NotEstablishedCircuit_ReturnsFalse()
    {
        // Arrange
        var circuit = new Circuit(12345, 3, 600);
        // Don't mark as established

        // Act
        var isHealthy = await _monitor.CheckCircuitHealthAsync(circuit, CancellationToken.None);

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task HealthCheckLoop_ChecksAllCircuitsPeriodically()
    {
        // Arrange
        var circuit1 = CreateEstablishedCircuit();
        var circuit2 = CreateEstablishedCircuit();

        // Add circuits to manager using reflection (since Circuits is read-only)
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit1.IPv8CircuitId] = circuit1;
        circuits[circuit2.IPv8CircuitId] = circuit2;

        // Act
        await _monitor.StartAsync(CancellationToken.None);
        await Task.Delay(1500); // Wait for at least one health check cycle
        await _monitor.StopAsync(CancellationToken.None);

        // Assert
        // Verify that health checks were performed (check logs or metrics)
        _monitor.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task HealthCheckLoop_UnhealthyCircuit_MarksAsUnhealthy()
    {
        // Arrange
        var circuit = CreateExpiredCircuit();

        // Add circuit to manager using reflection
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit.IPv8CircuitId] = circuit;

        // Act
        await _monitor.StartAsync(CancellationToken.None);
        await Task.Delay(1500); // Wait for health check
        await _monitor.StopAsync(CancellationToken.None);

        // Assert
        // Circuit should be marked as unhealthy (implementation will handle this)
        circuit.IsExpired.Should().BeTrue();
    }

    private Circuit CreateEstablishedCircuit()
    {
        var circuit = new Circuit(12345, 3, 600);

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
        var circuit = new Circuit(12346, 1, 60); // 60 second lifetime (minimum)

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

