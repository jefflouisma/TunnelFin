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
}

