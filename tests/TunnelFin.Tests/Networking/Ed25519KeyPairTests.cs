using FluentAssertions;
using TunnelFin.Networking.Identity;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for Ed25519 identity and key pair operations.
/// Tests key generation, signing, and verification using NSec.Cryptography.
/// </summary>
public class Ed25519KeyPairTests
{
    [Fact]
    public void Ed25519KeyPair_Should_Generate_Valid_KeyPair()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();

        // Assert
        keyPair.Should().NotBeNull();
        keyPair.PublicKeyBytes.Should().NotBeNull();
        keyPair.PrivateKeySeedBytes.Should().NotBeNull();
    }

    [Fact]
    public void Ed25519KeyPair_Should_Have_32Byte_PrivateKey_Seed()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();

        // Assert
        keyPair.PrivateKeySeedBytes.Should().HaveCount(32,
            "Private key seed must be exactly 32 bytes (PyNaCl to_seed() format)");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Have_32Byte_PublicKey()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();

        // Assert
        keyPair.PublicKeyBytes.Should().HaveCount(32,
            "Public key must be exactly 32 bytes (compressed point)");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Sign_Message()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Hello, World!"u8.ToArray();

        // Act
        var signature = keyPair.Sign(message);

        // Assert
        signature.Should().NotBeNull();
        signature.Should().HaveCount(64);
    }

    [Fact]
    public void Ed25519KeyPair_Should_Verify_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Hello, World!"u8.ToArray();
        var signature = keyPair.Sign(message);

        // Act
        var isValid = keyPair.Verify(message, signature);

        // Assert
        isValid.Should().BeTrue("Valid signature should verify successfully");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Produce_64Byte_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();

        // Act
        var signature = keyPair.Sign(message);

        // Assert
        signature.Should().HaveCount(64,
            "Signature must be exactly 64 bytes (R || S, each 32 bytes)");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Reject_Invalid_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Original message"u8.ToArray();
        var signature = keyPair.Sign(message);

        // Tamper with signature
        signature[0] ^= 0xFF;

        // Act
        var isValid = keyPair.Verify(message, signature);

        // Assert
        isValid.Should().BeFalse("Tampered signature should be rejected");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Export_PrivateKey_As_RawPrivateKey()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();
        var seed = keyPair.PrivateKeySeedBytes;

        // Assert
        seed.Should().HaveCount(32,
            "Exported private key seed must be 32 bytes (RawPrivateKey format, PyNaCl compatible)");
    }

    [Fact]
    public void Ed25519KeyPair_Should_Import_PrivateKey_From_32Byte_Seed()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)i;

        // Act
        using var keyPair = new Ed25519KeyPair(seed);

        // Assert
        keyPair.PublicKeyBytes.Should().HaveCount(32);
        keyPair.PrivateKeySeedBytes.Should().Equal(seed);
    }

    [Fact]
    public void Ed25519KeyPair_Should_Derive_Same_PublicKey_From_Same_Seed()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)(i * 2);

        // Act
        using var keyPair1 = new Ed25519KeyPair(seed);
        using var keyPair2 = new Ed25519KeyPair(seed);

        // Assert
        keyPair1.PublicKeyBytes.Should().Equal(keyPair2.PublicKeyBytes,
            "Same seed must produce identical public key (deterministic key derivation)");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Seed_Is_Null()
    {
        // Arrange & Act
        var act = () => new Ed25519KeyPair(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("seed");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Seed_Is_Not_32_Bytes()
    {
        // Arrange & Act
        var act1 = () => new Ed25519KeyPair(new byte[16]);
        var act2 = () => new Ed25519KeyPair(new byte[64]);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void Sign_Should_Throw_When_Message_Is_Null()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();

        // Act
        var act = () => keyPair.Sign(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public void Sign_Should_Throw_After_Dispose()
    {
        // Arrange
        var keyPair = new Ed25519KeyPair();
        keyPair.Dispose();

        // Act
        var act = () => keyPair.Sign(new byte[] { 1, 2, 3 });

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void PrivateKeySeedBytes_Should_Throw_After_Dispose()
    {
        // Arrange
        var keyPair = new Ed25519KeyPair();
        keyPair.Dispose();

        // Act
        var act = () => keyPair.PrivateKeySeedBytes;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Verify_Should_Throw_When_Message_Is_Null()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var signature = new byte[64];

        // Act
        var act = () => keyPair.Verify(null!, signature);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public void Verify_Should_Throw_When_Signature_Is_Null()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = new byte[] { 1, 2, 3 };

        // Act
        var act = () => keyPair.Verify(message, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("signature");
    }

    [Fact]
    public void Verify_Should_Throw_When_Signature_Is_Not_64_Bytes()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = new byte[] { 1, 2, 3 };

        // Act
        var act1 = () => keyPair.Verify(message, new byte[32]);
        var act2 = () => keyPair.Verify(message, new byte[128]);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*64 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*64 bytes*");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Verify_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();
        var signature = keyPair.Sign(message);
        var publicKey = keyPair.PublicKeyBytes;

        // Act
        var isValid = Ed25519KeyPair.VerifyWithPublicKey(publicKey, message, signature);

        // Assert
        isValid.Should().BeTrue("valid signature should verify with public key");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Reject_Invalid_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();
        var signature = keyPair.Sign(message);
        signature[0] ^= 0xFF; // Tamper with signature
        var publicKey = keyPair.PublicKeyBytes;

        // Act
        var isValid = Ed25519KeyPair.VerifyWithPublicKey(publicKey, message, signature);

        // Assert
        isValid.Should().BeFalse("tampered signature should be rejected");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Throw_When_PublicKey_Is_Null()
    {
        // Arrange
        var message = new byte[] { 1, 2, 3 };
        var signature = new byte[64];

        // Act
        var act = () => Ed25519KeyPair.VerifyWithPublicKey(null!, message, signature);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publicKey");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Throw_When_PublicKey_Is_Not_32_Bytes()
    {
        // Arrange
        var message = new byte[] { 1, 2, 3 };
        var signature = new byte[64];

        // Act
        var act1 = () => Ed25519KeyPair.VerifyWithPublicKey(new byte[16], message, signature);
        var act2 = () => Ed25519KeyPair.VerifyWithPublicKey(new byte[64], message, signature);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*32 bytes*");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Throw_When_Message_Is_Null()
    {
        // Arrange
        var publicKey = new byte[32];
        var signature = new byte[64];

        // Act
        var act = () => Ed25519KeyPair.VerifyWithPublicKey(publicKey, null!, signature);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("message");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Throw_When_Signature_Is_Null()
    {
        // Arrange
        var publicKey = new byte[32];
        var message = new byte[] { 1, 2, 3 };

        // Act
        var act = () => Ed25519KeyPair.VerifyWithPublicKey(publicKey, message, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("signature");
    }

    [Fact]
    public void VerifyWithPublicKey_Should_Throw_When_Signature_Is_Not_64_Bytes()
    {
        // Arrange
        var publicKey = new byte[32];
        var message = new byte[] { 1, 2, 3 };

        // Act
        var act1 = () => Ed25519KeyPair.VerifyWithPublicKey(publicKey, message, new byte[32]);
        var act2 = () => Ed25519KeyPair.VerifyWithPublicKey(publicKey, message, new byte[128]);

        // Assert
        act1.Should().Throw<ArgumentException>()
            .WithMessage("*64 bytes*");
        act2.Should().Throw<ArgumentException>()
            .WithMessage("*64 bytes*");
    }

    [Fact]
    public void Dispose_Should_Allow_Multiple_Calls()
    {
        // Arrange
        var keyPair = new Ed25519KeyPair();

        // Act
        keyPair.Dispose();
        var act = () => keyPair.Dispose();

        // Assert
        act.Should().NotThrow("multiple dispose calls should be safe");
    }

    [Fact]
    public void Sign_Should_Produce_Deterministic_Signatures()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)i;
        using var keyPair = new Ed25519KeyPair(seed);
        var message = "Test message"u8.ToArray();

        // Act
        var signature1 = keyPair.Sign(message);
        var signature2 = keyPair.Sign(message);

        // Assert
        signature1.Should().Equal(signature2, "Ed25519 signatures should be deterministic");
    }

    [Fact]
    public void Verify_Should_Reject_Wrong_Message()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message1 = "Original message"u8.ToArray();
        var message2 = "Different message"u8.ToArray();
        var signature = keyPair.Sign(message1);

        // Act
        var isValid = keyPair.Verify(message2, signature);

        // Assert
        isValid.Should().BeFalse("signature for different message should be rejected");
    }
}

