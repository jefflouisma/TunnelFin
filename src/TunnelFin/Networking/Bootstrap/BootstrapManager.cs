using Microsoft.Extensions.Logging;
using TunnelFin.Core;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Manages bootstrap peer discovery from TU Delft nodes (FR-005, FR-006).
/// Contacts hardcoded bootstrap nodes and populates peer table.
/// </summary>
public class BootstrapManager : IBootstrapManager
{
    private readonly PrivacyAwareLogger _logger;
    private readonly ITransport _transport;
    private readonly List<BootstrapNode> _bootstrapNodes;
    private BootstrapStatus _status = BootstrapStatus.NotStarted;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public BootstrapStatus Status
    {
        get { lock (_lock) return _status; }
        private set { lock (_lock) _status = value; }
    }

    /// <inheritdoc/>
    public IPeerTable PeerTable { get; }

    /// <inheritdoc/>
    public IReadOnlyList<BootstrapNode> BootstrapNodes => _bootstrapNodes.AsReadOnly();

    /// <summary>
    /// Creates a new bootstrap manager.
    /// </summary>
    /// <param name="logger">Logger for privacy-aware logging.</param>
    /// <param name="transport">UDP transport for network communication.</param>
    /// <param name="peerTable">Peer table to populate (optional, creates new if null).</param>
    /// <param name="bootstrapNodes">Bootstrap nodes to use (optional, uses defaults if null).</param>
    public BootstrapManager(
        ILogger logger,
        ITransport transport,
        IPeerTable? peerTable = null,
        List<BootstrapNode>? bootstrapNodes = null)
    {
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        PeerTable = peerTable ?? new PeerTable();
        _bootstrapNodes = bootstrapNodes ?? BootstrapNode.GetDefaultNodes();

        // Validate bootstrap nodes
        foreach (var node in _bootstrapNodes)
        {
            if (!node.IsValid())
                throw new ArgumentException($"Invalid bootstrap node: {node.Address}:{node.Port}");
        }
    }

    /// <inheritdoc/>
    public async Task DiscoverPeersAsync(int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        if (timeoutSeconds < 1)
            throw new ArgumentException("Timeout must be at least 1 second", nameof(timeoutSeconds));

        Status = BootstrapStatus.Contacting;
        _logger.LogInformation("Starting bootstrap discovery with {Count} nodes, timeout {Timeout}s",
            _bootstrapNodes.Count, timeoutSeconds);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // TODO: Implement actual bootstrap discovery
            // This requires:
            // 1. Sending introduction-request messages to bootstrap nodes
            // 2. Parsing introduction-response messages
            // 3. Extracting peer information and adding to peer table
            // 4. Handling timeouts and retries
            //
            // For now, this is a placeholder that will be implemented in Phase 3
            // when we have the handshake protocol wired to the transport layer.

            await Task.Delay(100, cts.Token); // Placeholder

            Status = BootstrapStatus.Ready;
            PeerTable.MarkRefreshed();
            _logger.LogInformation("Bootstrap discovery complete, {Count} peers discovered", PeerTable.Count);
        }
        catch (OperationCanceledException)
        {
            Status = BootstrapStatus.Failed;
            _logger.LogWarning("Bootstrap discovery timed out after {Timeout}s", timeoutSeconds);
            throw;
        }
        catch (Exception ex)
        {
            Status = BootstrapStatus.Failed;
            _logger.LogError("Bootstrap discovery failed", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RefreshPeersAsync(CancellationToken cancellationToken = default)
    {
        if (!PeerTable.NeedsRefresh())
        {
            _logger.LogDebug("Peer table refresh not needed yet");
            return;
        }

        _logger.LogInformation("Refreshing peer table");
        Status = BootstrapStatus.Discovering;

        try
        {
            // TODO: Implement peer refresh
            // Similar to DiscoverPeersAsync but doesn't clear existing peers
            await Task.Delay(100, cancellationToken); // Placeholder

            PeerTable.MarkRefreshed();
            Status = BootstrapStatus.Ready;
            _logger.LogInformation("Peer table refreshed, {Count} peers", PeerTable.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError("Peer refresh failed", ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task StartPeriodicRefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting periodic peer refresh every {Interval}s", PeerTable.RefreshIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(PeerTable.RefreshIntervalSeconds), cancellationToken);
                await RefreshPeersAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Periodic refresh stopped");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Periodic refresh error", ex);
                // Continue running despite errors
            }
        }
    }
}

