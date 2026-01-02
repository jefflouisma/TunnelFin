using FluentAssertions;
using System.Net;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;
using TunnelFin.Networking.Tunnel;
using Xunit;

namespace TunnelFin.Tests.Networking.Tunnel;

public class TunnelStreamTests
{
    [Fact]
    public void Constructor_Should_Initialize_Stream()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        ushort streamId = 1;

        // Act
        var stream = new TunnelStream(circuit, endpoint, streamId);

        // Assert
        stream.StreamId.Should().Be(streamId);
        stream.Circuit.Should().Be(circuit);
        stream.RemoteEndpoint.Should().Be(endpoint);
        stream.CanRead.Should().BeTrue();
        stream.CanWrite.Should().BeTrue();
        stream.CanSeek.Should().BeFalse();
    }

    [Fact]
    public void Write_Should_Increment_BytesSent()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        stream.Write(data, 0, data.Length);

        // Assert
        stream.BytesSent.Should().Be(data.Length);
    }

    [Fact]
    public async Task WriteAsync_Should_Increment_BytesSent()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await stream.WriteAsync(data, 0, data.Length);

        // Assert
        stream.BytesSent.Should().Be(data.Length);
    }

    [Fact]
    public void Read_Should_Return_Zero_When_No_Data()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        var buffer = new byte[10];

        // Act
        var bytesRead = stream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_Should_Return_Zero_When_No_Data()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        var buffer = new byte[10];

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(0);
    }

    [Fact]
    public void Write_Should_Throw_When_Disposed()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        stream.Dispose();

        // Act
        var act = () => stream.Write(new byte[10], 0, 10);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Read_Should_Throw_When_Disposed()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);
        stream.Dispose();

        // Act
        var act = () => stream.Read(new byte[10], 0, 10);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Seek_Should_Throw_NotSupportedException()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);

        // Act
        var act = () => stream.Seek(0, SeekOrigin.Begin);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetLength_Should_Throw_NotSupportedException()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);

        // Act
        var act = () => stream.SetLength(100);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Position_Set_Should_Throw_NotSupportedException()
    {
        // Arrange
        var circuit = CreateTestCircuit();
        var endpoint = new IPEndPoint(IPAddress.Parse("192.168.1.1"), 8080);
        var stream = new TunnelStream(circuit, endpoint, 1);

        // Act
        var act = () => stream.Position = 100;

        // Assert
        act.Should().Throw<NotSupportedException>();
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

