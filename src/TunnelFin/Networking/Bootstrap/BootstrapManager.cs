using Microsoft.Extensions.Logging;
using System.Net;
using TunnelFin.Core;
using TunnelFin.Networking.Identity;
using TunnelFin.Networking.IPv8;
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
    private readonly Handshake _handshake;
    private readonly NetworkIdentity _identity;

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
    /// <param name="identity">Network identity for signing messages (optional, creates new if null).</param>
    public BootstrapManager(
        ILogger logger,
        ITransport transport,
        IPeerTable? peerTable = null,
        List<BootstrapNode>? bootstrapNodes = null,
        NetworkIdentity? identity = null)
    {
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        PeerTable = peerTable ?? new PeerTable();
        _bootstrapNodes = bootstrapNodes ?? BootstrapNode.GetDefaultNodes();
        _identity = identity ?? new NetworkIdentity();
        _handshake = new Handshake(_identity, _transport, logger);

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

        if (!_transport.IsRunning)
            throw new InvalidOperationException("Transport must be started before discovering peers");

        Status = BootstrapStatus.Contacting;
        _logger.LogInformation("Starting bootstrap discovery with {Count} nodes, timeout {Timeout}s",
            _bootstrapNodes.Count, timeoutSeconds);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            // Get local endpoint for source addresses
            var localEndpoint = _transport.LocalEndPoint!;
            var sourceLan = (localEndpoint.Address.ToString(), localEndpoint.Port);
            var sourceWan = sourceLan; // For now, assume LAN and WAN are the same

            // Send introduction-request to all bootstrap nodes
            var tasks = new List<Task<bool>>();
            ushort identifier = (ushort)Random.Shared.Next(1, 65535);

            foreach (var node in _bootstrapNodes)
            {
                node.LastContactAttempt = DateTime.UtcNow;
                var endpoint = node.GetEndPoint();

                var task = _handshake.SendIntroductionRequestAsync(
                    endpoint,
                    sourceLan,
                    sourceWan,
                    identifier++,
                    cts.Token);

                tasks.Add(task);
            }

            // Wait for all sends to complete
            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation("Sent introduction-requests to {Success}/{Total} bootstrap nodes",
                successCount, _bootstrapNodes.Count);

            // Update bootstrap node status
            for (int i = 0; i < _bootstrapNodes.Count; i++)
            {
                if (results[i])
                {
                    _bootstrapNodes[i].IsReachable = true;
                    _bootstrapNodes[i].LastSuccessfulContact = DateTime.UtcNow;
                }
            }

            Status = BootstrapStatus.Discovering;

            // Wait a bit for responses (introduction-response messages would be handled by Protocol layer)
            // For now, we just mark as ready since we successfully contacted bootstrap nodes
            await Task.Delay(100, cts.Token);

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

        if (!_transport.IsRunning)
        {
            _logger.LogWarning("Cannot refresh peers: transport not running");
            return;
        }

        _logger.LogInformation("Refreshing peer table");
        Status = BootstrapStatus.Discovering;

        try
        {
            // Similar to DiscoverPeersAsync but doesn't clear existing peers
            var localEndpoint = _transport.LocalEndPoint!;
            var sourceLan = (localEndpoint.Address.ToString(), localEndpoint.Port);
            var sourceWan = sourceLan;

            // Send introduction-request to reachable bootstrap nodes
            var tasks = new List<Task<bool>>();
            ushort identifier = (ushort)Random.Shared.Next(1, 65535);

            foreach (var node in _bootstrapNodes.Where(n => n.IsReachable))
            {
                node.LastContactAttempt = DateTime.UtcNow;
                var endpoint = node.GetEndPoint();

                var task = _handshake.SendIntroductionRequestAsync(
                    endpoint,
                    sourceLan,
                    sourceWan,
                    identifier++,
                    cancellationToken);

                tasks.Add(task);
            }

            if (tasks.Count > 0)
            {
                var results = await Task.WhenAll(tasks);
                var successCount = results.Count(r => r);
                _logger.LogInformation("Refreshed with {Success}/{Total} bootstrap nodes",
                    successCount, tasks.Count);
            }

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

