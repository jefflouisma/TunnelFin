using FluentAssertions;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Unit.Networking;

/// <summary>
/// Unit tests for NetworkAvailabilityService (T111-T112).
/// Tests circuit availability checking and status change events.
/// </summary>
public class NetworkAvailabilityServiceTests
{
    private readonly ILogger<NetworkAvailabilityService> _logger;
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;

    public NetworkAvailabilityServiceTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<NetworkAvailabilityService>();
        
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 1,
            CircuitLifetimeSeconds = 600,
            MaxConcurrentCircuits = 10
        };

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
    }

    /// <summary>
    /// T111: Verify CheckNetworkAvailabilityAsync returns true when circuits exist.
    /// </summary>
    [Fact]
    public async Task CheckNetworkAvailabilityAsync_WithEstablishedCircuits_ReturnsTrue()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);
        
        // Create an established circuit
        var circuit = await CreateEstablishedCircuitAsync();

        // Act
        var isAvailable = await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        isAvailable.Should().BeTrue("because an established circuit exists");
        service.AvailableCircuitCount.Should().Be(1);
    }

    /// <summary>
    /// T111: Verify CheckNetworkAvailabilityAsync returns false when no circuits exist.
    /// </summary>
    [Fact]
    public async Task CheckNetworkAvailabilityAsync_WithNoCircuits_ReturnsFalse()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);

        // Act
        var isAvailable = await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        isAvailable.Should().BeFalse("because no circuits exist");
        service.AvailableCircuitCount.Should().Be(0);
    }

    /// <summary>
    /// T111: Verify CheckNetworkAvailabilityAsync returns false when circuits are expired.
    /// </summary>
    [Fact]
    public async Task CheckNetworkAvailabilityAsync_WithExpiredCircuits_ReturnsFalse()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);
        
        // Create an expired circuit
        var circuit = await CreateEstablishedCircuitAsync();
        
        // Force circuit expiration using reflection
        var expiresAtProperty = typeof(Circuit).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(circuit, DateTime.UtcNow.AddSeconds(-1));

        // Act
        var isAvailable = await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        isAvailable.Should().BeFalse("because the circuit is expired");
        service.AvailableCircuitCount.Should().Be(0);
    }

    /// <summary>
    /// T112: Verify status change event fires when availability changes from false to true.
    /// </summary>
    [Fact]
    public async Task StatusChanged_WhenCircuitCreated_FiresEvent()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);
        var eventFired = false;
        var eventAvailability = false;

        service.StatusChanged += (sender, isAvailable) =>
        {
            eventFired = true;
            eventAvailability = isAvailable;
        };

        // Initial check (no circuits)
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Act - Create a circuit
        await CreateEstablishedCircuitAsync();
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        eventFired.Should().BeTrue("because availability changed from false to true");
        eventAvailability.Should().BeTrue("because circuits are now available");
    }

    /// <summary>
    /// T112: Verify status change event fires when availability changes from true to false.
    /// </summary>
    [Fact]
    public async Task StatusChanged_WhenCircuitExpires_FiresEvent()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);
        var eventFired = false;
        var eventAvailability = true;

        // Create a circuit first
        var circuit = await CreateEstablishedCircuitAsync();
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        service.StatusChanged += (sender, isAvailable) =>
        {
            eventFired = true;
            eventAvailability = isAvailable;
        };

        // Act - Expire the circuit
        var expiresAtProperty = typeof(Circuit).GetProperty("ExpiresAt");
        expiresAtProperty!.SetValue(circuit, DateTime.UtcNow.AddSeconds(-1));
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        eventFired.Should().BeTrue("because availability changed from true to false");
        eventAvailability.Should().BeFalse("because all circuits are now expired");
    }

    /// <summary>
    /// T112: Verify status change event does not fire when availability remains the same.
    /// </summary>
    [Fact]
    public async Task StatusChanged_WhenAvailabilityUnchanged_DoesNotFireEvent()
    {
        // Arrange
        var service = new NetworkAvailabilityService(_circuitManager, _settings, _logger);
        var eventFiredCount = 0;

        service.StatusChanged += (sender, isAvailable) =>
        {
            eventFiredCount++;
        };

        // Act - Check multiple times with no circuits
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);
        await service.CheckNetworkAvailabilityAsync(CancellationToken.None);

        // Assert
        eventFiredCount.Should().Be(0, "because availability remained false throughout");
    }

    /// <summary>
    /// Helper method to create an established circuit.
    /// </summary>
    private async Task<Circuit> CreateEstablishedCircuitAsync()
    {
        var circuit = await _circuitManager.CreateCircuitAsync(1);

        // Ensure circuit is established
        if (circuit.State != CircuitState.Established)
        {
            // Wait for circuit to establish
            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;
            while (circuit.State == CircuitState.Creating && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(100);
            }
        }

        circuit.State.Should().Be(CircuitState.Established);
        return circuit;
    }
}


