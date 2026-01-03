using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MonoTorrent.Connections;
using NSec.Cryptography;
using ReusableTasks;
using System.Net;
using System.Net.Sockets;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Unit.Networking;

public class TunnelSocketConnectorTests
{
    private readonly CircuitManager _circuitManager;
    private readonly Mock<ITunnelProxy> _mockTunnelProxy;
    private readonly ILogger _logger;
    private readonly Mock<ISocketConnector> _mockFallbackConnector;
    private readonly AnonymitySettings _settings;
    private readonly TunnelSocketConnector _connector;

    public TunnelSocketConnectorTests()
    {
        _settings = new AnonymitySettings
        {
            DefaultHopCount = 3,
            AllowNonAnonymousFallback = false
        };

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<TunnelSocketConnectorTests>();
        _circuitManager = new CircuitManager(_settings);
        _mockTunnelProxy = new Mock<ITunnelProxy>();
        _mockFallbackConnector = new Mock<ISocketConnector>();

        _connector = new TunnelSocketConnector(
            _circuitManager,
            _mockTunnelProxy.Object,
            _settings,
            _logger,
            _mockFallbackConnector.Object);
    }

    [Fact]
    public async Task ConnectAsync_WithEstablishedCircuit_RoutesThroughTunnelProxy()
    {
        // Arrange
        var uri = new Uri("ipv4://192.168.1.100:6881");
        var circuit = CreateEstablishedCircuit();
        var mockTunnelStream = CreateMockTunnelStream(circuit);

        // Add circuit to manager using reflection
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit.IPv8CircuitId] = circuit;

        _mockTunnelProxy.Setup(m => m.CreateTunnelAsync(
                It.IsAny<Circuit>(),
                It.IsAny<IPEndPoint>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTunnelStream);

        // Act
        var socket = await _connector.ConnectAsync(uri, CancellationToken.None);

        // Assert
        socket.Should().NotBeNull();
        socket.Should().BeOfType<TunnelSocket>();
        _mockTunnelProxy.Verify(m => m.CreateTunnelAsync(
            circuit,
            It.Is<IPEndPoint>(ep => ep.Address.ToString() == "192.168.1.100" && ep.Port == 6881),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_NoCircuitAvailable_WithFallbackEnabled_UsesFallbackConnector()
    {
        // Arrange
        _settings.AllowNonAnonymousFallback = true;
        var uri = new Uri("ipv4://192.168.1.100:6881");
        var fallbackSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // CircuitManager has no circuits by default
        _mockFallbackConnector.Setup(m => m.ConnectAsync(uri, It.IsAny<CancellationToken>()))
            .Returns(ReusableTask.FromResult(fallbackSocket));

        // Act
        var socket = await _connector.ConnectAsync(uri, CancellationToken.None);

        // Assert
        socket.Should().Be(fallbackSocket);
        _mockFallbackConnector.Verify(m => m.ConnectAsync(uri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_NoCircuitAvailable_WithFallbackDisabled_ThrowsException()
    {
        // Arrange
        _settings.AllowNonAnonymousFallback = false;
        var uri = new Uri("ipv4://192.168.1.100:6881");

        // CircuitManager has no circuits by default

        // Act
        var act = async () => await _connector.ConnectAsync(uri, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*non-anonymous fallback is disabled*");
    }

    [Fact]
    public async Task ConnectAsync_CircuitFailure_WithFallbackEnabled_UsesFallbackConnector()
    {
        // Arrange
        _settings.AllowNonAnonymousFallback = true;
        var uri = new Uri("ipv4://192.168.1.100:6881");
        var circuit = CreateEstablishedCircuit();
        var fallbackSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Add circuit to manager using reflection
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit.IPv8CircuitId] = circuit;

        _mockTunnelProxy.Setup(m => m.CreateTunnelAsync(
                It.IsAny<Circuit>(),
                It.IsAny<IPEndPoint>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Circuit failed"));
        _mockFallbackConnector.Setup(m => m.ConnectAsync(uri, It.IsAny<CancellationToken>()))
            .Returns(ReusableTask.FromResult(fallbackSocket));

        // Act
        var socket = await _connector.ConnectAsync(uri, CancellationToken.None);

        // Assert
        socket.Should().Be(fallbackSocket);
        _mockFallbackConnector.Verify(m => m.ConnectAsync(uri, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_IPv6Address_RoutesCorrectly()
    {
        // Arrange
        var uri = new Uri("ipv6://[2001:db8::1]:6881");
        var circuit = CreateEstablishedCircuit();
        var mockTunnelStream = CreateMockTunnelStream(circuit);

        // Add circuit to manager using reflection
        var circuitsField = typeof(CircuitManager).GetField("_circuits",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var circuits = (Dictionary<uint, Circuit>)circuitsField!.GetValue(_circuitManager)!;
        circuits[circuit.IPv8CircuitId] = circuit;

        _mockTunnelProxy.Setup(m => m.CreateTunnelAsync(
                It.IsAny<Circuit>(),
                It.IsAny<IPEndPoint>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTunnelStream);

        // Act
        var socket = await _connector.ConnectAsync(uri, CancellationToken.None);

        // Assert
        socket.Should().NotBeNull();
        socket.Should().BeOfType<TunnelSocket>();
        _mockTunnelProxy.Verify(m => m.CreateTunnelAsync(
            circuit,
            It.Is<IPEndPoint>(ep => ep.Address.ToString() == "2001:db8::1" && ep.Port == 6881),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var uri = new Uri("ipv4://192.168.1.100:6881");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _connector.ConnectAsync(uri, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ConnectAsync_NullUri_ThrowsArgumentNullException()
    {
        // Act
        var act = async () => await _connector.ConnectAsync(null!, CancellationToken.None);

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    private Circuit CreateEstablishedCircuit()
    {
        var circuit = new Circuit(12345, 3, 600);

        // Add 3 hops to make it established
        for (int i = 0; i < 3; i++)
        {
            // Create ephemeral key pair for this hop
            var ephemeralPrivateKey = NSec.Cryptography.Key.Create(NSec.Cryptography.KeyAgreementAlgorithm.X25519);
            var ephemeralPublicKey = ephemeralPrivateKey.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

            var publicKey = new byte[32];
            publicKey[0] = (byte)(i + 1);
            var hop = new HopNode(publicKey, (uint)(0x7F000001 + i), (ushort)(8000 + i), i);
            hop.CompleteKeyExchange(ephemeralPublicKey, ephemeralPrivateKey);
            circuit.AddHop(hop);
        }

        return circuit;
    }

    private TunnelStream CreateMockTunnelStream(Circuit circuit)
    {
        return new TunnelStream(circuit, new IPEndPoint(IPAddress.Parse("192.168.1.100"), 6881), 1);
    }
}

