using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Networking.Tunnel;

public class TunnelProxyTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly TunnelProxy _tunnelProxy;

    public TunnelProxyTests()
    {
        _mockLogger = new Mock<ILogger>();
        _tunnelProxy = new TunnelProxy(_mockLogger.Object);
    }

    [Fact]
    public async Task StartAsync_Should_Start_Proxy()
    {
        // Act
        await _tunnelProxy.StartAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task StopAsync_Should_Stop_Proxy()
    {
        // Arrange
        await _tunnelProxy.StartAsync();

        // Act
        await _tunnelProxy.StopAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task CreateTunnelAsync_Should_Throw_When_Not_Running()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);

        // Act
        var act = async () => await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TunnelProxy is not running");
    }

    [Fact]
    public async Task CreateTunnelAsync_Should_Create_Tunnel_Stream()
    {
        // Arrange
        await _tunnelProxy.StartAsync();
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);

        // Act
        var stream = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Assert
        stream.Should().NotBeNull();
        stream.Circuit.Should().Be(circuit);
        stream.RemoteEndpoint.Should().Be(endpoint);
        stream.StreamId.Should().BeGreaterThan((ushort)0);
    }

    [Fact]
    public async Task CreateTunnelAsync_Should_Throw_When_Circuit_Not_Established()
    {
        // Arrange
        await _tunnelProxy.StartAsync();
        var circuit = new Circuit(1, 3, 600);
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);

        // Act
        var act = async () => await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Circuit must be established*");
    }

    [Fact]
    public async Task CloseTunnelAsync_Should_Close_Tunnel()
    {
        // Arrange
        await _tunnelProxy.StartAsync();
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Act
        await _tunnelProxy.CloseTunnelAsync(stream.StreamId);

        // Assert - no exception thrown
    }

    [Fact]
    public async Task CreateTunnelAsync_Should_Generate_Unique_Stream_IDs()
    {
        // Arrange
        await _tunnelProxy.StartAsync();
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);

        // Act
        var stream1 = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);
        var stream2 = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Assert
        stream1.StreamId.Should().NotBe(stream2.StreamId);
    }

    [Fact]
    public async Task StopAsync_Should_Close_All_Active_Streams()
    {
        // Arrange
        await _tunnelProxy.StartAsync();
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream1 = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);
        var stream2 = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint);

        // Act
        await _tunnelProxy.StopAsync();

        // Assert - streams should be disposed (no exception on dispose)
        var act1 = () => stream1.Write(new byte[10], 0, 10);
        var act2 = () => stream2.Write(new byte[10], 0, 10);

        act1.Should().Throw<ObjectDisposedException>();
        act2.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_Should_Stop_Proxy_And_Close_Streams()
    {
        // Arrange
        var proxy = new TunnelProxy(_mockLogger.Object);

        // Act
        proxy.Dispose();

        // Assert - no exception thrown
    }

    private Circuit CreateTestCircuit()
    {
        var circuit = new Circuit(1, 3, 600);

        // Add 3 hops to make it established
        for (int i = 0; i < 3; i++)
        {
            // Create ephemeral key pair for this hop
            var ephemeralPrivateKey = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519);
            var ephemeralPublicKey = ephemeralPrivateKey.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

            var hop = new HopNode(new byte[32], (uint)(192 << 24 | 168 << 16 | 1 << 8 | (i + 1)), 8080, i);
            hop.CompleteKeyExchange(ephemeralPublicKey, ephemeralPrivateKey);
            circuit.AddHop(hop);
        }

        return circuit;
    }
}

