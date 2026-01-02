using FluentAssertions;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.TrustChain;
using Xunit;

namespace TunnelFin.Tests.Networking.TrustChain;

/// <summary>
/// Unit tests for TrustChain block serialization (FR-050, ipv8-wire-format.md).
/// Tests exact field ordering for signature verification.
/// </summary>
public class BlockSerializerTests
{
    [Fact]
    public void SerializeBlock_Should_Include_All_Fields_In_Correct_Order()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32], // All zeros for genesis block
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = new byte[] { 1, 2, 3, 4, 5 }
        };

        // Act
        var serialized = BlockSerializer.SerializeBlock(block);

        // Assert
        serialized.Should().NotBeNull();
        // Expected size: 32 (creator key) + 32 (link key) + 4 (seq) + 32 (hash) + 8 (timestamp) + 2 (msg len) + 5 (msg) + 64 (sig) = 179 bytes
        serialized.Should().HaveCount(179, "block should have all fields serialized");
    }

    [Fact]
    public void SerializeBlock_Should_Use_BigEndian_For_Integers()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 0x12345678, // Distinctive pattern
            PreviousHash = new byte[32],
            Timestamp = 0x0102030405060708, // Distinctive pattern
            Message = new byte[] { 0xAA }
        };

        // Act
        var serialized = BlockSerializer.SerializeBlock(block);

        // Assert
        // Sequence number at offset 64 (after two 32-byte keys)
        serialized[64].Should().Be(0x12, "sequence number should be big-endian");
        serialized[65].Should().Be(0x34);
        serialized[66].Should().Be(0x56);
        serialized[67].Should().Be(0x78);

        // Timestamp at offset 100 (after keys + seq + hash)
        serialized[100].Should().Be(0x01, "timestamp should be big-endian");
        serialized[101].Should().Be(0x02);
    }

    [Fact]
    public void SerializeBlock_Should_Include_Message_Length_Prefix()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var message = new byte[256]; // 256 bytes
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32],
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = message
        };

        // Act
        var serialized = BlockSerializer.SerializeBlock(block);

        // Assert
        // Message length at offset 108 (after keys + seq + hash + timestamp)
        serialized[108].Should().Be(0x01, "message length should be 256 (0x0100) in big-endian");
        serialized[109].Should().Be(0x00);
    }

    [Fact]
    public void SerializeForSigning_Should_Exclude_Signature()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32],
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = new byte[] { 1, 2, 3 }
        };

        // Act
        var forSigning = BlockSerializer.SerializeForSigning(block);

        // Assert
        // Expected size: 32 + 32 + 4 + 32 + 8 + 2 + 3 = 113 bytes (no signature)
        forSigning.Should().HaveCount(113, "should not include 64-byte signature");
    }

    [Fact]
    public void SignBlock_Should_Produce_Valid_Signature()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32],
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = new byte[] { 1, 2, 3, 4, 5 }
        };

        // Act
        BlockSerializer.SignBlock(block, creatorIdentity);

        // Assert
        block.Signature.Should().NotBeNull();
        block.Signature.Should().HaveCount(64, "Ed25519 signature is 64 bytes");
    }

    [Fact]
    public void VerifyBlock_Should_Accept_Valid_Signature()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32],
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = new byte[] { 1, 2, 3, 4, 5 }
        };

        BlockSerializer.SignBlock(block, creatorIdentity);

        // Act
        var isValid = BlockSerializer.VerifyBlock(block);

        // Assert
        isValid.Should().BeTrue("signature should be valid");
    }

    [Fact]
    public void VerifyBlock_Should_Reject_Modified_Block()
    {
        // Arrange
        var creatorIdentity = new NetworkIdentity();
        var linkIdentity = new NetworkIdentity();
        
        var block = new TrustChainBlock
        {
            CreatorPublicKey = creatorIdentity.PublicKey,
            LinkPublicKey = linkIdentity.PublicKey,
            SequenceNumber = 1,
            PreviousHash = new byte[32],
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Message = new byte[] { 1, 2, 3, 4, 5 }
        };

        BlockSerializer.SignBlock(block, creatorIdentity);

        // Modify the block after signing
        block.SequenceNumber = 2;

        // Act
        var isValid = BlockSerializer.VerifyBlock(block);

        // Assert
        isValid.Should().BeFalse("signature should not verify for modified block");
    }
}

