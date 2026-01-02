using TunnelFin.Networking.IPv8;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Network layer for circuit operations over UDP transport (T034).
/// Handles CREATE/EXTEND/DESTROY messages for circuit establishment.
/// </summary>
public interface ICircuitNetworkClient
{
    /// <summary>
    /// Sends a CREATE message to establish the first hop of a circuit (FR-014).
    /// </summary>
    /// <param name="circuitId">Circuit identifier.</param>
    /// <param name="relay">First hop relay peer.</param>
    /// <param name="ephemeralPublicKey">Ephemeral public key for key exchange (32 bytes, Curve25519).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CreateResponse containing the relay's ephemeral key and auth.</returns>
    /// <exception cref="TimeoutException">No CREATED response within timeout.</exception>
    Task<CreateResponse> SendCreateAsync(
        uint circuitId,
        Peer relay,
        byte[] ephemeralPublicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an EXTEND message to add another hop to an existing circuit (FR-015, FR-016).
    /// </summary>
    /// <param name="circuitId">Circuit identifier.</param>
    /// <param name="nextRelay">Next hop relay peer.</param>
    /// <param name="ephemeralPublicKey">Ephemeral public key for key exchange (32 bytes, Curve25519).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ExtendResponse containing the relay's ephemeral key and auth.</returns>
    /// <exception cref="TimeoutException">No EXTENDED response within timeout.</exception>
    Task<ExtendResponse> SendExtendAsync(
        uint circuitId,
        Peer nextRelay,
        byte[] ephemeralPublicKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a DESTROY message to tear down a circuit (FR-018).
    /// </summary>
    /// <param name="circuitId">Circuit identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when the message is sent.</returns>
    Task SendDestroyAsync(
        uint circuitId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts listening for circuit responses.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when listening starts.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops listening for circuit responses.
    /// </summary>
    /// <returns>Task that completes when listening stops.</returns>
    Task StopAsync();
}

