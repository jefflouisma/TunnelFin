using FluentAssertions;
using System.Text;
using System.Text.Json;
using TunnelFin.Networking.Identity;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Ed25519 signature cross-language compatibility tests.
/// Verifies C# signatures verify in Python and vice versa using test vectors (FR-049).
/// </summary>
public class Ed25519SignatureTests
{
    private static readonly string TestVectorsPath = Path.Combine(
        Directory.GetCurrentDirectory().Split("tests")[0],
        "tests/fixtures/ed25519_test_vectors.json");

    [Fact]
    public void CSharp_Signature_Should_Match_Python_Signature()
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
                $"C# signature should match Python signature for seed {seedHex} and message {messageHex}");
        }
    }

    [Fact]
    public void Python_Signature_Should_Verify_In_CSharp()
    {
        // Arrange
        var testVectors = LoadTestVectors();
        var signatures = testVectors.RootElement.GetProperty("signatures");

        foreach (var sig in signatures.EnumerateArray())
        {
            var seedHex = sig.GetProperty("seed_hex").GetString()!;
            var messageHex = sig.GetProperty("message_hex").GetString()!;
            var signatureHex = sig.GetProperty("signature_hex").GetString()!;

            var seed = Convert.FromHexString(seedHex);
            var message = messageHex.Length > 0 ? Convert.FromHexString(messageHex) : Array.Empty<byte>();
            var signature = Convert.FromHexString(signatureHex);

            // Act
            using var keyPair = new Ed25519KeyPair(seed);
            var isValid = keyPair.Verify(message, signature);

            // Assert
            isValid.Should().BeTrue(
                $"Python-generated signature should verify in C# for seed {seedHex} and message {messageHex}");
        }
    }

    [Fact]
    public void Signature_Should_Be_64_Bytes()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();

        // Act
        var signature = keyPair.Sign(message);

        // Assert
        signature.Should().HaveCount(64,
            "Ed25519 signature is always 64 bytes (R || S, each 32 bytes)");
    }

    [Fact]
    public void Signature_Should_Use_LittleEndian_Per_RFC8032()
    {
        // Ed25519 uses little-endian per RFC 8032
        // This test verifies that our implementation matches Python's PyNaCl
        // which also follows RFC 8032

        // Arrange
        var seed = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000");
        var message = "Hello, World!"u8.ToArray();
        var expectedSignature = Convert.FromHexString("bb1a53a55494a41cd7f98302d1a156cf9a1ac046eced43b2b565e2debe4a336ccb5e2a03f7bd57bf33fa4bd154b64494a4a695e2d7993fc0727ad35d89413207");

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var signature = keyPair.Sign(message);

        // Assert
        signature.Should().Equal(expectedSignature,
            "Signature byte order should match RFC 8032 (little-endian)");
    }

    [Fact]
    public void Empty_Message_Should_Produce_Valid_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var emptyMessage = Array.Empty<byte>();

        // Act
        var signature = keyPair.Sign(emptyMessage);
        var isValid = keyPair.Verify(emptyMessage, signature);

        // Assert
        signature.Should().HaveCount(64);
        isValid.Should().BeTrue("Empty message should produce valid signature");
    }

    [Fact]
    public void Large_Message_Should_Produce_Valid_Signature()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var largeMessage = new byte[1024 * 1024]; // 1MB
        for (int i = 0; i < largeMessage.Length; i++)
            largeMessage[i] = (byte)(i % 256);

        // Act
        var signature = keyPair.Sign(largeMessage);
        var isValid = keyPair.Verify(largeMessage, signature);

        // Assert
        signature.Should().HaveCount(64);
        isValid.Should().BeTrue("Large message should produce valid signature");
    }

    [Theory]
    [InlineData("Hello, World!", "bb1a53a55494a41cd7f98302d1a156cf9a1ac046eced43b2b565e2debe4a336ccb5e2a03f7bd57bf33fa4bd154b64494a4a695e2d7993fc0727ad35d89413207")]
    [InlineData("The quick brown fox jumps over the lazy dog", "576eb0642a95ddbd3b0386d131869751c98296dcbba0ebba14c0665a67750531b10c78d5003e81c450fea448a1d8bce37789c62aa9a2ea4bf061bfc823eabe0d")]
    [InlineData("", "8f895b3cafe2c9506039d0e2a66382568004674fe8d237785092e40d6aaf483e4fc60168705f31f101596138ce21aa357c0d32a064f423dc3ee4aa3abf53f803")]
    public void Known_Messages_Should_Produce_Expected_Signatures(string message, string expectedSignatureHex)
    {
        // Arrange
        var seed = Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000");
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var expectedSignature = Convert.FromHexString(expectedSignatureHex);

        // Act
        using var keyPair = new Ed25519KeyPair(seed);
        var actualSignature = keyPair.Sign(messageBytes);

        // Assert
        actualSignature.Should().Equal(expectedSignature,
            $"Message '{message}' should produce expected signature");
    }

    [Fact]
    public void Modified_Message_Should_Fail_Verification()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var originalMessage = "Original message"u8.ToArray();
        var signature = keyPair.Sign(originalMessage);

        var modifiedMessage = "Modified message"u8.ToArray();

        // Act
        var isValid = keyPair.Verify(modifiedMessage, signature);

        // Assert
        isValid.Should().BeFalse("Modified message should fail verification");
    }

    [Fact]
    public void Modified_Signature_Should_Fail_Verification()
    {
        // Arrange
        using var keyPair = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();
        var signature = keyPair.Sign(message);

        // Tamper with signature
        signature[0] ^= 0xFF;

        // Act
        var isValid = keyPair.Verify(message, signature);

        // Assert
        isValid.Should().BeFalse("Modified signature should fail verification");
    }

    [Fact]
    public void Wrong_PublicKey_Should_Fail_Verification()
    {
        // Arrange
        using var keyPair1 = new Ed25519KeyPair();
        using var keyPair2 = new Ed25519KeyPair();
        var message = "Test message"u8.ToArray();
        var signature = keyPair1.Sign(message);

        // Act
        var isValid = keyPair2.Verify(message, signature);

        // Assert
        isValid.Should().BeFalse("Signature from different key should fail verification");
    }

    private static JsonDocument LoadTestVectors()
    {
        var json = File.ReadAllText(TestVectorsPath);
        return JsonDocument.Parse(json);
    }
}

