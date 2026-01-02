using FluentAssertions;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Networking.Tunnel;

public class LayeredEncryptionTests
{
    [Fact]
    public void EncryptLayers_Should_Encrypt_Data_Through_All_Hops()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var encrypted = LayeredEncryption.EncryptLayers(plaintext, circuit);

        // Assert
        encrypted.Should().NotBeNull();
        encrypted.Should().NotBeEquivalentTo(plaintext,
            "Encrypted data should differ from plaintext after layered encryption");
        encrypted.Length.Should().BeGreaterThan(plaintext.Length,
            "Encrypted data should be larger due to nonces and auth tags");
    }

    [Fact]
    public void DecryptLayers_Should_Decrypt_Data_Through_All_Hops()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };
        var encrypted = LayeredEncryption.EncryptLayers(plaintext, circuit);

        // Act
        var decrypted = LayeredEncryption.DecryptLayers(encrypted, circuit);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void EncryptLayers_Should_Throw_When_Circuit_Null()
    {
        // Arrange
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => LayeredEncryption.EncryptLayers(plaintext, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptLayers_Should_Throw_When_Plaintext_Null()
    {
        // Arrange
        var circuit = CreateTestCircuit();

        // Act
        var act = () => LayeredEncryption.EncryptLayers(null!, circuit);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptLayers_Should_Throw_When_Circuit_Not_Established()
    {
        // Arrange
        var circuit = new Circuit(1, 3, 600);
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => LayeredEncryption.EncryptLayers(plaintext, circuit);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Circuit must be established*");
    }

    [Fact]
    public void DecryptLayers_Should_Throw_When_Circuit_Null()
    {
        // Arrange
        var ciphertext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => LayeredEncryption.DecryptLayers(ciphertext, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DecryptLayers_Should_Throw_When_Ciphertext_Null()
    {
        // Arrange
        var circuit = CreateTestCircuit();

        // Act
        var act = () => LayeredEncryption.DecryptLayers(null!, circuit);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EncryptForHop_Should_Encrypt_For_Specific_Hop()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var encrypted = LayeredEncryption.EncryptForHop(plaintext, circuit, 0);

        // Assert
        encrypted.Should().NotBeNull();
        encrypted.Should().NotBeEquivalentTo(plaintext,
            "Encrypted data should differ from plaintext");
        encrypted.Length.Should().BeGreaterThan(plaintext.Length,
            "Encrypted data should include nonce and auth tag");
    }

    [Fact]
    public void DecryptFromHop_Should_Decrypt_From_Specific_Hop()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };
        var encrypted = LayeredEncryption.EncryptForHop(plaintext, circuit, 0);

        // Act
        var decrypted = LayeredEncryption.DecryptFromHop(encrypted, circuit, 0);

        // Assert
        decrypted.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void EncryptForHop_Should_Throw_When_Hop_Index_Invalid()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => LayeredEncryption.EncryptForHop(plaintext, circuit, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DecryptFromHop_Should_Throw_When_Hop_Index_Invalid()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var ciphertext = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var act = () => LayeredEncryption.DecryptFromHop(ciphertext, circuit, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private Circuit CreateTestCircuit()
    {
        var circuit = new Circuit(1, 3, 600);

        // Add 3 hops to make it established
        for (int i = 0; i < 3; i++)
        {
            // Create ephemeral key pair for this hop
            var ephemeralPrivateKey = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519);
            var ephemeralPublicKey = ephemeralPrivateKey.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

            var hop = new HopNode(new byte[32], (uint)(192 << 24 | 168 << 16 | 1 << 8 | (i + 1)), 8080, i);
            hop.CompleteKeyExchange(ephemeralPublicKey, ephemeralPrivateKey);
            circuit.AddHop(hop);
        }

        return circuit;
    }
}

