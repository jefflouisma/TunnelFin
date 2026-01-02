using System.Buffers.Binary;
using NSec.Cryptography;
using TunnelFin.Networking.Identity;

namespace TunnelFin.Networking.TrustChain;

/// <summary>
/// Serializes TrustChain blocks with exact field ordering for signature verification (FR-050).
/// Per ipv8-wire-format.md: creator key, link key, sequence, hash, timestamp, message length, message, signature.
/// </summary>
public static class BlockSerializer
{
    /// <summary>
    /// Serializes a complete TrustChain block including signature.
    /// </summary>
    /// <param name="block">Block to serialize.</param>
    /// <returns>Serialized block bytes.</returns>
    public static byte[] SerializeBlock(TrustChainBlock block)
    {
        var dataForSigning = SerializeForSigning(block);
        var buffer = new byte[dataForSigning.Length + 64]; // Add 64 bytes for signature

        // Copy data
        Array.Copy(dataForSigning, 0, buffer, 0, dataForSigning.Length);

        // Append signature
        if (block.Signature != null && block.Signature.Length == 64)
        {
            Array.Copy(block.Signature, 0, buffer, dataForSigning.Length, 64);
        }

        return buffer;
    }

    /// <summary>
    /// Serializes block data for signing (fields 1-7, excluding signature).
    /// </summary>
    /// <param name="block">Block to serialize.</param>
    /// <returns>Serialized data for signing.</returns>
    public static byte[] SerializeForSigning(TrustChainBlock block)
    {
        // Calculate total size: 32 + 32 + 4 + 32 + 8 + 2 + message.Length
        int totalSize = 32 + 32 + 4 + 32 + 8 + 2 + block.Message.Length;
        var buffer = new byte[totalSize];
        int offset = 0;

        // 1. Creator public key (32 bytes)
        if (block.CreatorPublicKey.Length != 32)
            throw new ArgumentException("Creator public key must be 32 bytes");
        Array.Copy(block.CreatorPublicKey, 0, buffer, offset, 32);
        offset += 32;

        // 2. Link public key (32 bytes)
        if (block.LinkPublicKey.Length != 32)
            throw new ArgumentException("Link public key must be 32 bytes");
        Array.Copy(block.LinkPublicKey, 0, buffer, offset, 32);
        offset += 32;

        // 3. Sequence number (4 bytes, big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), block.SequenceNumber);
        offset += 4;

        // 4. Previous hash (32 bytes)
        if (block.PreviousHash.Length != 32)
            throw new ArgumentException("Previous hash must be 32 bytes");
        Array.Copy(block.PreviousHash, 0, buffer, offset, 32);
        offset += 32;

        // 5. Timestamp (8 bytes, big-endian)
        BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(offset), block.Timestamp);
        offset += 8;

        // 6. Message length (2 bytes, big-endian)
        if (block.Message.Length > ushort.MaxValue)
            throw new ArgumentException("Message length exceeds maximum");
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)block.Message.Length);
        offset += 2;

        // 7. Message content (variable length)
        Array.Copy(block.Message, 0, buffer, offset, block.Message.Length);

        return buffer;
    }

    /// <summary>
    /// Signs a TrustChain block using the creator's identity.
    /// </summary>
    /// <param name="block">Block to sign.</param>
    /// <param name="creatorIdentity">Creator's network identity.</param>
    public static void SignBlock(TrustChainBlock block, NetworkIdentity creatorIdentity)
    {
        if (block == null)
            throw new ArgumentNullException(nameof(block));
        if (creatorIdentity == null)
            throw new ArgumentNullException(nameof(creatorIdentity));

        // Verify creator public key matches identity
        if (!block.CreatorPublicKey.SequenceEqual(creatorIdentity.PublicKey))
            throw new ArgumentException("Block creator public key does not match identity");

        // Serialize data for signing (fields 1-7)
        var dataForSigning = SerializeForSigning(block);

        // Sign the data
        block.Signature = creatorIdentity.Sign(dataForSigning);
    }

    /// <summary>
    /// Verifies a TrustChain block's signature.
    /// </summary>
    /// <param name="block">Block to verify.</param>
    /// <returns>True if signature is valid.</returns>
    public static bool VerifyBlock(TrustChainBlock block)
    {
        if (block == null)
            throw new ArgumentNullException(nameof(block));
        if (block.Signature == null || block.Signature.Length != 64)
            return false;

        // Serialize data for signing (fields 1-7)
        var dataForSigning = SerializeForSigning(block);

        // Import public key from block
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, block.CreatorPublicKey, KeyBlobFormat.RawPublicKey);

        // Verify signature
        return algorithm.Verify(publicKey, dataForSigning, block.Signature);
    }
}

