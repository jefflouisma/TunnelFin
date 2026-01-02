using FluentAssertions;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for Peer class.
/// Tests peer state management, reliability tracking, and validation.
/// </summary>
public class PeerTests
{
    [Fact]
    public void Constructor_Should_Initialize_Peer_With_Valid_Parameters()
    {
        // Arrange
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        uint ipv4Address = 0xC0A80101; // 192.168.1.1
        ushort port = 6881;

        // Act
        var peer = new Peer(publicKey, ipv4Address, port);

        // Assert
        peer.PublicKey.Should().BeEquivalentTo(publicKey);
        peer.IPv4Address.Should().Be(ipv4Address);
        peer.Port.Should().Be(port);
        peer.IsRelayCandidate.Should().BeTrue("new peers should be relay candidates by default");
        peer.IsHandshakeComplete.Should().BeFalse("handshake should not be complete initially");
        peer.DiscoveredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        peer.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_Should_Throw_When_PublicKey_Is_Null()
    {
        // Arrange & Act
        var act = () => new Peer(null!, 0xC0A80101, 6881);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publicKey");
    }

    [Fact]
    public void Constructor_Should_Throw_When_PublicKey_Is_Not_32_Bytes()
    {
        // Arrange & Act
        var act1 = () => new Peer(new byte[16], 0xC0A80101, 6881);
        var act2 = () => new Peer(new byte[64], 0xC0A80101, 6881);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void UpdateLastSeen_Should_Update_Timestamp()
    {
        // Arrange
        var peer = CreateTestPeer();
        var originalLastSeen = peer.LastSeenAt;
        Thread.Sleep(10); // Ensure time difference

        // Act
        peer.UpdateLastSeen();

        // Assert
        peer.LastSeenAt.Should().BeAfter(originalLastSeen);
    }

    [Fact]
    public void RecordSuccess_Should_Increment_SuccessCount_And_Update_LastSeen()
    {
        // Arrange
        var peer = CreateTestPeer();
        var originalLastSeen = peer.LastSeenAt;
        Thread.Sleep(10);

        // Act
        peer.RecordSuccess();

        // Assert
        peer.SuccessCount.Should().Be(1);
        peer.LastSeenAt.Should().BeAfter(originalLastSeen);
    }

    [Fact]
    public void RecordFailure_Should_Increment_FailureCount_And_Update_LastSeen()
    {
        // Arrange
        var peer = CreateTestPeer();
        var originalLastSeen = peer.LastSeenAt;
        Thread.Sleep(10);

        // Act
        peer.RecordFailure();

        // Assert
        peer.FailureCount.Should().Be(1);
        peer.LastSeenAt.Should().BeAfter(originalLastSeen);
    }

    [Fact]
    public void ReliabilityScore_Should_Be_0_5_When_No_Interactions()
    {
        // Arrange
        var peer = CreateTestPeer();

        // Act
        var score = peer.ReliabilityScore;

        // Assert
        score.Should().Be(0.5, "default reliability should be 50%");
    }

    [Fact]
    public void ReliabilityScore_Should_Calculate_Correctly_With_Successes()
    {
        // Arrange
        var peer = CreateTestPeer();
        peer.RecordSuccess();
        peer.RecordSuccess();
        peer.RecordSuccess();
        peer.RecordFailure();

        // Act
        var score = peer.ReliabilityScore;

        // Assert
        score.Should().Be(0.75, "3 successes out of 4 total = 75%");
    }

    [Fact]
    public void ReliabilityScore_Should_Be_1_0_With_Only_Successes()
    {
        // Arrange
        var peer = CreateTestPeer();
        peer.RecordSuccess();
        peer.RecordSuccess();

        // Act
        var score = peer.ReliabilityScore;

        // Assert
        score.Should().Be(1.0, "100% success rate");
    }

    [Fact]
    public void ReliabilityScore_Should_Be_0_0_With_Only_Failures()
    {
        // Arrange
        var peer = CreateTestPeer();
        peer.RecordFailure();
        peer.RecordFailure();

        // Act
        var score = peer.ReliabilityScore;

        // Assert
        score.Should().Be(0.0, "0% success rate");
    }

    [Fact]
    public void GetPeerId_Should_Return_Base64_PublicKey()
    {
        // Arrange
        var publicKey = new byte[32];
        for (int i = 0; i < 32; i++)
            publicKey[i] = (byte)i;
        var peer = new Peer(publicKey, 0xC0A80101, 6881);

        // Act
        var peerId = peer.GetPeerId();

        // Assert
        peerId.Should().NotBeNullOrEmpty();
        peerId.Should().Be(Convert.ToBase64String(publicKey));
    }

    [Fact]
    public void GetSocketAddress_Should_Return_Formatted_Address()
    {
        // Arrange
        // 192.168.1.1 = 0xC0A80101 (big-endian)
        var peer = CreateTestPeer(0xC0A80101, 6881);

        // Act
        var address = peer.GetSocketAddress();

        // Assert
        address.Should().Be("192.168.1.1:6881");
    }

    [Fact]
    public void GetSocketAddress_Should_Handle_Different_IPs()
    {
        // Arrange
        // 10.0.0.1 = 0x0A000001 (big-endian)
        var peer = CreateTestPeer(0x0A000001, 8080);

        // Act
        var address = peer.GetSocketAddress();

        // Assert
        address.Should().Be("10.0.0.1:8080");
    }

    [Fact]
    public void ToString_Should_Include_Socket_RTT_And_Reliability()
    {
        // Arrange
        var peer = CreateTestPeer(0xC0A80101, 6881);
        peer.RttMs = 42.5;
        peer.RecordSuccess();
        peer.RecordSuccess();
        peer.RecordFailure();

        // Act
        var str = peer.ToString();

        // Assert
        str.Should().Contain("192.168.1.1:6881");
        str.Should().Contain("42.5");
        str.Should().Contain("67%"); // 2/3 = 67%
    }

    [Fact]
    public void Equals_Should_Return_True_For_Same_PublicKey()
    {
        // Arrange
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        var peer1 = new Peer(publicKey, 0xC0A80101, 6881);
        var peer2 = new Peer(publicKey, 0x0A000001, 8080); // Different IP/port

        // Act
        var equals = peer1.Equals(peer2);

        // Assert
        equals.Should().BeTrue("peers with same public key should be equal");
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_PublicKey()
    {
        // Arrange
        var peer1 = CreateTestPeer();
        var peer2 = CreateTestPeer();

        // Act
        var equals = peer1.Equals(peer2);

        // Assert
        equals.Should().BeFalse("peers with different public keys should not be equal");
    }

    [Fact]
    public void Equals_Should_Return_False_For_Null()
    {
        // Arrange
        var peer = CreateTestPeer();

        // Act
        var equals = peer.Equals(null);

        // Assert
        equals.Should().BeFalse();
    }

    [Fact]
    public void Equals_Should_Return_False_For_Non_Peer_Object()
    {
        // Arrange
        var peer = CreateTestPeer();

        // Act
        var equals = peer.Equals("not a peer");

        // Assert
        equals.Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        var peer = new Peer(publicKey, 0xC0A80101, 6881);

        // Act
        var hash1 = peer.GetHashCode();
        var hash2 = peer.GetHashCode();

        // Assert
        hash1.Should().Be(hash2, "hash code should be consistent");
    }

    [Fact]
    public void GetHashCode_Should_Be_Same_For_Equal_Peers()
    {
        // Arrange
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        var peer1 = new Peer(publicKey, 0xC0A80101, 6881);
        var peer2 = new Peer(publicKey, 0x0A000001, 8080);

        // Act
        var hash1 = peer1.GetHashCode();
        var hash2 = peer2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2, "equal peers should have same hash code");
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        // Arrange
        var peer = CreateTestPeer();

        // Act
        peer.IPv4Address = 0x0A000001;
        peer.Port = 8080;
        peer.IsHandshakeComplete = true;
        peer.IsRelayCandidate = false;
        peer.RttMs = 123.45;
        peer.EstimatedBandwidth = 1000000;

        // Assert
        peer.IPv4Address.Should().Be(0x0A000001);
        peer.Port.Should().Be(8080);
        peer.IsHandshakeComplete.Should().BeTrue();
        peer.IsRelayCandidate.Should().BeFalse();
        peer.RttMs.Should().Be(123.45);
        peer.EstimatedBandwidth.Should().Be(1000000);
    }

    private Peer CreateTestPeer(uint ipv4Address = 0xC0A80101, ushort port = 6881)
    {
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        return new Peer(publicKey, ipv4Address, port);
    }
}

