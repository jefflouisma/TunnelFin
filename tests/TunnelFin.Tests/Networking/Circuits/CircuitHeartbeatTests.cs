using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Networking.Circuits;

/// <summary>
/// Tests for CircuitHeartbeat (T043).
/// Validates keepalive timer and timeout detection.
/// </summary>
public class CircuitHeartbeatTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly CircuitManager _circuitManager;
    private readonly CircuitHeartbeat _heartbeat;

    public CircuitHeartbeatTests()
    {
        _mockLogger = new Mock<ILogger>();
        var settings = new AnonymitySettings
        {
            DefaultHopCount = 2,
            MinHopCount = 1,
            MaxHopCount = 3,
            CircuitEstablishmentTimeoutSeconds = 30
        };
        _circuitManager = new CircuitManager(settings);
        _heartbeat = new CircuitHeartbeat(_circuitManager, _mockLogger.Object, intervalSeconds: 1, timeoutSeconds: 3);
    }

    [Fact]
    public void Start_Should_Start_Heartbeat_Timer()
    {
        // Act
        _heartbeat.Start();

        // Assert - No exception thrown
        _heartbeat.Stop();
    }

    [Fact]
    public void Stop_Should_Stop_Heartbeat_Timer()
    {
        // Arrange
        _heartbeat.Start();

        // Act
        _heartbeat.Stop();

        // Assert - No exception thrown
    }

    [Fact]
    public async Task CircuitTimedOut_Event_Should_Fire_When_Circuit_Times_Out()
    {
        // Arrange
        AddTestRelayPeers(5);
        var circuit = await _circuitManager.CreateCircuitAsync(2);
        circuit.MarkEstablished();

        // Simulate old last activity time (5 seconds ago, timeout is 3 seconds)
        var oldActivityTime = DateTime.UtcNow.AddSeconds(-5);
        typeof(Circuit).GetProperty("LastActivityAt")!.SetValue(circuit, oldActivityTime);

        var timeoutFired = false;
        Circuit? timedOutCircuit = null;

        _heartbeat.CircuitTimedOut += (sender, e) =>
        {
            timeoutFired = true;
            timedOutCircuit = e.Circuit;
        };

        _heartbeat.Start();

        // Act - Wait for heartbeat to check (interval is 1 second)
        await Task.Delay(1500);

        // Assert
        timeoutFired.Should().BeTrue();
        timedOutCircuit.Should().NotBeNull();
        timedOutCircuit!.State.Should().Be(CircuitState.Failed);
        timedOutCircuit.ErrorMessage.Should().Contain("timed out");

        _heartbeat.Stop();
    }

    [Fact]
    public async Task CircuitTimedOut_Event_Should_Not_Fire_For_Active_Circuits()
    {
        // Arrange
        AddTestRelayPeers(5);
        var circuit = await _circuitManager.CreateCircuitAsync(2);
        circuit.MarkEstablished();

        var timeoutFired = false;

        _heartbeat.CircuitTimedOut += (sender, e) =>
        {
            timeoutFired = true;
        };

        _heartbeat.Start();

        // Act - Wait for heartbeat to check (interval is 1 second)
        await Task.Delay(1500);

        // Assert - Circuit is active, should not timeout
        timeoutFired.Should().BeFalse();
        circuit.State.Should().Be(CircuitState.Established);

        _heartbeat.Stop();
    }

    [Fact]
    public async Task CircuitTimedOut_Event_Should_Not_Fire_For_Failed_Circuits()
    {
        // Arrange
        AddTestRelayPeers(5);
        var circuit = await _circuitManager.CreateCircuitAsync(2);
        circuit.MarkFailed("Test failure");

        // Simulate old last activity time
        var oldActivityTime = DateTime.UtcNow.AddSeconds(-5);
        typeof(Circuit).GetProperty("LastActivityAt")!.SetValue(circuit, oldActivityTime);

        var timeoutFired = false;

        _heartbeat.CircuitTimedOut += (sender, e) =>
        {
            timeoutFired = true;
        };

        _heartbeat.Start();

        // Act - Wait for heartbeat to check
        await Task.Delay(1500);

        // Assert - Circuit is already failed, should not timeout again
        timeoutFired.Should().BeFalse();
        circuit.State.Should().Be(CircuitState.Failed);

        _heartbeat.Stop();
    }

    private void AddTestRelayPeers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var publicKey = new byte[32];
            Random.Shared.NextBytes(publicKey);
            var ipv4 = (uint)Random.Shared.Next(0x01000000, 0x7FFFFFFF);
            var port = (ushort)Random.Shared.Next(1024, 65535);

            var peer = new Peer(publicKey, ipv4, port)
            {
                IsRelayCandidate = true,
                IsHandshakeComplete = true,
                SuccessCount = 10 // High reliability
            };

            _circuitManager.AddPeer(peer);
        }
    }

    public void Dispose()
    {
        _heartbeat.Dispose();
        _circuitManager.Dispose();
    }
}

