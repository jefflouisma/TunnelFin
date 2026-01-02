using FluentAssertions;
using Moq;
using TunnelFin.BitTorrent;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using Xunit;

namespace TunnelFin.Tests.BitTorrent;

/// <summary>
/// Unit tests for PeerManager (FR-002, FR-007).
/// Tests peer connection management and routing through anonymity circuits.
/// </summary>
public class PeerManagerTests
{
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;

    public PeerManagerTests()
    {
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 3,
            MaxConcurrentCircuits = 10
        };
        _circuitManager = new CircuitManager(_settings);
    }

    [Fact]
    public async Task AddPeer_Should_Route_Through_Circuit()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        // Create a circuit first - need multiple peers for circuit creation
        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        // Act
        var result = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);

        // Assert
        result.Should().BeTrue("peer should be added successfully");
        manager.GetPeerCount(torrentId).Should().Be(1);
        manager.ActiveConnectionCount.Should().Be(1);
    }

    [Fact]
    public async Task AddPeer_Should_Reject_When_Max_Peers_Reached()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 2);
        var torrentId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        // Act
        await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);
        await manager.AddPeerAsync(torrentId, "10.0.0.2", 6883, circuit.CircuitId);
        var result = await manager.AddPeerAsync(torrentId, "10.0.0.3", 6884, circuit.CircuitId);

        // Assert
        result.Should().BeFalse("should reject when max peers reached");
        manager.GetPeerCount(torrentId).Should().Be(2);
    }

    [Fact]
    public async Task AddPeer_Should_Prevent_Duplicate_Connections()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        // Act
        var result1 = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);
        var result2 = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);

        // Assert
        result1.Should().BeTrue("first connection should succeed");
        result2.Should().BeFalse("duplicate connection should be rejected");
        manager.GetPeerCount(torrentId).Should().Be(1);
    }

    [Fact]
    public async Task RemovePeer_Should_Cleanup_Connection()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);
        await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);

        // Act
        manager.RemovePeer("10.0.0.1", 6882);

        // Assert
        manager.GetPeerCount(torrentId).Should().Be(0);
        manager.ActiveConnectionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPeers_Should_Return_Torrent_Peers()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);
        await manager.AddPeerAsync(torrentId, "10.0.0.2", 6883, circuit.CircuitId);

        // Act
        var peers = manager.GetPeers(torrentId);

        // Assert
        peers.Should().HaveCount(2);
        peers.Should().AllSatisfy(p => p.TorrentId.Should().Be(torrentId));
    }

    [Fact]
    public async Task RemoveTorrent_Should_Remove_All_Peers()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882, circuit.CircuitId);
        await manager.AddPeerAsync(torrentId, "10.0.0.2", 6883, circuit.CircuitId);

        // Act
        manager.RemoveTorrent(torrentId);

        // Assert
        manager.GetPeerCount(torrentId).Should().Be(0);
        manager.ActiveConnectionCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_Should_Throw_When_CircuitManager_Is_Null()
    {
        // Arrange & Act
        var act = () => new PeerManager(null!, maxPeersPerTorrent: 50);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("circuitManager");
    }

    [Fact]
    public void Constructor_Should_Throw_When_MaxPeersPerTorrent_Is_Less_Than_1()
    {
        // Arrange & Act
        var act = () => new PeerManager(_circuitManager, maxPeersPerTorrent: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least 1*");
    }

    [Fact]
    public void Constructor_Should_Set_MaxPeersPerTorrent()
    {
        // Arrange & Act
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 100);

        // Assert
        manager.MaxPeersPerTorrent.Should().Be(100);
    }

    [Fact]
    public async Task AddPeer_Should_Use_Existing_Circuit_When_Available()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        // Add peers and create circuit
        for (int i = 1; i <= 3; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        // Act - Should use existing circuit
        var result = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882);

        // Assert
        result.Should().BeTrue("should use existing circuit and add peer");
        manager.GetPeerCount(torrentId).Should().Be(1);
    }

    [Fact]
    public async Task AddPeer_Should_Return_False_When_Circuit_Creation_Fails()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();
        // No peers available for circuit creation

        // Act
        var result = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882);

        // Assert
        result.Should().BeFalse("should fail when no circuits available");
    }

    [Fact]
    public async Task AddPeer_Should_Throw_When_Disposed()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        manager.Dispose();

        // Act
        var act = async () => await manager.AddPeerAsync(Guid.NewGuid(), "10.0.0.1", 6882);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void GetPeerCount_Should_Return_Zero_For_Unknown_Torrent()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);

        // Act
        var count = manager.GetPeerCount(Guid.NewGuid());

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void GetPeers_Should_Return_Empty_For_Unknown_Torrent()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);

        // Act
        var peers = manager.GetPeers(Guid.NewGuid());

        // Assert
        peers.Should().BeEmpty();
    }

    [Fact]
    public void RemovePeer_Should_Handle_Non_Existent_Peer()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);

        // Act
        var act = () => manager.RemovePeer("10.0.0.1", 6882);

        // Assert
        act.Should().NotThrow("removing non-existent peer should be safe");
    }

    [Fact]
    public void Dispose_Should_Clear_All_Connections()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);

        // Act
        manager.Dispose();

        // Assert
        manager.ActiveConnectionCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_Should_Allow_Multiple_Calls()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);

        // Act
        manager.Dispose();
        var act = () => manager.Dispose();

        // Assert
        act.Should().NotThrow("multiple dispose calls should be safe");
    }

    [Fact]
    public async Task AddPeer_Should_Use_Best_Circuit_When_Multiple_Available()
    {
        // Arrange
        var manager = new PeerManager(_circuitManager, maxPeersPerTorrent: 50);
        var torrentId = Guid.NewGuid();

        // Create multiple circuits
        for (int i = 1; i <= 5; i++)
        {
            var peer = CreateTestPeer($"192.168.1.{i}", 6881);
            _circuitManager.AddPeer(peer);
        }
        var circuit1 = await _circuitManager.CreateCircuitAsync(hopCount: 1);
        var circuit2 = await _circuitManager.CreateCircuitAsync(hopCount: 1);

        // Act - Should select circuit with fewest connections
        var result1 = await manager.AddPeerAsync(torrentId, "10.0.0.1", 6882);
        var result2 = await manager.AddPeerAsync(torrentId, "10.0.0.2", 6883);

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        manager.GetPeerCount(torrentId).Should().Be(2);
    }

    private TunnelFin.Networking.IPv8.Peer CreateTestPeer(string ip, int port)
    {
        // Convert IP string to uint (big-endian)
        var parts = ip.Split('.');
        uint ipv4 = (uint.Parse(parts[0]) << 24) |
                    (uint.Parse(parts[1]) << 16) |
                    (uint.Parse(parts[2]) << 8) |
                    uint.Parse(parts[3]);

        var peer = new TunnelFin.Networking.IPv8.Peer(
            new byte[32], // public key
            ipv4,
            (ushort)port
        );

        // Mark handshake as complete so peer can be used for circuits
        peer.IsHandshakeComplete = true;

        return peer;
    }
}

