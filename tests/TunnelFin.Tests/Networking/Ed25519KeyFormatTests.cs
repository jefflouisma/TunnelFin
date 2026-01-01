using FluentAssertions;
using System.Text.Json;
using TunnelFin.Networking.Identity;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Ed25519 cross-language key format compatibility tests.
/// Verifies NSec RawPrivateKey import of PyNaCl to_seed() produces identical public key (FR-049).
/// </summary>
public class Ed25519KeyFormatTests
{
    private static readonly string TestVectorsPath = Path.Combine(
        Directory.GetCurrentDirectory().Split("tests")[0],
        "tests/fixtures/ed25519_test_vectors.json");

    [Fact]
    public void NSec_RawPrivateKey_Should_Match_PyNaCl_Seed_Format()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)i;

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var exportedSeed = keyPair.PrivateKeySeedBytes;

        // Assert
        exportedSeed.Should().Equal(seed,
            "NSec RawPrivateKey format should match PyNaCl to_seed() format (32-byte seed)");
    }

    [Fact]
    public void Same_Seed_Should_Produce_Same_PublicKey_In_CSharp_And_Python()
    {
        // Arrange
        var testVectors = LoadTestVectors();
        var keypairs = testVectors.RootElement.GetProperty("keypairs");

        foreach (var keypair in keypairs.EnumerateArray())
        {
            var seedHex = keypair.GetProperty("seed_hex").GetString()!;
            var expectedPublicKeyHex = keypair.GetProperty("public_key_hex").GetString()!;
            var seed = Convert.FromHexString(seedHex);
            var expectedPublicKey = Convert.FromHexString(expectedPublicKeyHex);

            // Act
            using var keyPair = new Ed25519KeyPair(seed);
            var actualPublicKey = keyPair.PublicKeyBytes;

            // Assert
            actualPublicKey.Should().Equal(expectedPublicKey,
                $"C# should derive same public key as Python for seed {seedHex}");
        }
    }

    [Fact]
    public void PublicKey_Should_Be_32Bytes_Compressed_Point()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();
        var publicKey = keyPair.PublicKeyBytes;

        // Assert
        publicKey.Should().HaveCount(32,
            "Public key format: 32 bytes (little-endian y-coordinate + x-parity bit)");
    }

    [Fact]
    public void PrivateKey_Export_Should_Use_RawPrivateKey_Format()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();
        var exportedSeed = keyPair.PrivateKeySeedBytes;

        // Assert
        exportedSeed.Should().HaveCount(32,
            "NSec must export using KeyBlobFormat.RawPrivateKey for 32-byte seed");
    }

    [Fact]
    public void PrivateKey_Import_Should_Accept_32Byte_Seed()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)(i * 3);

        // Act
        using var keyPair = new Ed25519KeyPair(seed);

        // Assert
        keyPair.PrivateKeySeedBytes.Should().Equal(seed,
            "NSec must import 32-byte seeds using KeyBlobFormat.RawPrivateKey");
    }

    [Fact]
    public void KeyExportPolicy_Should_Allow_Plaintext_Archiving()
    {
        // Arrange & Act
        using var keyPair = new Ed25519KeyPair();

        // This should not throw - export policy is configured correctly
        var seed = keyPair.PrivateKeySeedBytes;

        // Assert
        seed.Should().NotBeNull("KeyExportPolicies.AllowPlaintextArchiving should be configured");
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000", "3b6a27bcceb6a42d62a3a8d02a6f0d73653215771de243a63ac048a18b59da29")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "207a067892821e25d770f1fba0c47c11ff4b813e54162ece9eb839e076231ab6")]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", "76a1592044a6e4f511265bca73a604d90b0529d1df602be30a19a9257660d1f5")]
    public void Known_Seeds_Should_Produce_Expected_PublicKeys(string seedHex, string expectedPublicKeyHex)
    {
        // Arrange
        var seed = Convert.FromHexString(seedHex);
        var expectedPublicKey = Convert.FromHexString(expectedPublicKeyHex);

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var actualPublicKey = keyPair.PublicKeyBytes;

        // Assert
        actualPublicKey.Should().Equal(expectedPublicKey,
            $"Seed {seedHex} should produce public key {expectedPublicKeyHex}");
    }

    private static JsonDocument LoadTestVectors()
    {
        var json = File.ReadAllText(TestVectorsPath);
        return JsonDocument.Parse(json);
    }
}

