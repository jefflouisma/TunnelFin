using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Transport;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for bootstrap node discovery (T063, SC-001).
/// Tests real network communication with TU Delft bootstrap nodes.
/// </summary>
public class BootstrapIntegrationTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly UdpTransport _transport;
    private readonly BootstrapManager _bootstrapManager;

    public BootstrapIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _transport = new UdpTransport(_mockLogger.Object);
        _bootstrapManager = new BootstrapManager(_mockLogger.Object, _transport);
    }

    [Fact]
    public async Task BootstrapManager_Should_Contact_Real_Bootstrap_Nodes()
    {
        // Arrange
        await _transport.StartAsync(0); // Random port

        // Act
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);

        // Assert
        _bootstrapManager.Status.Should().Be(BootstrapStatus.Ready,
            "Bootstrap should complete successfully");
        _bootstrapManager.PeerTable.Count.Should().BeGreaterThan(0,
            "Should discover at least one peer from bootstrap nodes");
    }

    [Fact]
    public async Task BootstrapManager_Should_Discover_Peers_Within_Timeout()
    {
        // Arrange
        await _transport.StartAsync(0);
        var startTime = DateTime.UtcNow;

        // Act
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7),
            "Bootstrap discovery should complete within timeout + buffer");
        _bootstrapManager.Status.Should().Be(BootstrapStatus.Ready);
    }

    [Fact]
    public async Task BootstrapManager_Should_Refresh_Peer_Table()
    {
        // Arrange
        await _transport.StartAsync(0);
        await _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);
        var initialCount = _bootstrapManager.PeerTable.Count;

        // Act
        await _bootstrapManager.RefreshPeersAsync();

        // Assert
        _bootstrapManager.PeerTable.Count.Should().BeGreaterThanOrEqualTo(initialCount,
            "Peer table should maintain or increase peer count after refresh");
    }

    [Fact]
    public async Task BootstrapManager_Should_Have_Valid_Bootstrap_Nodes()
    {
        // Arrange & Act
        var bootstrapNodes = _bootstrapManager.BootstrapNodes;

        // Assert
        bootstrapNodes.Should().NotBeEmpty("Should have at least one bootstrap node");
        bootstrapNodes.Should().AllSatisfy(node =>
        {
            node.Address.Should().NotBeNullOrWhiteSpace("Bootstrap node should have valid address");
            node.Port.Should().BeInRange(6421, 6528, "Bootstrap node port should be in TU Delft range");
        });
    }

    [Fact]
    public async Task BootstrapManager_Should_Initialize_With_NotStarted_Status()
    {
        // Arrange & Act
        var status = _bootstrapManager.Status;

        // Assert
        status.Should().Be(BootstrapStatus.NotStarted,
            "Bootstrap manager should start in NotStarted state");
    }

    [Fact]
    public async Task BootstrapManager_Should_Transition_To_Contacting_Status()
    {
        // Arrange
        await _transport.StartAsync(0);

        // Act
        var discoveryTask = _bootstrapManager.DiscoverPeersAsync(timeoutSeconds: 5);
        await Task.Delay(100); // Give it time to start

        // Assert
        var validStatuses = new[] { BootstrapStatus.Contacting, BootstrapStatus.Discovering, BootstrapStatus.Ready };
        validStatuses.Should().Contain(_bootstrapManager.Status,
            "Bootstrap should be in progress or completed");

        await discoveryTask; // Wait for completion
    }

    public void Dispose()
    {
        _transport?.Dispose();
    }
}

