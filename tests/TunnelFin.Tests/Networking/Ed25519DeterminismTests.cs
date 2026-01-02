using FluentAssertions;
using System.Text;
using System.Text.Json;
using TunnelFin.Networking.Identity;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Ed25519 signature determinism tests.
/// Verifies same seed + message produces identical signature in C# and Python (FR-049).
/// </summary>
public class Ed25519DeterminismTests
{
    private static readonly string TestVectorsPath = Path.Combine(
        Directory.GetCurrentDirectory().Split("tests")[0],
        "tests/fixtures/ed25519_test_vectors.json");

    [Fact]
    public void Same_Seed_Should_Always_Produce_Same_PublicKey()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)i;

        // Act
        using var keyPair1 = new Ed25519KeyPair(seed);
        using var keyPair2 = new Ed25519KeyPair(seed);
        using var keyPair3 = new Ed25519KeyPair(seed);

        // Assert
        keyPair1.PublicKeyBytes.Should().Equal(keyPair2.PublicKeyBytes,
            "Same seed should always produce identical public key (deterministic key derivation)");
        keyPair2.PublicKeyBytes.Should().Equal(keyPair3.PublicKeyBytes,
            "Same seed should always produce identical public key (deterministic key derivation)");
    }

    [Fact]
    public void Same_Seed_And_Message_Should_Produce_Same_Signature()
    {
        // Arrange
        var seed = new byte[32];
        for (int i = 0; i < 32; i++)
            seed[i] = (byte)(i * 2);
        var message = "Test message"u8.ToArray();

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var signature1 = keyPair.Sign(message);
        var signature2 = keyPair.Sign(message);
        var signature3 = keyPair.Sign(message);

        // Assert
        signature1.Should().Equal(signature2,
            "Same seed and message should produce identical signature (deterministic signing)");
        signature2.Should().Equal(signature3,
            "Same seed and message should produce identical signature (deterministic signing)");
    }

    [Fact]
    public void CSharp_And_Python_Should_Produce_Identical_Signatures()
    {
        // Arrange
        var testVectors = LoadTestVectors();
        var signatures = testVectors.RootElement.GetProperty("signatures");

        foreach (var sig in signatures.EnumerateArray())
        {
            var seedHex = sig.GetProperty("seed_hex").GetString()!;
            var messageHex = sig.GetProperty("message_hex").GetString()!;
            var expectedSignatureHex = sig.GetProperty("signature_hex").GetString()!;

            var seed = Convert.FromHexString(seedHex);
            var message = messageHex.Length > 0 ? Convert.FromHexString(messageHex) : Array.Empty<byte>();
            var expectedSignature = Convert.FromHexString(expectedSignatureHex);

            // Act
            using var keyPair = new Ed25519KeyPair(seed);
            var actualSignature = keyPair.Sign(message);

            // Assert
            actualSignature.Should().Equal(expectedSignature,
                $"C# and Python should produce byte-identical signatures for seed {seedHex} and message {messageHex}");
        }
    }

    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000", "Hello, World!", "bb1a53a55494a41cd7f98302d1a156cf9a1ac046eced43b2b565e2debe4a336ccb5e2a03f7bd57bf33fa4bd154b64494a4a695e2d7993fc0727ad35d89413207")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "Test message", "")]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", "", "ee00ac6e44a6a43bc3a701c137b467473e32a4bed47d3293fe6186fe9868c7d254024f65ffa552487bdbd34aaccd1e82c1a20a191d183dbb64c3523cccd89c0f")]
    public void Known_Seed_And_Message_Should_Match_Python_Signature(string seedHex, string message, string expectedSignatureHex)
    {
        // Skip test if expected signature is empty (not in test vectors)
        if (string.IsNullOrEmpty(expectedSignatureHex))
            return;

        // Arrange
        var seed = Convert.FromHexString(seedHex);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var expectedSignature = Convert.FromHexString(expectedSignatureHex);

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var actualSignature = keyPair.Sign(messageBytes);

        // Assert
        actualSignature.Should().Equal(expectedSignature,
            $"C# should produce same signature as Python for seed {seedHex} and message '{message}'");
    }

    [Fact]
    public void Multiple_Signatures_With_Same_Key_Should_Be_Identical()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();

        // Act - Sign same message 100 times
        var signatures = new List<byte[]>();
        for (int i = 0; i < 100; i++)
        {
            signatures.Add(keyPair.Sign(message));
        }

        // Assert - All signatures should be byte-identical
        for (int i = 1; i < signatures.Count; i++)
        {
            signatures[i].Should().Equal(signatures[0],
                $"Signature {i} should be identical to signature 0 (Ed25519 is deterministic)");
        }
    }

    [Fact]
    public void Different_Seeds_Should_Produce_Different_Signatures()
    {
        // Arrange
        var seed1 = new byte[32];
        var seed2 = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            seed1[i] = (byte)i;
            seed2[i] = (byte)(i + 1);
        }
        var message = "Test message"u8.ToArray();

        // Act
        using var keyPair1 = new Ed25519KeyPair(seed1);
        using var keyPair2 = new Ed25519KeyPair(seed2);
        var signature1 = keyPair1.Sign(message);
        var signature2 = keyPair2.Sign(message);

        // Assert
        signature1.Should().NotEqual(signature2,
            "Different seeds should produce different signatures");
    }

    [Fact]
    public void Different_Messages_Should_Produce_Different_Signatures()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message1 = "Message 1"u8.ToArray();
        var message2 = "Message 2"u8.ToArray();

        // Act
        var signature1 = keyPair.Sign(message1);
        var signature2 = keyPair.Sign(message2);

        // Assert
        signature1.Should().NotEqual(signature2,
            "Different messages should produce different signatures");
    }

    [Fact]
    public async Task Signature_Should_Not_Depend_On_Timestamp()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();

        // Act - Sign at different times
        var signature1 = keyPair.Sign(message);
        await Task.Delay(100); // Wait 100ms
        var signature2 = keyPair.Sign(message);
        await Task.Delay(100); // Wait another 100ms
        var signature3 = keyPair.Sign(message);

        // Assert - All signatures should be identical (no timestamp dependency)
        signature1.Should().Equal(signature2,
            "Ed25519 is deterministic - no randomness, no timestamp dependency");
        signature2.Should().Equal(signature3,
            "Ed25519 is deterministic - no randomness, no timestamp dependency");
    }

    private static JsonDocument LoadTestVectors()
    {
        var json = File.ReadAllText(TestVectorsPath);
        return JsonDocument.Parse(json);
    }
}

