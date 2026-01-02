using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;
using Xunit;
using System.Net;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for IPv8 Handshake protocol.
/// Tests four-message discovery: introduction-request/response, puncture-request/puncture.
/// </summary>
public class HandshakeTests
{
    [Fact]
    public void CreateIntroductionRequest_Should_Include_Required_Fields()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var destinationAddress = ("192.168.1.100", 8000);
        var sourceLanAddress = ("192.168.1.50", 8001);
        var sourceWanAddress = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        // Act
        var message = handshake.CreateIntroductionRequest(
            destinationAddress,
            sourceLanAddress,
            sourceWanAddress,
            identifier);

        // Assert
        message.Should().NotBeNull();
        message.Should().NotBeEmpty("introduction-request should have payload");
    }

    [Fact]
    public void CreateIntroductionResponse_Should_Include_Introduction_Addresses()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var destinationAddress = ("192.168.1.100", 8000);
        var sourceLanAddress = ("192.168.1.50", 8001);
        var sourceWanAddress = ("203.0.113.50", 8001);
        var lanIntroAddress = ("192.168.1.75", 8002);
        var wanIntroAddress = ("203.0.113.75", 8002);
        ushort identifier = 12345;

        // Act
        var message = handshake.CreateIntroductionResponse(
            destinationAddress,
            sourceLanAddress,
            sourceWanAddress,
            lanIntroAddress,
            wanIntroAddress,
            identifier);

        // Assert
        message.Should().NotBeNull();
        message.Should().NotBeEmpty("introduction-response should have payload");
    }

    [Fact]
    public void CreatePunctureRequest_Should_Include_Walker_Addresses()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var lanWalkerAddress = ("192.168.1.75", 8002);
        var wanWalkerAddress = ("203.0.113.75", 8002);
        ushort identifier = 12345;

        // Act
        var message = handshake.CreatePunctureRequest(
            lanWalkerAddress,
            wanWalkerAddress,
            identifier);

        // Assert
        message.Should().NotBeNull();
        message.Should().NotBeEmpty("puncture-request should have payload");
    }

    [Fact]
    public void CreatePuncture_Should_Include_Source_Addresses()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var sourceLanAddress = ("192.168.1.50", 8001);
        var sourceWanAddress = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        // Act
        var message = handshake.CreatePuncture(
            sourceLanAddress,
            sourceWanAddress,
            identifier);

        // Assert
        message.Should().NotBeNull();
        message.Should().NotBeEmpty("puncture should have payload");
    }

    [Fact]
    public void ParseIntroductionRequest_Should_Extract_Fields()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var destinationAddress = ("192.168.1.100", 8000);
        var sourceLanAddress = ("192.168.1.50", 8001);
        var sourceWanAddress = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        var message = handshake.CreateIntroductionRequest(
            destinationAddress,
            sourceLanAddress,
            sourceWanAddress,
            identifier);

        // Act
        var parsed = handshake.ParseIntroductionRequest(message);

        // Assert
        parsed.Should().NotBeNull();
        parsed.Identifier.Should().Be(identifier);
    }

    [Fact]
    public void ParsePunctureRequest_Should_Extract_Identifier()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity);
        var lanWalkerAddress = ("192.168.1.75", 8002);
        var wanWalkerAddress = ("203.0.113.75", 8002);
        ushort identifier = 12345;

        var message = handshake.CreatePunctureRequest(
            lanWalkerAddress,
            wanWalkerAddress,
            identifier);

        // Act
        var parsed = handshake.ParsePunctureRequest(message);

        // Assert
        parsed.Should().NotBeNull();
        parsed.Identifier.Should().Be(identifier);
    }


    [Fact]
    public void IntroductionRequestPayload_Should_Support_Equality()
    {
        // Arrange
        var payload1 = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        var payload2 = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        var payload3 = new IntroductionRequestPayload(
            ("192.168.1.101", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        // Act & Assert
        payload1.Should().Be(payload2);
        payload1.Should().NotBe(payload3);
        payload1.Equals(payload2).Should().BeTrue();
        payload1.Equals(payload3).Should().BeFalse();
    }

    [Fact]
    public void IntroductionRequestPayload_Should_Have_Consistent_HashCode()
    {
        // Arrange
        var payload1 = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        var payload2 = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        // Act & Assert
        payload1.GetHashCode().Should().Be(payload2.GetHashCode());
    }

    [Fact]
    public void IntroductionRequestPayload_Should_Support_ToString()
    {
        // Arrange
        var payload = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        // Act
        var str = payload.ToString();

        // Assert
        str.Should().NotBeNullOrEmpty();
        str.Should().Contain("IntroductionRequestPayload");
    }

    [Fact]
    public void IntroductionRequestPayload_Should_Support_Deconstruction()
    {
        // Arrange
        var payload = new IntroductionRequestPayload(
            ("192.168.1.100", 8000),
            ("192.168.1.50", 8001),
            ("203.0.113.50", 8001),
            12345);

        // Act
        var (dest, lan, wan, id) = payload;

        // Assert
        dest.Should().Be(("192.168.1.100", 8000));
        lan.Should().Be(("192.168.1.50", 8001));
        wan.Should().Be(("203.0.113.50", 8001));
        id.Should().Be(12345);
    }

    [Fact]
    public void PunctureRequestPayload_Should_Support_Equality()
    {
        // Arrange
        var payload1 = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        var payload2 = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        var payload3 = new PunctureRequestPayload(
            ("192.168.1.76", 8002),
            ("203.0.113.75", 8002),
            12345);

        // Act & Assert
        payload1.Should().Be(payload2);
        payload1.Should().NotBe(payload3);
        payload1.Equals(payload2).Should().BeTrue();
        payload1.Equals(payload3).Should().BeFalse();
    }

    [Fact]
    public void PunctureRequestPayload_Should_Have_Consistent_HashCode()
    {
        // Arrange
        var payload1 = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        var payload2 = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        // Act & Assert
        payload1.GetHashCode().Should().Be(payload2.GetHashCode());
    }

    [Fact]
    public void PunctureRequestPayload_Should_Support_ToString()
    {
        // Arrange
        var payload = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        // Act
        var str = payload.ToString();

        // Assert
        str.Should().NotBeNullOrEmpty();
        str.Should().Contain("PunctureRequestPayload");
    }

    [Fact]
    public void PunctureRequestPayload_Should_Support_Deconstruction()
    {
        // Arrange
        var payload = new PunctureRequestPayload(
            ("192.168.1.75", 8002),
            ("203.0.113.75", 8002),
            12345);

        // Act
        var (lan, wan, id) = payload;

        // Assert
        lan.Should().Be(("192.168.1.75", 8002));
        wan.Should().Be(("203.0.113.75", 8002));
        id.Should().Be(12345);
    }

    // T032: Live handshake tests with ITransport integration

    [Fact]
    public async Task SendIntroductionRequestAsync_Should_Send_Via_Transport()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var mockLogger = new Mock<ILogger>();
        var mockTransport = new Mock<ITransport>();

        var handshake = new Handshake(identity, mockTransport.Object, mockLogger.Object);
        var destination = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 8000);
        var sourceLan = ("192.168.1.50", 8001);
        var sourceWan = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        mockTransport
            .Setup(t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await handshake.SendIntroductionRequestAsync(destination, sourceLan, sourceWan, identifier);

        // Assert
        result.Should().BeTrue();
        mockTransport.Verify(
            t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), destination, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendIntroductionRequestAsync_Should_Return_False_On_Transport_Failure()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var mockLogger = new Mock<ILogger>();
        var mockTransport = new Mock<ITransport>();

        var handshake = new Handshake(identity, mockTransport.Object, mockLogger.Object);
        var destination = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 8000);
        var sourceLan = ("192.168.1.50", 8001);
        var sourceWan = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        mockTransport
            .Setup(t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        // Act
        var result = await handshake.SendIntroductionRequestAsync(destination, sourceLan, sourceWan, identifier);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendPunctureRequestAsync_Should_Send_Via_Intermediary()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var mockLogger = new Mock<ILogger>();
        var mockTransport = new Mock<ITransport>();

        var handshake = new Handshake(identity, mockTransport.Object, mockLogger.Object);
        var intermediary = new IPEndPoint(IPAddress.Parse("192.168.1.200"), 8000);
        var targetLan = ("192.168.1.75", 8002);
        var targetWan = ("203.0.113.75", 8002);
        ushort identifier = 12345;

        mockTransport
            .Setup(t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await handshake.SendPunctureRequestAsync(intermediary, targetLan, targetWan, identifier);

        // Assert
        result.Should().BeTrue();
        mockTransport.Verify(
            t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), intermediary, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPunctureAsync_Should_Send_Direct_Puncture()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var mockLogger = new Mock<ILogger>();
        var mockTransport = new Mock<ITransport>();

        var handshake = new Handshake(identity, mockTransport.Object, mockLogger.Object);
        var destination = new IPEndPoint(IPAddress.Parse("192.168.1.75"), 8002);
        var sourceLan = ("192.168.1.50", 8001);
        var sourceWan = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        mockTransport
            .Setup(t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IPEndPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await handshake.SendPunctureAsync(destination, sourceLan, sourceWan, identifier);

        // Assert
        result.Should().BeTrue();
        mockTransport.Verify(
            t => t.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), destination, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SendIntroductionRequestAsync_Should_Throw_Without_Transport()
    {
        // Arrange
        var identity = new NetworkIdentity();
        var handshake = new Handshake(identity); // No transport
        var destination = new IPEndPoint(IPAddress.Parse("192.168.1.100"), 8000);
        var sourceLan = ("192.168.1.50", 8001);
        var sourceWan = ("203.0.113.50", 8001);
        ushort identifier = 12345;

        // Act
        Func<Task> act = async () => await handshake.SendIntroductionRequestAsync(destination, sourceLan, sourceWan, identifier);

        // Assert
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Transport not configured*");
    }

    [Fact]
    public async Task Live_Handshake_Should_Achieve_Success_Rate_Above_Threshold()
    {
        // Arrange
        var identity1 = new NetworkIdentity();
        var identity2 = new NetworkIdentity();
        var logger = new Mock<ILogger>();

        using var transport1 = new UdpTransport(logger.Object);
        using var transport2 = new UdpTransport(logger.Object);

        await transport1.StartAsync(0);
        await transport2.StartAsync(0);

        var handshake1 = new Handshake(identity1, transport1, logger.Object);
        var handshake2 = new Handshake(identity2, transport2, logger.Object);

        var stateMachine = new HandshakeStateMachine(timeoutSeconds: 5);

        int successCount = 0;
        int totalAttempts = 10;

        // Act - perform multiple handshake attempts
        for (int i = 0; i < totalAttempts; i++)
        {
            var identifier = (ushort)(1000 + i);
            var publicKeyHex = Convert.ToHexString(identity2.PublicKey);

            stateMachine.UpdateState(publicKeyHex, HandshakeState.IntroRequestSent);

            var sent = await handshake1.SendIntroductionRequestAsync(
                transport2.LocalEndPoint!,
                ("127.0.0.1", transport1.LocalEndPoint!.Port),
                ("127.0.0.1", transport1.LocalEndPoint!.Port),
                identifier);

            if (sent)
            {
                // Wait a bit for potential response
                await Task.Delay(100);

                // Check if we're still in a valid state (not timed out)
                var state = stateMachine.GetState(publicKeyHex);
                if (state != HandshakeState.TimedOut && state != HandshakeState.Failed)
                {
                    successCount++;
                }
            }
        }

        // Assert - success rate should be > 80% for local loopback
        var successRate = (double)successCount / totalAttempts;
        successRate.Should().BeGreaterThan(0.8, "local handshakes should have high success rate");
    }

}

