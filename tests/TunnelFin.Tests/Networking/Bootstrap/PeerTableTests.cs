using FluentAssertions;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Tests.Networking.Bootstrap;

public class PeerTableTests
{
    [Fact]
    public void PeerTable_Should_Initialize_With_Defaults()
    {
        var table = new PeerTable();

        table.Count.Should().Be(0);
        table.MinimumPeerCount.Should().Be(20);
        table.MaxPeerCount.Should().Be(200);
        table.RefreshIntervalSeconds.Should().Be(300);
        table.LastRefresh.Should().BeNull();
    }

    [Fact]
    public void PeerTable_Should_Initialize_With_Custom_Values()
    {
        var table = new PeerTable(minimumPeerCount: 10, maxPeerCount: 100, refreshIntervalSeconds: 60);

        table.MinimumPeerCount.Should().Be(10);
        table.MaxPeerCount.Should().Be(100);
        table.RefreshIntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_MinimumPeerCount()
    {
        var act = () => new PeerTable(minimumPeerCount: 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_MaxPeerCount()
    {
        var act = () => new PeerTable(minimumPeerCount: 20, maxPeerCount: 10);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Should_Throw_On_Invalid_RefreshInterval()
    {
        var act = () => new PeerTable(refreshIntervalSeconds: 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddPeer_Should_Add_New_Peer()
    {
        var table = new PeerTable();
        var peer = CreateTestPeer();

        var added = table.AddPeer(peer);

        added.Should().BeTrue();
        table.Count.Should().Be(1);
    }

    [Fact]
    public void AddPeer_Should_Not_Add_Duplicate()
    {
        var table = new PeerTable();
        var peer = CreateTestPeer();

        table.AddPeer(peer);
        var added = table.AddPeer(peer);

        added.Should().BeFalse();
        table.Count.Should().Be(1);
    }

    [Fact]
    public void AddPeer_Should_Respect_MaxPeerCount()
    {
        var table = new PeerTable(minimumPeerCount: 1, maxPeerCount: 2);

        table.AddPeer(CreateTestPeer(1));
        table.AddPeer(CreateTestPeer(2));
        var added = table.AddPeer(CreateTestPeer(3));

        added.Should().BeFalse();
        table.Count.Should().Be(2);
    }

    [Fact]
    public void RemovePeer_Should_Remove_Existing_Peer()
    {
        var table = new PeerTable();
        var peer = CreateTestPeer();
        var publicKeyHex = Convert.ToHexString(peer.PublicKey).ToLowerInvariant();

        table.AddPeer(peer);
        var removed = table.RemovePeer(publicKeyHex);

        removed.Should().BeTrue();
        table.Count.Should().Be(0);
    }

    [Fact]
    public void RemovePeer_Should_Return_False_For_NonExistent_Peer()
    {
        var table = new PeerTable();
        var removed = table.RemovePeer("nonexistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public void GetPeer_Should_Return_Existing_Peer()
    {
        var table = new PeerTable();
        var peer = CreateTestPeer();
        var publicKeyHex = Convert.ToHexString(peer.PublicKey).ToLowerInvariant();

        table.AddPeer(peer);
        var retrieved = table.GetPeer(publicKeyHex);

        retrieved.Should().NotBeNull();
        retrieved.Should().BeSameAs(peer);
    }

    [Fact]
    public void GetPeer_Should_Return_Null_For_NonExistent_Peer()
    {
        var table = new PeerTable();
        var retrieved = table.GetPeer("nonexistent");

        retrieved.Should().BeNull();
    }

    [Fact]
    public void GetRelayPeers_Should_Return_High_Reliability_Peers()
    {
        var table = new PeerTable();

        // Add relay-capable peer with high reliability
        var goodPeer = CreateTestPeer(1);
        goodPeer.IsRelayCandidate = true;
        goodPeer.SuccessCount = 10;
        goodPeer.FailureCount = 1; // 0.91 reliability
        goodPeer.RttMs = 100;
        table.AddPeer(goodPeer);

        // Add non-relay peer
        var badPeer = CreateTestPeer(2);
        badPeer.IsRelayCandidate = false;
        table.AddPeer(badPeer);

        var relayPeers = table.GetRelayPeers(10);

        relayPeers.Should().HaveCount(1);
        relayPeers[0].Should().BeSameAs(goodPeer);
    }

    private static Peer CreateTestPeer(int seed = 1)
    {
        var publicKey = new byte[32];
        for (int i = 0; i < 32; i++)
            publicKey[i] = (byte)(seed + i);

        return new Peer(publicKey, 0x7F000001, 8000); // 127.0.0.1:8000
    }
}

