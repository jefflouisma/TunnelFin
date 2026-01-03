using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TunnelFin.Networking;

/// <summary>
/// Custom socket connector for routing BitTorrent peer connections through Tribler circuits.
/// Implements MonoTorrent's ISocketConnector interface.
/// </summary>
public interface ITunnelSocketConnector
{
    /// <summary>
    /// Connects to a peer endpoint through a Tribler circuit.
    /// Uses SOCKS5 proxy pattern over circuit infrastructure.
    /// </summary>
    /// <param name="endpoint">Peer IP endpoint to connect to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connected socket routed through circuit</returns>
    Task<Socket> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Connects to a peer endpoint through a specific circuit.
    /// Useful when stream session has pre-allocated circuit.
    /// </summary>
    /// <param name="endpoint">Peer IP endpoint to connect to</param>
    /// <param name="circuitId">Circuit ID to use for connection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connected socket routed through specified circuit</returns>
    Task<Socket> ConnectAsync(IPEndPoint endpoint, Guid circuitId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a healthy circuit from the pool or creates a new one.
    /// Implements exponential backoff if no healthy circuits available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Healthy circuit metadata</returns>
    Task<CircuitMetadata> GetHealthyCircuitAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns a circuit to the pool after use.
    /// Marks circuit as unhealthy if connection failed.
    /// </summary>
    /// <param name="circuitId">Circuit ID to return</param>
    /// <param name="healthy">Whether circuit is still healthy</param>
    void ReturnCircuit(Guid circuitId, bool healthy);

    /// <summary>
    /// Enables or disables circuit routing.
    /// When disabled, connections are made directly (privacy warning shown to user).
    /// </summary>
    /// <param name="enabled">Whether to route through circuits</param>
    void SetCircuitRoutingEnabled(bool enabled);

    /// <summary>
    /// Gets whether circuit routing is currently enabled.
    /// </summary>
    /// <returns>True if routing through circuits, false if direct connections</returns>
    bool IsCircuitRoutingEnabled();
}

