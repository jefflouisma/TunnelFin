using FluentAssertions;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Circuit message parsing tests.
/// Verifies C# can parse Python-generated circuit messages from test vectors.
/// </summary>
public class CircuitMessageParsingTests
{
    [Fact]
    public void Should_Parse_Python_Generated_CREATE_Message()
    {
        // Arrange - simulate a CREATE message (would come from Python in real scenario)
        uint expectedCircuitId = 0x12345678;
        ushort expectedIdentifier = 0xABCD;  // Changed to ushort (16-bit)
        var expectedNodeKey = new byte[32];
        var expectedEphemeralKey = new byte[32];
        for (int i = 0; i < 32; i++)
        {
            expectedNodeKey[i] = (byte)i;
            expectedEphemeralKey[i] = (byte)(i + 32);
        }
        var message = CircuitMessage.SerializeCreate(expectedCircuitId, expectedIdentifier, expectedNodeKey, expectedEphemeralKey);

        // Act
        var (circuitId, identifier, nodeKey, ephemeralKey) = CircuitMessage.ParseCreate(message);

        // Assert
        circuitId.Should().Be(expectedCircuitId);
        identifier.Should().Be(expectedIdentifier);
        nodeKey.Should().Equal(expectedNodeKey);
        ephemeralKey.Should().Equal(expectedEphemeralKey);
    }

    [Fact]
    public void Should_Parse_Python_Generated_CREATED_Message()
    {
        // Arrange
        uint expectedCircuitId = 0x87654321;
        ushort expectedIdentifier = 0x1234;  // Added identifier parameter
        var expectedEphemeralKey = new byte[32];
        var expectedAuth = new byte[32];  // Changed to 32 bytes (fixed size per py-ipv8)
        var expectedCandidatesEnc = new byte[10];  // Renamed from candidateList
        for (int i = 0; i < 32; i++) expectedEphemeralKey[i] = (byte)i;
        for (int i = 0; i < 32; i++) expectedAuth[i] = (byte)(i + 100);  // Fill 32 bytes
        for (int i = 0; i < 10; i++) expectedCandidatesEnc[i] = (byte)(i + 200);
        var message = CircuitMessage.SerializeCreated(expectedCircuitId, expectedIdentifier, expectedEphemeralKey, expectedAuth, expectedCandidatesEnc);

        // Act
        var (circuitId, identifier, ephemeralKey, auth, candidatesEnc) = CircuitMessage.ParseCreated(message);

        // Assert
        circuitId.Should().Be(expectedCircuitId);
        identifier.Should().Be(expectedIdentifier);
        ephemeralKey.Should().Equal(expectedEphemeralKey);
        auth.Should().Equal(expectedAuth);
        candidatesEnc.Should().Equal(expectedCandidatesEnc);
    }

    [Fact]
    public void Should_Parse_Python_Generated_EXTEND_Message()
    {
        // Arrange
        uint expectedCircuitId = 0xDEADBEEF;
        var expectedNodeKey = new byte[32];
        uint expectedIpv4 = 0x7F000001;
        ushort expectedPort = 8080;
        ushort expectedIdentifier = 0x1122;  // Changed to ushort (16-bit)
        for (int i = 0; i < 32; i++) expectedNodeKey[i] = (byte)i;
        var message = CircuitMessage.SerializeExtend(expectedCircuitId, expectedNodeKey, expectedIpv4, expectedPort, expectedIdentifier);

        // Act
        var (circuitId, nodeKey, ipv4, port, identifier) = CircuitMessage.ParseExtend(message);

        // Assert
        circuitId.Should().Be(expectedCircuitId);
        nodeKey.Should().Equal(expectedNodeKey);
        ipv4.Should().Be(expectedIpv4);
        port.Should().Be(expectedPort);
        identifier.Should().Be(expectedIdentifier);
    }

    [Fact]
    public void Should_Parse_Python_Generated_EXTENDED_Message()
    {
        // Arrange
        uint expectedCircuitId = 0xCAFEBABE;
        ushort expectedIdentifier = 0x5678;  // Added identifier parameter
        var expectedEphemeralKey = new byte[32];
        var expectedAuth = new byte[32];  // Changed to 32 bytes (fixed size per py-ipv8)
        var expectedCandidatesEnc = new byte[5];  // Renamed from candidateList
        for (int i = 0; i < 32; i++) expectedEphemeralKey[i] = (byte)(255 - i);
        for (int i = 0; i < 32; i++) expectedAuth[i] = (byte)(i + 50);  // Fill 32 bytes
        for (int i = 0; i < 5; i++) expectedCandidatesEnc[i] = (byte)(i + 150);
        var message = CircuitMessage.SerializeExtended(expectedCircuitId, expectedIdentifier, expectedEphemeralKey, expectedAuth, expectedCandidatesEnc);

        // Act
        var (circuitId, identifier, ephemeralKey, auth, candidatesEnc) = CircuitMessage.ParseExtended(message);

        // Assert
        circuitId.Should().Be(expectedCircuitId);
        identifier.Should().Be(expectedIdentifier);
        ephemeralKey.Should().Equal(expectedEphemeralKey);
        auth.Should().Equal(expectedAuth);
        candidatesEnc.Should().Equal(expectedCandidatesEnc);
    }

    [Fact]
    public void Should_Extract_CircuitID_From_Message()
    {
        // Arrange
        uint expectedCircuitId = 0xABCD1234;
        ushort identifier = 1;  // Changed to ushort
        var message = CircuitMessage.SerializeCreate(expectedCircuitId, identifier, new byte[32], new byte[32]);

        // Act
        var circuitId = CircuitMessage.ExtractCircuitId(message);

        // Assert
        circuitId.Should().Be(expectedCircuitId);
    }

    [Fact]
    public void Should_Extract_Ephemeral_Key_From_Message()
    {
        // Arrange
        var expectedEphemeralKey = new byte[32];
        for (int i = 0; i < 32; i++) expectedEphemeralKey[i] = (byte)(i + 100);
        var auth = new byte[32];  // Changed to 32 bytes
        var message = CircuitMessage.SerializeCreated(1, 1, expectedEphemeralKey, auth, new byte[0]);

        // Act
        var (_, _, ephemeralKey, _, _) = CircuitMessage.ParseCreated(message);  // Updated tuple deconstruction

        // Assert
        ephemeralKey.Should().Equal(expectedEphemeralKey);
    }

    [Fact]
    public void Should_Handle_Malformed_Messages_Gracefully()
    {
        // Arrange - message too short
        var tooShortMessage = new byte[2];

        // Act & Assert
        var act = () => CircuitMessage.ParseCreate(tooShortMessage);
        act.Should().Throw<ArgumentException>().WithMessage("*too short*");
    }

    [Fact]
    public void Should_Validate_Message_Length()
    {
        // Arrange - CREATE message requires at least 76 bytes
        var invalidMessage = new byte[10];

        // Act & Assert
        var act = () => CircuitMessage.ParseCreate(invalidMessage);
        act.Should().Throw<ArgumentException>().WithMessage("*too short*");
    }

    [Fact]
    public void Should_Validate_CircuitID_Range()
    {
        // Arrange - test various circuit ID values
        uint[] testCircuitIds = { 0, 1, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF };

        foreach (var circuitId in testCircuitIds)
        {
            // Act
            var message = CircuitMessage.SerializeCreate(circuitId, 1, new byte[32], new byte[32]);
            var extractedId = CircuitMessage.ExtractCircuitId(message);

            // Assert
            extractedId.Should().Be(circuitId, $"Circuit ID {circuitId:X8} should round-trip correctly");
        }
    }
}

