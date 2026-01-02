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
    private bool _isListening = false;

    // Message type for introduction-response
    private const byte MSG_INTRODUCTION_RESPONSE = 245;

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

        // Subscribe to incoming messages
        _transport.DatagramReceived += OnDatagramReceived;
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
                    cancellationToken);

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

            // Enable message listening
            _isListening = true;

            // Wait for responses (introduction-response messages handled by OnDatagramReceived)
            await Task.Delay(timeoutSeconds * 1000, cancellationToken);

            // Disable message listening
            _isListening = false;

            Status = BootstrapStatus.Ready;
            PeerTable.MarkRefreshed();
            _logger.LogInformation("Bootstrap discovery complete, {Count} peers discovered", PeerTable.Count);
        }
        catch (OperationCanceledException)
        {
            // Disable message listening on cancellation
            _isListening = false;
            Status = BootstrapStatus.Failed;
            _logger.LogWarning("Bootstrap discovery cancelled");
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

    /// <summary>
    /// Handles incoming datagrams to process introduction-response messages.
    /// </summary>
    private void OnDatagramReceived(object? sender, DatagramReceivedEventArgs e)
    {
        if (!_isListening)
            return;

        try
        {
            var data = e.Data.ToArray(); // Convert ReadOnlyMemory to byte[]

            _logger.LogDebug("Received {Length} bytes from {Endpoint}", data.Length, e.RemoteEndPoint);

            // Check minimum message length
            // py-ipv8 format: prefix (22 bytes) + msg_type (1) + auth + global_time + payload + signature
            if (data.Length < 23)
            {
                _logger.LogDebug("Message too short: {Length} bytes", data.Length);
                return;
            }

            // Check for py-ipv8 message format
            // Format: 0x00 + 0x02 + community_id (20 bytes) + message_type
            byte messageType;
            int payloadOffset;

            if (data[0] == 0x00 && data[1] == 0x02)
            {
                // New py-ipv8 format: 0x00 + 0x02 + community_id (20 bytes) + msg_type at byte 22
                messageType = data[22];
                payloadOffset = 23;
                _logger.LogDebug("Received py-ipv8 format message, type: {Type}", messageType);
            }
            else if (data[0] == 0x02)
            {
                // Legacy format: 0x02 + community_id (20 bytes) + service_id + reserved + msg_type at byte 23
                messageType = data[23];
                payloadOffset = 24;
                _logger.LogDebug("Received legacy format message, type: {Type}", messageType);
            }
            else
            {
                _logger.LogDebug("Unknown message format, first bytes: 0x{B0:X2} 0x{B1:X2}", data[0], data[1]);
                return;
            }

            // Only process introduction-response messages
            if (messageType != MSG_INTRODUCTION_RESPONSE)
            {
                _logger.LogDebug("Ignoring message type {Type}, expected {Expected}", messageType, MSG_INTRODUCTION_RESPONSE);
                return;
            }

            _logger.LogInformation("Received introduction-response from {Endpoint}", e.RemoteEndPoint);

            // Parse introduction-response payload
            // py-ipv8 format after prefix: BinMemberAuth (varlenH) + GlobalTime (8 bytes) + IntroductionResponsePayload
            // IntroductionResponsePayload: dest(6) + source_lan(6) + source_wan(6) + lan_intro(6) + wan_intro(6) + identifier(2)

            if (data.Length < payloadOffset + 10) // Minimum: auth_len(2) + some data
            {
                _logger.LogDebug("Payload too short");
                return;
            }

            // Skip BinMemberAuthenticationPayload (varlenH = 2-byte length + data)
            int offset = payloadOffset;
            var authLen = (data[offset] << 8) | data[offset + 1];
            offset += 2 + authLen;

            // Skip GlobalTimeDistributionPayload (8 bytes)
            offset += 8;

            // Now we're at IntroductionResponsePayload
            // Format: dest(6) + source_lan(6) + source_wan(6) + lan_intro(6) + wan_intro(6) + identifier(2)
            if (data.Length < offset + 32)
            {
                _logger.LogDebug("IntroductionResponsePayload too short");
                return;
            }

            // Extract source WAN address (offset 12-17 in IntroductionResponsePayload)
            var ipBytes = new byte[4];
            Array.Copy(data, offset + 12, ipBytes, 0, 4);
            // IPv4 addresses are in network byte order (big-endian)
            var ipv4Address = (uint)((ipBytes[0] << 24) | (ipBytes[1] << 16) | (ipBytes[2] << 8) | ipBytes[3]);

            var port = (ushort)((data[offset + 16] << 8) | data[offset + 17]);

            // Extract public key from BinMemberAuthenticationPayload
            var publicKey = new byte[32];
            if (authLen >= 32)
            {
                Array.Copy(data, payloadOffset + 2, publicKey, 0, 32);
            }

            // Create peer and add to table
            var peer = new Peer(publicKey, ipv4Address, port);
            peer.IsHandshakeComplete = false; // Needs full handshake
            peer.IsRelayCandidate = true; // Bootstrap responses are potential relays

            PeerTable.AddPeer(peer);
            _logger.LogInformation("Added peer from introduction-response: {IP}:{Port}",
                new IPAddress(ipBytes), port);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error processing introduction-response", ex);
        }
    }
}

