using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Tests.Networking.Bootstrap;

public class BootstrapManagerTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger = new();
    private readonly Mock<ITransport> _mockTransport = new();
    private readonly List<BootstrapManager> _managers = new();

    [Fact]
    public void BootstrapManager_Should_Initialize_With_Defaults()
    {
        var manager = CreateManager();

        manager.Status.Should().Be(BootstrapStatus.NotStarted);
        manager.PeerTable.Should().NotBeNull();
        manager.BootstrapNodes.Should().HaveCount(14); // Default TU Delft nodes
    }

    [Fact]
    public void BootstrapManager_Should_Initialize_With_Custom_PeerTable()
    {
        var customTable = new PeerTable(minimumPeerCount: 10);
        var manager = CreateManager(peerTable: customTable);

        manager.PeerTable.Should().BeSameAs(customTable);
        manager.PeerTable.MinimumPeerCount.Should().Be(10);
    }

    [Fact]
    public void BootstrapManager_Should_Initialize_With_Custom_Nodes()
    {
        var customNodes = new List<BootstrapNode>
        {
            new() { Address = "127.0.0.1", Port = 6421 }
        };

        var manager = CreateManager(bootstrapNodes: customNodes);

        manager.BootstrapNodes.Should().HaveCount(1);
        manager.BootstrapNodes[0].Address.Should().Be("127.0.0.1");
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Logger()
    {
        var act = () => new BootstrapManager(null!, _mockTransport.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Transport()
    {
        var act = () => new BootstrapManager(_mockLogger.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_Bootstrap_Node()
    {
        var invalidNodes = new List<BootstrapNode>
        {
            new() { Address = "invalid", Port = 6421 }
        };

        var act = () => CreateManager(bootstrapNodes: invalidNodes);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Update_Status()
    {
        var manager = CreateManager();

        await manager.DiscoverPeersAsync(timeoutSeconds: 1);

        manager.Status.Should().Be(BootstrapStatus.Ready);
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Mark_Peer_Table_Refreshed()
    {
        var manager = CreateManager();

        await manager.DiscoverPeersAsync(timeoutSeconds: 1);

        manager.PeerTable.LastRefresh.Should().NotBeNull();
        manager.PeerTable.LastRefresh.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Throw_On_Invalid_Timeout()
    {
        var manager = CreateManager();

        var act = async () => await manager.DiscoverPeersAsync(timeoutSeconds: 0);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Respect_Cancellation()
    {
        var manager = CreateManager();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await manager.DiscoverPeersAsync(cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RefreshPeersAsync_Should_Skip_If_Not_Needed()
    {
        var manager = CreateManager();

        // Mark as recently refreshed
        manager.PeerTable.MarkRefreshed();

        await manager.RefreshPeersAsync();

        // Should complete without error
        manager.Status.Should().Be(BootstrapStatus.NotStarted);
    }

    [Fact]
    public async Task RefreshPeersAsync_Should_Update_If_Needed()
    {
        var table = new PeerTable(refreshIntervalSeconds: 1);
        var manager = CreateManager(peerTable: table);

        // Wait for refresh to be needed
        await Task.Delay(1100);

        await manager.RefreshPeersAsync();

        table.LastRefresh.Should().NotBeNull();
    }

    [Fact]
    public async Task StartPeriodicRefreshAsync_Should_Respect_Cancellation()
    {
        var table = new PeerTable(refreshIntervalSeconds: 1);
        var manager = CreateManager(peerTable: table);
        var cts = new CancellationTokenSource();

        var refreshTask = manager.StartPeriodicRefreshAsync(cts.Token);

        // Cancel after short delay
        await Task.Delay(100);
        cts.Cancel();

        // Should complete without throwing
        await refreshTask;
    }

    private BootstrapManager CreateManager(
        IPeerTable? peerTable = null,
        List<BootstrapNode>? bootstrapNodes = null)
    {
        var manager = new BootstrapManager(_mockLogger.Object, _mockTransport.Object, peerTable, bootstrapNodes);
        _managers.Add(manager);
        return manager;
    }

    public void Dispose()
    {
        _managers.Clear();
    }
}

