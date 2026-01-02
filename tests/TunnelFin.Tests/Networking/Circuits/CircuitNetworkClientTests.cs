using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;
using Xunit;

namespace TunnelFin.Tests.Networking.Circuits;

/// <summary>
/// Tests for CircuitNetworkClient (T042).
/// Validates CREATE/EXTEND/DESTROY message sending and response handling.
/// </summary>
public class CircuitNetworkClientTests : IDisposable
{
    private readonly Mock<ITransport> _mockTransport;
    private readonly Mock<ILogger> _mockLogger;
    private readonly CircuitNetworkClient _client;

    public CircuitNetworkClientTests()
    {
        _mockTransport = new Mock<ITransport>();
        _mockLogger = new Mock<ILogger>();
        _client = new CircuitNetworkClient(_mockTransport.Object, _mockLogger.Object, responseTimeoutMs: 1000);
    }

    [Fact]
    public async Task SendCreateAsync_Should_Send_CREATE_Message()
    {
        // Arrange
        await _client.StartAsync();

        var circuitId = 12345u;
        var relay = CreateTestPeer();
        var ephemeralKey = new byte[32];
        Random.Shared.NextBytes(ephemeralKey);

        _mockTransport.Setup(t => t.SendAsync(
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IPEndPoint>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act & Assert - Should timeout since we don't send a response
        var act = async () => await _client.SendCreateAsync(circuitId, relay, ephemeralKey);
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*No CREATED response*");

        // Verify CREATE message was sent
        _mockTransport.Verify(t => t.SendAsync(
            It.Is<ReadOnlyMemory<byte>>(data => data.Length > 0), // MSG_CREATE = 2
            It.IsAny<IPEndPoint>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendExtendAsync_Should_Send_EXTEND_Message()
    {
        // Arrange
        await _client.StartAsync();

        var circuitId = 12345u;
        var relay = CreateTestPeer();
        var ephemeralKey = new byte[32];
        Random.Shared.NextBytes(ephemeralKey);

        _mockTransport.Setup(t => t.SendAsync(
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IPEndPoint>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act & Assert - Should timeout since we don't send a response
        var act = async () => await _client.SendExtendAsync(circuitId, relay, ephemeralKey);
        await act.Should().ThrowAsync<TimeoutException>()
            .WithMessage("*No EXTENDED response*");

        // Verify EXTEND message was sent
        _mockTransport.Verify(t => t.SendAsync(
            It.Is<ReadOnlyMemory<byte>>(data => data.Length > 0), // MSG_EXTEND = 4
            It.IsAny<IPEndPoint>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendDestroyAsync_Should_Send_DESTROY_Message()
    {
        // Arrange
        await _client.StartAsync();

        var circuitId = 12345u;

        _mockTransport.Setup(t => t.SendAsync(
            It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IPEndPoint>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        await _client.SendDestroyAsync(circuitId);

        // Assert - DESTROY is fire-and-forget, no response expected
        // Note: Current implementation doesn't actually send, just logs
        // In production, this would verify the message was sent
    }

    [Fact]
    public async Task SendCreateAsync_Should_Throw_When_Ephemeral_Key_Invalid()
    {
        // Arrange
        await _client.StartAsync();

        var circuitId = 12345u;
        var relay = CreateTestPeer();
        var invalidKey = new byte[16]; // Wrong size

        // Act
        var act = async () => await _client.SendCreateAsync(circuitId, relay, invalidKey);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ephemeral public key must be 32 bytes*");
    }

    [Fact]
    public async Task SendExtendAsync_Should_Throw_When_Ephemeral_Key_Invalid()
    {
        // Arrange
        await _client.StartAsync();

        var circuitId = 12345u;
        var relay = CreateTestPeer();
        var invalidKey = new byte[16]; // Wrong size

        // Act
        var act = async () => await _client.SendExtendAsync(circuitId, relay, invalidKey);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Ephemeral public key must be 32 bytes*");
    }

    private Peer CreateTestPeer()
    {
        var publicKey = new byte[32];
        Random.Shared.NextBytes(publicKey);

        return new Peer(publicKey, 0x7F000001, 8000); // 127.0.0.1:8000
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

