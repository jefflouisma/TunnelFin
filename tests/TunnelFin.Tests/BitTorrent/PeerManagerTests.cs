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

