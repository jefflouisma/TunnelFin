namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Interface for bootstrap peer discovery (FR-005, FR-006).
/// Manages contact with TU Delft bootstrap nodes and peer table population.
/// </summary>
public interface IBootstrapManager
{
    /// <summary>
    /// Gets the current bootstrap status.
    /// </summary>
    BootstrapStatus Status { get; }

    /// <summary>
    /// Gets the peer table populated by bootstrap discovery.
    /// </summary>
    IPeerTable PeerTable { get; }

    /// <summary>
    /// Gets the list of bootstrap nodes.
    /// </summary>
    IReadOnlyList<BootstrapNode> BootstrapNodes { get; }

    /// <summary>
    /// Discovers peers by contacting bootstrap nodes (FR-006).
    /// Sends introduction-request messages and populates peer table from responses.
    /// </summary>
    /// <param name="timeoutSeconds">Timeout for bootstrap discovery (default: 30s).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when bootstrap is done or timeout occurs.</returns>
    Task DiscoverPeersAsync(int timeoutSeconds = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the peer table by re-contacting bootstrap nodes (FR-008).
    /// Should be called periodically (every 5 minutes).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when refresh is done.</returns>
    Task RefreshPeersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts periodic peer refresh (FR-008).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop refresh.</param>
    /// <returns>Task that runs until cancelled.</returns>
    Task StartPeriodicRefreshAsync(CancellationToken cancellationToken = default);
}

