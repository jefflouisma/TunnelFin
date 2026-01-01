using FluentAssertions;
using TunnelFin.Configuration;
using TunnelFin.Networking.IPv8;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for IPv8 protocol implementation.
/// Tests peer discovery, handshake, and message serialization.
/// </summary>
public class IPv8ProtocolTests
{
    [Fact]
    public void IPv8Protocol_Should_Initialize_With_Valid_Configuration()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        using var protocol = new Protocol(settings);

        // Assert
        protocol.Should().NotBeNull();
        protocol.Identity.Should().NotBeNull();
        protocol.IsInitialized.Should().BeFalse("Protocol should not be initialized until InitializeAsync is called");
    }

    [Fact]
    public async Task IPv8Protocol_Should_Initialize_Successfully()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);

        // Act
        await protocol.InitializeAsync();

        // Assert
        protocol.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task IPv8Protocol_Should_Discover_Peers()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);
        await protocol.InitializeAsync();

        // Act
        var peers = await protocol.DiscoverPeersAsync();

        // Assert
        peers.Should().NotBeNull();
        // Note: Actual peer discovery will be implemented in later stages
    }

    [Fact]
    public async Task IPv8Protocol_Should_Perform_Handshake()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);
        await protocol.InitializeAsync();

        // Act
        var result = await protocol.PerformHandshakeAsync("127.0.0.1", 8080);

        // Assert
        // Note: Actual handshake will be implemented in later stages
        // For now, just verify it doesn't throw
        result.Should().BeFalse("Handshake not yet fully implemented");
    }

    [Fact]
    public void IPv8Protocol_Should_Serialize_Messages_Correctly()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var message = protocol.SerializeMessage(IPv8MessageType.IntroductionRequest, payload);

        // Assert
        message.Should().NotBeNull();
        message.Should().HaveCountGreaterThan(24, "Message should have 23-byte prefix + 1-byte type + payload");
        message[23].Should().Be(IPv8MessageType.IntroductionRequest, "Message type should be at byte 23");
    }

    [Fact]
    public void IPv8Protocol_Should_Deserialize_Messages_Correctly()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var message = protocol.SerializeMessage(IPv8MessageType.IntroductionResponse, payload);

        // Act
        var (messageType, deserializedPayload) = protocol.DeserializeMessage(message);

        // Assert
        messageType.Should().Be(IPv8MessageType.IntroductionResponse);
        deserializedPayload.Should().Equal(payload);
    }
}

