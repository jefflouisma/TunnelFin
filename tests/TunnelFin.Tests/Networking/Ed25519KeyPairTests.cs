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
}

