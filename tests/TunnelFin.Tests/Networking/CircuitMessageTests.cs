using FluentAssertions;
using System.Buffers.Binary;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Circuit message byte-level verification tests.
/// Verifies CREATE/CREATED/EXTEND/EXTENDED messages serialize with big-endian circuit IDs
/// and correct field ordering per ipv8-wire-format.md.
/// </summary>
public class CircuitMessageTests
{
    [Fact]
    public void CREATE_Message_Should_Have_CircuitID_As_First_Field()
    {
        // Arrange
        uint circuitId = 0x12345678;
        ushort identifier = 0xABCD;  // Changed to ushort (16-bit)
        var nodePublicKey = new byte[32];
        var ephemeralKey = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            nodePublicKey[i] = (byte)i;
            ephemeralKey[i] = (byte)(i + 32);
        }

        // Act
        var message = CircuitMessage.SerializeCreate(circuitId, identifier, nodePublicKey, ephemeralKey);

        // Assert
        message.Should().NotBeNull();
        message.Length.Should().BeGreaterThan(4);

        // Circuit ID should be first 4 bytes, big-endian
        var extractedCircuitId = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(0, 4));
        extractedCircuitId.Should().Be(circuitId, "Circuit ID must be first field");
    }

    [Fact]
    public void CREATE_Message_CircuitID_Should_Be_BigEndian()
    {
        // Verify circuit ID is serialized as big-endian uint32
        var buffer = new byte[4];
        uint circuitId = 0x00000001;
        
        BinaryPrimitives.WriteUInt32BigEndian(buffer, circuitId);
        
        buffer.Should().Equal(new byte[] { 0x00, 0x00, 0x00, 0x01 },
            "Circuit ID must be big-endian per ipv8-wire-format.md");
    }

    [Fact]
    public void CREATED_Message_Should_Have_CircuitID_As_First_Field()
    {
        // Arrange
        uint circuitId = 0x87654321;
        ushort identifier = 0x1234;  // Added identifier parameter
        var ephemeralKey = new byte[32];
        var auth = new byte[32];  // Changed to 32 bytes (fixed size per py-ipv8)
        var candidatesEnc = new byte[10];  // Renamed from candidateList
        for (int i = 0; i < 32; i++) ephemeralKey[i] = (byte)i;
        for (int i = 0; i < 32; i++) auth[i] = (byte)(i + 100);  // Fill 32 bytes
        for (int i = 0; i < 10; i++) candidatesEnc[i] = (byte)(i + 200);

        // Act
        var message = CircuitMessage.SerializeCreated(circuitId, identifier, ephemeralKey, auth, candidatesEnc);

        // Assert
        message.Should().NotBeNull();
        var extractedCircuitId = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(0, 4));
        extractedCircuitId.Should().Be(circuitId, "Circuit ID must be first field");
    }

    [Fact]
    public void EXTEND_Message_Should_Have_CircuitID_As_First_Field()
    {
        // Arrange
        uint circuitId = 0xDEADBEEF;
        var nodePublicKey = new byte[32];
        uint ipv4Address = 0x7F000001; // 127.0.0.1
        ushort port = 8080;
        ushort identifier = 0x1122;  // Changed to ushort (16-bit)
        for (int i = 0; i < 32; i++) nodePublicKey[i] = (byte)i;

        // Act
        var message = CircuitMessage.SerializeExtend(circuitId, nodePublicKey, ipv4Address, port, identifier);

        // Assert
        message.Should().NotBeNull();
        var extractedCircuitId = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(0, 4));
        extractedCircuitId.Should().Be(circuitId, "Circuit ID must be first field");
    }

    [Fact]
    public void EXTENDED_Message_Should_Have_CircuitID_As_First_Field()
    {
        // Arrange
        uint circuitId = 0xCAFEBABE;
        ushort identifier = 0x5678;  // Added identifier parameter
        var ephemeralKey = new byte[32];
        var auth = new byte[32];  // Changed to 32 bytes (fixed size per py-ipv8)
        var candidatesEnc = new byte[5];  // Renamed from candidateList
        for (int i = 0; i < 32; i++) ephemeralKey[i] = (byte)(255 - i);
        for (int i = 0; i < 32; i++) auth[i] = (byte)(i + 50);  // Fill 32 bytes
        for (int i = 0; i < 5; i++) candidatesEnc[i] = (byte)(i + 150);

        // Act
        var message = CircuitMessage.SerializeExtended(circuitId, identifier, ephemeralKey, auth, candidatesEnc);

        // Assert
        message.Should().NotBeNull();
        var extractedCircuitId = BinaryPrimitives.ReadUInt32BigEndian(message.AsSpan(0, 4));
        extractedCircuitId.Should().Be(circuitId, "Circuit ID must be first field");
    }

    [Fact]
    public void Circuit_Messages_Should_Have_No_Padding_Between_Fields()
    {
        // Arrange
        uint circuitId = 1;
        ushort identifier = 2;  // Changed to ushort (16-bit)
        var nodePublicKey = new byte[32];
        var ephemeralKey = new byte[32];

        // Act
        var message = CircuitMessage.SerializeCreate(circuitId, identifier, nodePublicKey, ephemeralKey);

        // Assert - exact size with no padding
        // 4 (circuit ID) + 2 (identifier) + 2 (node key length) + 32 (node key) + 2 (ephemeral key length) + 32 (ephemeral key) = 74
        message.Length.Should().Be(74, "Message should have no padding between fields");  // Changed from 76 to 74
    }

    [Fact]
    public void Circuit_Messages_Should_Use_TwoByte_Length_Prefix_For_Variable_Fields()
    {
        // Arrange
        uint circuitId = 1;
        ushort identifier = 2;  // Added identifier parameter
        var ephemeralKey = new byte[32];
        var auth = new byte[32];  // Changed to 32 bytes (fixed size per py-ipv8)
        var candidatesEnc = new byte[10];  // Renamed from candidateList

        // Act
        var message = CircuitMessage.SerializeCreated(circuitId, identifier, ephemeralKey, auth, candidatesEnc);

        // Assert - check length prefixes
        // Offset 4 (circuit ID) + 2 (identifier) = 6: ephemeral key length prefix (2 bytes)
        var ephemeralKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(6, 2));
        ephemeralKeyLength.Should().Be(32, "Ephemeral key length should be 2-byte big-endian");

        // NOTE: Auth is FIXED 32 bytes with NO length prefix per py-ipv8 format!
        // Offset 6 + 2 + 32 = 40: auth data (32 bytes, no prefix)
        // Offset 40 + 32 = 72: candidates_enc data (10 bytes, no prefix)
        message.Length.Should().Be(4 + 2 + 2 + 32 + 32 + 10, "Total message size should be correct");
    }

    [Theory]
    [InlineData(0x00000001)]
    [InlineData(0x12345678)]
    [InlineData(0xABCD1234)]
    [InlineData(0xFFFFFFFF)]
    public void CircuitID_Should_Serialize_Correctly(uint circuitId)
    {
        // Test various circuit ID values
        var buffer = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, circuitId);
        
        var readBack = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        readBack.Should().Be(circuitId, "Circuit ID should round-trip correctly");
    }
}

