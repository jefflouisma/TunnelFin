using System.Net;
using System.Net.Sockets;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Socket wrapper that routes I/O through a TunnelStream for circuit-based communication.
/// Provides a Socket-like interface for MonoTorrent integration.
/// </summary>
public class TunnelSocket : Socket
{
    private readonly TunnelStream _tunnelStream;
    private readonly IPEndPoint _remoteEndpoint;
    private bool _connected;

    /// <summary>
    /// The underlying tunnel stream.
    /// </summary>
    public TunnelStream TunnelStream => _tunnelStream;

    /// <summary>
    /// Creates a new tunnel socket.
    /// </summary>
    /// <param name="tunnelStream">The tunnel stream to wrap.</param>
    /// <param name="remoteEndpoint">The remote endpoint.</param>
    public TunnelSocket(TunnelStream tunnelStream, IPEndPoint remoteEndpoint)
        : base(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    {
        _tunnelStream = tunnelStream ?? throw new ArgumentNullException(nameof(tunnelStream));
        _remoteEndpoint = remoteEndpoint ?? throw new ArgumentNullException(nameof(remoteEndpoint));
        _connected = true; // Tunnel is already "connected" when created
    }

    /// <summary>
    /// Gets the remote endpoint.
    /// </summary>
    public new EndPoint? RemoteEndPoint => _remoteEndpoint;

    /// <summary>
    /// Gets whether the socket is connected.
    /// </summary>
    public new bool Connected => _connected && !_tunnelStream.Circuit.IsExpired;

    /// <summary>
    /// Receives data from the tunnel stream.
    /// </summary>
    public new int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (!_connected)
            throw new SocketException((int)SocketError.NotConnected);

        return _tunnelStream.Read(buffer, offset, size);
    }

    /// <summary>
    /// Receives data from the tunnel stream asynchronously.
    /// </summary>
    public new Task<int> ReceiveAsync(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (!_connected)
            throw new SocketException((int)SocketError.NotConnected);

        return _tunnelStream.ReadAsync(buffer, offset, size);
    }

    /// <summary>
    /// Sends data through the tunnel stream.
    /// </summary>
    public new int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (!_connected)
            throw new SocketException((int)SocketError.NotConnected);

        _tunnelStream.Write(buffer, offset, size);
        return size; // Assume all bytes written
    }

    /// <summary>
    /// Sends data through the tunnel stream asynchronously.
    /// </summary>
    public new async Task<int> SendAsync(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (!_connected)
            throw new SocketException((int)SocketError.NotConnected);

        await _tunnelStream.WriteAsync(buffer, offset, size);
        return size; // Assume all bytes written
    }

    /// <summary>
    /// Closes the tunnel socket.
    /// </summary>
    public new void Close()
    {
        if (_connected)
        {
            _connected = false;
            _tunnelStream.Dispose();
        }
        base.Close();
    }

    /// <summary>
    /// Disposes the tunnel socket.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _connected)
        {
            _connected = false;
            _tunnelStream.Dispose();
        }
        base.Dispose(disposing);
    }
}

