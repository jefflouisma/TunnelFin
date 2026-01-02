using System.Net;
using System.Net.Sockets;

namespace TunnelFin.Networking.Transport;

/// <summary>
/// Abstraction for network transport layer (FR-004).
/// Provides UDP datagram send/receive capabilities for IPv8 protocol communication.
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Bind to port and start receiving packets.
    /// </summary>
    /// <param name="port">Port to bind (0 = random available port per py-ipv8 behavior).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="SocketException">Port in use or bind failed.</exception>
    Task StartAsync(ushort port = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Close socket and stop transport.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Send datagram to endpoint.
    /// </summary>
    /// <param name="data">Data to send.</param>
    /// <param name="endpoint">Destination endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of bytes sent.</returns>
    /// <exception cref="SocketException">Network error.</exception>
    Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive next datagram.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Received datagram and source endpoint.</returns>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Local endpoint the transport is bound to.
    /// </summary>
    IPEndPoint? LocalEndPoint { get; }

    /// <summary>
    /// Whether the transport is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Fired when datagram received (alternative to ReceiveAsync polling).
    /// </summary>
    event EventHandler<DatagramReceivedEventArgs>? DatagramReceived;
}

