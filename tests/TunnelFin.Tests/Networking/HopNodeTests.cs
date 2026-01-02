using FluentAssertions;
using NSec.Cryptography;
using TunnelFin.Networking.Circuits;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for HopNode class.
/// Tests hop node state management, key exchange, and encryption/decryption.
/// </summary>
public class HopNodeTests
{
    [Fact]
    public void Constructor_Should_Initialize_HopNode_With_Valid_Parameters()
    {
        // Arrange
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        uint ipv4Address = 0xC0A80101; // 192.168.1.1
        ushort port = 6881;
        int hopIndex = 0;

        // Act
        var hopNode = new HopNode(publicKey, ipv4Address, port, hopIndex);

        // Assert
        hopNode.PublicKey.Should().BeEquivalentTo(publicKey);
        hopNode.IPv4Address.Should().Be(ipv4Address);
        hopNode.Port.Should().Be(port);
        hopNode.HopIndex.Should().Be(hopIndex);
        hopNode.IsKeyExchangeComplete.Should().BeFalse("key exchange should not be complete initially");
        hopNode.EphemeralPublicKey.Should().BeNull("ephemeral key should be null initially");
        hopNode.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_Should_Throw_When_PublicKey_Is_Null()
    {
        // Arrange & Act
        var act = () => new HopNode(null!, 0xC0A80101, 6881, 0);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publicKey");
    }

    [Fact]
    public void Constructor_Should_Throw_When_PublicKey_Is_Not_32_Bytes()
    {
        // Arrange & Act
        var act1 = () => new HopNode(new byte[16], 0xC0A80101, 6881, 0);
        var act2 = () => new HopNode(new byte[64], 0xC0A80101, 6881, 0);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void Constructor_Should_Throw_When_HopIndex_Is_Negative()
    {
        // Arrange
        var publicKey = new byte[32];

        // Act
        var act = () => new HopNode(publicKey, 0xC0A80101, 6881, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hopIndex");
    }

    [Fact]
    public void Constructor_Should_Throw_When_HopIndex_Is_Greater_Than_2()
    {
        // Arrange
        var publicKey = new byte[32];

        // Act
        var act = () => new HopNode(publicKey, 0xC0A80101, 6881, 3);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hopIndex");
    }

    [Fact]
    public void Constructor_Should_Accept_Valid_HopIndex_Values()
    {
        // Arrange
        var publicKey = new byte[32];

        // Act
        var hop0 = new HopNode(publicKey, 0xC0A80101, 6881, 0);
        var hop1 = new HopNode(publicKey, 0xC0A80101, 6881, 1);
        var hop2 = new HopNode(publicKey, 0xC0A80101, 6881, 2);

        // Assert
        hop0.HopIndex.Should().Be(0);
        hop1.HopIndex.Should().Be(1);
        hop2.HopIndex.Should().Be(2);
    }

    [Fact]
    public void CompleteKeyExchange_Should_Set_EphemeralPublicKey()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();

        // Act
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);

        // Assert
        hopNode.EphemeralPublicKey.Should().BeEquivalentTo(ephemeralPublicKey);
    }

    [Fact]
    public void CompleteKeyExchange_Should_Throw_When_EphemeralPublicKey_Is_Null()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ourEphemeralKey = CreateX25519Key();

        // Act
        var act = () => hopNode.CompleteKeyExchange(null!, ourEphemeralKey);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ephemeralPublicKey");
    }

    [Fact]
    public void CompleteKeyExchange_Should_Throw_When_EphemeralPublicKey_Is_Not_32_Bytes()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ourEphemeralKey = CreateX25519Key();

        // Act
        var act = () => hopNode.CompleteKeyExchange(new byte[16], ourEphemeralKey);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void CompleteKeyExchange_Should_Throw_When_OurEphemeralPrivateKey_Is_Null()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];

        // Act
        var act = () => hopNode.CompleteKeyExchange(ephemeralPublicKey, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ourEphemeralPrivateKey");
    }

    [Fact]
    public void Encrypt_Should_Throw_When_Key_Exchange_Not_Complete()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => hopNode.Encrypt(plaintext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Key exchange must be completed*");
    }

    [Fact]
    public void Encrypt_Should_Return_Data_After_Key_Exchange()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var ciphertext = hopNode.Encrypt(plaintext);

        // Assert
        ciphertext.Should().NotBeNull();
        ciphertext.Should().NotBeEquivalentTo(plaintext, "Encrypted data should differ from plaintext");
        ciphertext.Length.Should().Be(plaintext.Length + 12 + 16,
            "Ciphertext should include nonce (12 bytes) and auth tag (16 bytes)");
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Key_Exchange_Not_Complete()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ciphertext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => hopNode.Decrypt(ciphertext);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Key exchange must be completed*");
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Ciphertext_Is_Null()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);

        // Act
        var act = () => hopNode.Decrypt(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ciphertext");
    }

    [Fact]
    public void Decrypt_Should_Return_Data_After_Key_Exchange()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);
        var originalPlaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Encrypt first to get valid ciphertext
        var ciphertext = hopNode.Encrypt(originalPlaintext);

        // Act - decrypt the ciphertext
        var decryptedPlaintext = hopNode.Decrypt(ciphertext);

        // Assert
        decryptedPlaintext.Should().NotBeNull();
        decryptedPlaintext.Should().BeEquivalentTo(originalPlaintext,
            "Decrypted data should match original plaintext");
    }

    [Fact]
    public void Dispose_Should_Allow_Multiple_Calls()
    {
        // Arrange
        var hopNode = CreateTestHopNode();

        // Act
        hopNode.Dispose();
        var act = () => hopNode.Dispose();

        // Assert
        act.Should().NotThrow("multiple dispose calls should be safe");
    }

    [Fact]
    public void Encrypt_Should_Throw_After_Dispose()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);
        hopNode.Dispose();

        // Act
        var act = () => hopNode.Encrypt(new byte[] { 1, 2, 3 });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Decrypt_Should_Throw_After_Dispose()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        var ephemeralPublicKey = new byte[32];
        new Random().NextBytes(ephemeralPublicKey);
        var ourEphemeralKey = CreateX25519Key();
        hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);
        hopNode.Dispose();

        // Act
        var act = () => hopNode.Decrypt(new byte[] { 1, 2, 3 });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void CompleteKeyExchange_Should_Throw_After_Dispose()
    {
        // Arrange
        var hopNode = CreateTestHopNode();
        hopNode.Dispose();
        var ephemeralPublicKey = new byte[32];
        var ourEphemeralKey = CreateX25519Key();

        // Act
        var act = () => hopNode.CompleteKeyExchange(ephemeralPublicKey, ourEphemeralKey);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    private HopNode CreateTestHopNode(int hopIndex = 0)
    {
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);
        return new HopNode(publicKey, 0xC0A80101, 6881, hopIndex);
    }

    private Key CreateX25519Key()
    {
        var algorithm = KeyAgreementAlgorithm.X25519;
        var creationParameters = new KeyCreationParameters
        {
            ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving
        };
        return Key.Create(algorithm, creationParameters);
    }
}

