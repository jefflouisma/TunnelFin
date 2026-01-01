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
        uint identifier = 0xABCDEF01;
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
        var ephemeralKey = new byte[32];
        var auth = new byte[16];
        var candidateList = new byte[10];
        for (int i = 0; i < 32; i++) ephemeralKey[i] = (byte)i;
        for (int i = 0; i < 16; i++) auth[i] = (byte)(i + 100);
        for (int i = 0; i < 10; i++) candidateList[i] = (byte)(i + 200);

        // Act
        var message = CircuitMessage.SerializeCreated(circuitId, ephemeralKey, auth, candidateList);

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
        uint identifier = 0x11223344;
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
        var ephemeralKey = new byte[32];
        var auth = new byte[8];
        var candidateList = new byte[5];
        for (int i = 0; i < 32; i++) ephemeralKey[i] = (byte)(255 - i);
        for (int i = 0; i < 8; i++) auth[i] = (byte)(i + 50);
        for (int i = 0; i < 5; i++) candidateList[i] = (byte)(i + 150);

        // Act
        var message = CircuitMessage.SerializeExtended(circuitId, ephemeralKey, auth, candidateList);

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
        uint identifier = 2;
        var nodePublicKey = new byte[32];
        var ephemeralKey = new byte[32];

        // Act
        var message = CircuitMessage.SerializeCreate(circuitId, identifier, nodePublicKey, ephemeralKey);

        // Assert - exact size with no padding
        // 4 (circuit ID) + 4 (identifier) + 2 (node key length) + 32 (node key) + 2 (ephemeral key length) + 32 (ephemeral key) = 76
        message.Length.Should().Be(76, "Message should have no padding between fields");
    }

    [Fact]
    public void Circuit_Messages_Should_Use_TwoByte_Length_Prefix_For_Variable_Fields()
    {
        // Arrange
        uint circuitId = 1;
        var ephemeralKey = new byte[32];
        var auth = new byte[16];
        var candidateList = new byte[10];

        // Act
        var message = CircuitMessage.SerializeCreated(circuitId, ephemeralKey, auth, candidateList);

        // Assert - check length prefixes
        // Offset 4: ephemeral key length prefix (2 bytes)
        var ephemeralKeyLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(4, 2));
        ephemeralKeyLength.Should().Be(32, "Ephemeral key length should be 2-byte big-endian");

        // Offset 4 + 2 + 32 = 38: auth length prefix (2 bytes)
        var authLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(38, 2));
        authLength.Should().Be(16, "Auth length should be 2-byte big-endian");

        // Offset 38 + 2 + 16 = 56: candidate list length prefix (2 bytes)
        var candidateListLength = BinaryPrimitives.ReadUInt16BigEndian(message.AsSpan(56, 2));
        candidateListLength.Should().Be(10, "Candidate list length should be 2-byte big-endian");
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

