# IPv8 Wire Format Specification for C# Implementation

**Purpose**: Document exact byte-level serialization requirements for IPv8 protocol compatibility with Tribler network  
**Source**: Perplexity research analysis of py-ipv8 source code (January 2026)  
**Critical**: All C# serialization MUST produce byte-identical output to Python struct.pack

## Byte Order (Endianness)

**IPv8 uses big-endian (network byte order) consistently throughout all message serialization.**

- Python format: `">H"` (big-endian unsigned short), `">I"` (big-endian unsigned int), `">Q"` (big-endian unsigned long long)
- C# implementation: Use `System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian()`, `WriteUInt32BigEndian()`, `WriteUInt64BigEndian()`
- **NEVER use `BitConverter`** (platform-dependent byte order)
- **NEVER use `BinaryWriter`** (defaults to little-endian)

## Integer Type Specifications

| IPv8 Field | Python Format | C# Type | Byte Count | Serialization Method |
|-----------|---------------|---------|------------|---------------------|
| Circuit ID | `">I"` | `uint` | 4 | `BinaryPrimitives.WriteUInt32BigEndian()` |
| Sequence Number | `">I"` | `uint` | 4 | `BinaryPrimitives.WriteUInt32BigEndian()` |
| Message Type | `">B"` | `byte` | 1 | Direct byte write |
| Port Number | `">H"` | `ushort` | 2 | `BinaryPrimitives.WriteUInt16BigEndian()` |
| Timestamp | `">Q"` | `ulong` | 8 | `BinaryPrimitives.WriteUInt64BigEndian()` |
| IPv4 Address | `">I"` | `uint` | 4 | `BinaryPrimitives.WriteUInt32BigEndian()` |

**All integers are UNSIGNED** - use `uint`, `ushort`, `ulong`, `byte` (never `int`, `short`, `long`, `sbyte`)

## Boolean Serialization

- **Format**: Single byte (0x00 = false, 0x01 = true)
- **Python**: `struct.pack("?", value)` produces 1 byte
- **C# Implementation**: `buffer[offset] = value ? (byte)1 : (byte)0;`
- **NEVER use `BitConverter.GetBytes(bool)`** (may produce different representations)

## Variable-Length Fields

All variable-length fields use **2-byte big-endian length prefix** followed by data:

```csharp
// Write variable-length field
BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)data.Length);
offset += 2;
data.CopyTo(buffer, offset);
offset += data.Length;
```

## Message Structure

Every IPv8 message follows this structure:

1. **23-byte IPv8 community prefix** (version + routing info)
2. **1-byte message type identifier**
3. **Payload fields** (serialized sequentially, no padding)

**Circuit-based messages** include 4-byte big-endian circuit ID as first payload field.

## Core Message Formats

### Introduction-Request/Response

- Socket address: 4-byte big-endian IPv4 + 2-byte big-endian port
- Identifier fields: 4-byte big-endian unsigned integers (`">I"`)
- No padding between fields

### Puncture-Request/Puncture

- Circuit ID: 4-byte big-endian unsigned integer (`">I"`)
- Socket address: 4-byte big-endian IPv4 + 2-byte big-endian port

### CREATE/CREATED/EXTEND/EXTENDED

- Circuit ID: 4-byte big-endian unsigned integer (`">I"`) - FIRST field
- Ephemeral public key: 32 bytes (Curve25519, raw bytes)
- Encrypted payload: variable length with 2-byte big-endian length prefix

## TrustChain Block Serialization

**Critical for signature verification** - exact byte order required:

1. Creator public key: **74 bytes** (raw EC public key)
2. Link public key: **74 bytes** (counterparty public key)
3. Sequence number: **4 bytes** big-endian unsigned int (`">I"`)
4. Previous hash: **32 bytes** (SHA-3 hash, raw bytes)
5. Timestamp: **8 bytes** big-endian unsigned long long (`">Q"`, milliseconds since epoch)
6. Message length: **2 bytes** big-endian unsigned short (`">H"`)
7. Message content: **variable bytes** (length specified by field 6)
8. Signature: **64 bytes** (Ed25519 signature, raw bytes)

**Signature is computed over fields 1-7 in exact byte order above.**

## Ed25519 Cryptographic Formats

### Key Formats

- **Private key (seed)**: 32 bytes (PyNaCl `to_seed()` format)
- **Public key**: 32 bytes (compressed point, little-endian y-coordinate + x-parity bit)
- **Signature**: 64 bytes (R || S, each 32 bytes, little-endian)

### NSec.Cryptography Compatibility

```csharp
// Import PyNaCl seed (32 bytes)
var creationParams = new KeyCreationParameters 
{ 
    ExportPolicy = KeyExportPolicies.AllowPlaintextArchiving 
};
var key = Key.Import(
    SignatureAlgorithm.Ed25519, 
    seedBytes, 
    KeyBlobFormat.RawPrivateKey,
    creationParams
);

// Export public key (32 bytes, matches PyNaCl verify_key.encode())
byte[] publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

// Sign message (produces 64-byte signature)
byte[] signature = SignatureAlgorithm.Ed25519.Sign(key, messageBytes);
```

**Note**: Ed25519 keys use **little-endian** encoding (RFC 8032), while IPv8 messages use **big-endian**. Do not mix these!

## Critical Implementation Rules

1. **No padding**: Fields are serialized sequentially with no alignment padding
2. **No null terminators**: Strings/byte arrays use length prefix only
3. **Big-endian for all IPv8 integers**: Use BinaryPrimitives consistently
4. **Little-endian for Ed25519 keys**: Use NSec's native format (RFC 8032 compliant)
5. **Raw bytes for cryptographic material**: No hex encoding, Base64, or transformations
6. **Exact field ordering**: TrustChain blocks must serialize fields in documented order

## Testing Requirements

All IPv8 message serialization MUST be verified byte-for-byte against Python reference implementation:

```csharp
// Example test pattern
[Fact]
public void IPv8_CircuitCreate_ProducesByteIdenticalOutput()
{
    // Python reference hex dump (from py-ipv8 test vectors)
    string pythonHex = "00000001..."; // CREATE message hex
    byte[] expected = Convert.FromHexString(pythonHex);
    
    // C# implementation
    byte[] actual = SerializeCreateMessage(circuitId: 1, ...);
    
    // Byte-for-byte comparison
    Assert.Equal(expected, actual);
}
```

## References

- py-ipv8 serialization: https://py-ipv8.readthedocs.io/en/latest/reference/serialization.html
- RFC 8032 (Ed25519): https://datatracker.ietf.org/doc/html/rfc8032
- Python struct module: https://docs.python.org/3/library/struct.html
- BinaryPrimitives: https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives

