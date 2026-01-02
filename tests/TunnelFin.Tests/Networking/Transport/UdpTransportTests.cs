using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Tests.Networking.Transport;

public class UdpTransportTests : IDisposable
{
    private readonly List<UdpTransport> _transports = new();
    private readonly Mock<ILogger> _mockLogger = new();

    [Fact]
    public async Task StartAsync_Should_Bind_To_Random_Port_When_Port_Zero()
    {
        var transport = CreateTransport();

        await transport.StartAsync(port: 0);

        transport.IsRunning.Should().BeTrue();
        transport.LocalEndPoint.Should().NotBeNull();
        transport.LocalEndPoint!.Port.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StartAsync_Should_Bind_To_Specified_Port()
    {
        var transport = CreateTransport();
        var port = GetAvailablePort();

        await transport.StartAsync(port);

        transport.LocalEndPoint!.Port.Should().Be(port);
    }

    [Fact]
    public async Task StartAsync_Should_Throw_If_Port_In_Use()
    {
        var port = GetAvailablePort();
        var transport1 = CreateTransport();
        await transport1.StartAsync(port);

        var transport2 = CreateTransport();
        var act = async () => await transport2.StartAsync(port);

        await act.Should().ThrowAsync<SocketException>();
    }

    [Fact]
    public async Task StartAsync_Should_Throw_If_Already_Running()
    {
        var transport = CreateTransport();
        await transport.StartAsync();

        var act = async () => await transport.StartAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already running*");
    }

    [Fact]
    public async Task StopAsync_Should_Stop_Transport()
    {
        var transport = CreateTransport();
        await transport.StartAsync();

        await transport.StopAsync();

        transport.IsRunning.Should().BeFalse();
        transport.LocalEndPoint.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_Should_Send_Datagram()
    {
        var sender = CreateTransport();
        var receiver = CreateTransport();

        await sender.StartAsync();
        await receiver.StartAsync();

        var receivedTcs = new TaskCompletionSource<DatagramReceivedEventArgs>();
        receiver.DatagramReceived += (s, e) => receivedTcs.TrySetResult(e);

        var data = new byte[] { 1, 2, 3, 4, 5 };
        await sender.SendAsync(data, receiver.LocalEndPoint!);

        var received = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Data.ToArray().Should().Equal(data);
        received.RemoteEndPoint.Port.Should().Be(sender.LocalEndPoint!.Port);
    }

    [Fact]
    public async Task SendAsync_Should_Update_Metrics()
    {
        var sender = CreateTransport();
        var receiver = CreateTransport();

        await sender.StartAsync();
        await receiver.StartAsync();

        var data = new byte[100];
        await sender.SendAsync(data, receiver.LocalEndPoint!);
        await Task.Delay(50); // Allow receive loop to process

        sender.PacketsSent.Should().Be(1);
        sender.BytesSent.Should().Be(100);
    }

    [Fact]
    public async Task SendAsync_Should_Throw_If_Not_Running()
    {
        var transport = CreateTransport();
        var data = new byte[] { 1, 2, 3 };
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

        var act = async () => await transport.SendAsync(data, endpoint);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not running*");
    }

    [Fact]
    public async Task SendAsync_Should_Throw_If_Exceeds_MTU()
    {
        var transport = CreateTransport();
        await transport.StartAsync();

        var data = new byte[2000]; // Exceeds 1472 MTU
        var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

        var act = async () => await transport.SendAsync(data, endpoint);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*exceeds MTU*");
    }

    [Fact]
    public async Task DatagramReceived_Should_Fire_On_Receive()
    {
        var sender = CreateTransport();
        var receiver = CreateTransport();

        await sender.StartAsync();
        await receiver.StartAsync();

        var receivedCount = 0;
        receiver.DatagramReceived += (s, e) => receivedCount++;

        var data = new byte[] { 1, 2, 3 };
        await sender.SendAsync(data, receiver.LocalEndPoint!);
        await Task.Delay(100); // Allow event to fire

        receivedCount.Should().Be(1);
        receiver.PacketsReceived.Should().Be(1);
    }

    [Fact]
    public async Task Transport_Should_Handle_Concurrent_Operations()
    {
        var sender = CreateTransport();
        var receiver = CreateTransport();

        await sender.StartAsync();
        await receiver.StartAsync();

        var receivedCount = 0;
        var receiveLock = new object();
        receiver.DatagramReceived += (s, e) => { lock (receiveLock) receivedCount++; };

        // Send 100 packets concurrently
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var data = BitConverter.GetBytes(i);
            await sender.SendAsync(data, receiver.LocalEndPoint!);
        });

        await Task.WhenAll(tasks);
        await Task.Delay(500); // Allow all events to fire

        receivedCount.Should().Be(100);
        sender.PacketsSent.Should().Be(100);
        receiver.PacketsReceived.Should().Be(100);
    }

    [Fact]
    public async Task Transport_Should_Start_Within_Timeout()
    {
        // SC-001: Transport starts within timeout
        var transport = CreateTransport();
        var startTime = DateTime.UtcNow;

        await transport.StartAsync();

        var elapsed = DateTime.UtcNow - startTime;
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "transport should start quickly");
        transport.IsRunning.Should().BeTrue();
    }

    private UdpTransport CreateTransport()
    {
        var transport = new UdpTransport(_mockLogger.Object);
        _transports.Add(transport);
        return transport;
    }

    private static ushort GetAvailablePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    public void Dispose()
    {
        foreach (var transport in _transports)
        {
            transport.Dispose();
        }
    }
}

