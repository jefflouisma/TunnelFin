using FluentAssertions;
using TunnelFin.Configuration;
using TunnelFin.Networking.Identity;
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

    [Fact]
    public void Constructor_Should_Throw_When_Settings_Is_Null()
    {
        // Act
        var act = () => new Protocol(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void Constructor_Should_Accept_Custom_Identity()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var identity = new Ed25519KeyPair();

        // Act
        using var protocol = new Protocol(settings, identity);

        // Assert
        protocol.Identity.Should().BeSameAs(identity);
    }

    [Fact]
    public async Task InitializeAsync_Should_Be_Idempotent()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);

        // Act
        await protocol.InitializeAsync();
        await protocol.InitializeAsync(); // Call again

        // Assert
        protocol.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_Should_Throw_When_Disposed()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);
        protocol.Dispose();

        // Act
        var act = async () => await protocol.InitializeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PerformHandshakeAsync_Should_Throw_When_Disposed()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);
        protocol.Dispose();

        // Act
        var act = async () => await protocol.PerformHandshakeAsync("127.0.0.1", 8080);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PerformHandshakeAsync_Should_Throw_When_Not_Initialized()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);

        // Act
        var act = async () => await protocol.PerformHandshakeAsync("127.0.0.1", 8080);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Throw_When_Disposed()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);
        protocol.Dispose();

        // Act
        var act = async () => await protocol.DiscoverPeersAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DiscoverPeersAsync_Should_Throw_When_Not_Initialized()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);

        // Act
        var act = async () => await protocol.DiscoverPeersAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void SerializeMessage_Should_Throw_When_Disposed()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);
        protocol.Dispose();

        // Act
        var act = () => protocol.SerializeMessage(IPv8MessageType.IntroductionRequest, new byte[4]);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DeserializeMessage_Should_Throw_When_Disposed()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);
        protocol.Dispose();

        // Act
        var act = () => protocol.DeserializeMessage(new byte[24]);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void DeserializeMessage_Should_Throw_When_Message_Too_Short()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);

        // Act
        var act = () => protocol.DeserializeMessage(new byte[23]); // Too short

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("message")
            .WithMessage("*too short*");
    }

    [Fact]
    public void SerializeMessage_Should_Include_23Byte_Prefix()
    {
        // Arrange
        var settings = new AnonymitySettings();
        using var protocol = new Protocol(settings);
        var payload = new byte[] { 0xAA, 0xBB };

        // Act
        var message = protocol.SerializeMessage(IPv8MessageType.Create, payload);

        // Assert
        message.Length.Should().Be(24 + 2, "should have 23-byte prefix + 1-byte type + 2-byte payload");
        message[23].Should().Be(IPv8MessageType.Create);
        message[24].Should().Be(0xAA);
        message[25].Should().Be(0xBB);
    }

    [Fact]
    public void Dispose_Should_Be_Idempotent()
    {
        // Arrange
        var settings = new AnonymitySettings();
        var protocol = new Protocol(settings);

        // Act
        protocol.Dispose();
        var act = () => protocol.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}

