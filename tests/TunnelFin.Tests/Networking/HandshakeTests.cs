using FluentAssertions;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.IPv8;
using Xunit;

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

}

