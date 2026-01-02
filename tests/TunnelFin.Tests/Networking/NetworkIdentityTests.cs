using FluentAssertions;
using TunnelFin.Networking.Identity;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for NetworkIdentity (FR-004, FR-049).
/// Tests Ed25519 keypair generation and peer ID derivation.
/// </summary>
public class NetworkIdentityTests
{
    [Fact]
    public void Constructor_Should_Generate_New_Identity()
    {
        // Act
        var identity = new NetworkIdentity();

        // Assert
        identity.Should().NotBeNull();
        identity.PublicKey.Should().NotBeNull();
        identity.PublicKey.Should().HaveCount(32, "Ed25519 public key is 32 bytes");
        identity.PeerId.Should().NotBeNullOrEmpty("peer ID should be derived from public key");
    }

    [Fact]
    public void Constructor_With_Seed_Should_Be_Deterministic()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)i;

        // Act
        var identity1 = new NetworkIdentity(seed);
        var identity2 = new NetworkIdentity(seed);

        // Assert
        identity1.PublicKey.Should().Equal(identity2.PublicKey, "same seed should produce same public key");
        identity1.PeerId.Should().Be(identity2.PeerId, "same seed should produce same peer ID");
    }

    [Fact]
    public void PeerId_Should_Be_Derived_From_PublicKey()
    {
        // Arrange
        var identity = new NetworkIdentity();

        // Act
        var peerId = identity.PeerId;

        // Assert
        peerId.Should().NotBeNullOrEmpty();
        peerId.Should().HaveLength(40, "peer ID is SHA-1 hash (20 bytes) as hex string (40 chars)");
        peerId.Should().MatchRegex("^[0-9a-f]{40}$", "peer ID should be lowercase hex");
    }

    [Fact]
    public void Sign_Should_Produce_Valid_Signature()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var message = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var signature = identity.Sign(message);

        // Assert
        signature.Should().NotBeNull();
        signature.Should().HaveCount(64, "Ed25519 signature is 64 bytes");
    }

    [Fact]
    public void Verify_Should_Accept_Valid_Signature()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var message = new byte[] { 1, 2, 3, 4, 5 };
        var signature = identity.Sign(message);

        // Act
        var isValid = identity.Verify(message, signature);

        // Assert
        isValid.Should().BeTrue("signature should be valid");
    }

    [Fact]
    public void Verify_Should_Reject_Invalid_Signature()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var message = new byte[] { 1, 2, 3, 4, 5 };
        var invalidSignature = new byte[64]; // All zeros

        // Act
        var isValid = identity.Verify(message, invalidSignature);

        // Assert
        isValid.Should().BeFalse("invalid signature should be rejected");
    }

    [Fact]
    public void Verify_Should_Reject_Modified_Message()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var message = new byte[] { 1, 2, 3, 4, 5 };
        var signature = identity.Sign(message);
        var modifiedMessage = new byte[] { 1, 2, 3, 4, 6 }; // Changed last byte

        // Act
        var isValid = identity.Verify(modifiedMessage, signature);

        // Assert
        isValid.Should().BeFalse("signature should not verify for modified message");
    }

    [Fact]
    public void GetKeyPair_Should_Return_Ed25519KeyPair()
    {
        // Arrange
        var identity = new NetworkIdentity();

        // Act
        var keyPair = identity.GetKeyPair();

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.PublicKeyBytes.Should().Equal(identity.PublicKey);
    }

    [Fact]
    public void ToString_Should_Return_PeerId()
    {
        // Arrange
        var identity = new NetworkIdentity();

        // Act
        var str = identity.ToString();

        // Assert
        str.Should().Be(identity.PeerId);
    }

    [Fact]
    public void Different_Identities_Should_Have_Different_PeerIds()
    {
        // Arrange & Act
        var identity1 = new NetworkIdentity();
        var identity2 = new NetworkIdentity();

        // Assert
        identity1.PeerId.Should().NotBe(identity2.PeerId, "different identities should have different peer IDs");
    }
}

