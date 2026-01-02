using System.Net;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Routes TCP connections through anonymity circuits.
/// Provides tunnel creation and management for BitTorrent traffic.
/// </summary>
public interface ITunnelProxy : IDisposable
{
    /// <summary>
    /// Creates a tunnel stream through the specified circuit to the remote endpoint.
    /// </summary>
    /// <param name="circuit">The circuit to route traffic through.</param>
    /// <param name="remoteEndpoint">The destination endpoint (peer IP:port).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tunnel stream for bidirectional communication.</returns>
    Task<TunnelStream> CreateTunnelAsync(Circuit circuit, IPEndPoint remoteEndpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes a tunnel stream by its stream ID.
    /// </summary>
    /// <param name="streamId">The unique stream identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CloseTunnelAsync(ushort streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the tunnel proxy service.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the tunnel proxy service.
    /// </summary>
    Task StopAsync();
}

