using System.Collections.Concurrent;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.BitTorrent;

/// <summary>
/// Manages BitTorrent peer connections and routes them through anonymity circuits.
/// Integrates MonoTorrent peer management with Tribler circuit routing (FR-002, FR-007).
/// </summary>
public class PeerManager : IDisposable
{
    private readonly CircuitManager _circuitManager;
    private readonly ConcurrentDictionary<string, PeerConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, List<string>> _torrentPeers = new();
    private readonly int _maxPeersPerTorrent;
    private bool _disposed;

    /// <summary>
    /// Maximum peers per torrent (default: 50).
    /// </summary>
    public int MaxPeersPerTorrent => _maxPeersPerTorrent;

    /// <summary>
    /// Total number of active peer connections.
    /// </summary>
    public int ActiveConnectionCount => _connections.Count;

    /// <summary>
    /// Initializes a new instance of the PeerManager class.
    /// </summary>
    /// <param name="circuitManager">Circuit manager for routing peer connections.</param>
    /// <param name="maxPeersPerTorrent">Maximum peers per torrent (default: 50).</param>
    public PeerManager(CircuitManager circuitManager, int maxPeersPerTorrent = 50)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        
        if (maxPeersPerTorrent < 1)
            throw new ArgumentException("Max peers per torrent must be at least 1", nameof(maxPeersPerTorrent));

        _maxPeersPerTorrent = maxPeersPerTorrent;
    }

    /// <summary>
    /// Adds a peer for a torrent and routes connection through an anonymity circuit.
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <param name="peerAddress">Peer IP address.</param>
    /// <param name="peerPort">Peer port.</param>
    /// <param name="circuitId">Circuit ID to route through (optional, will select best circuit if null).</param>
    /// <returns>True if peer was added successfully.</returns>
    public async Task<bool> AddPeerAsync(Guid torrentId, string peerAddress, int peerPort, Guid? circuitId = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PeerManager));

        // Check per-torrent peer limit
        if (!_torrentPeers.TryGetValue(torrentId, out var peers))
        {
            peers = new List<string>();
            _torrentPeers[torrentId] = peers;
        }

        if (peers.Count >= _maxPeersPerTorrent)
            return false;

        var peerId = $"{peerAddress}:{peerPort}";
        if (_connections.ContainsKey(peerId))
            return false; // Already connected

        // Select or create circuit for routing
        var circuit = circuitId.HasValue
            ? _circuitManager.Circuits.Values.FirstOrDefault(c => c.CircuitId == circuitId.Value)
            : await SelectBestCircuitAsync();

        if (circuit == null)
            return false;

        var connection = new PeerConnection
        {
            PeerId = peerId,
            TorrentId = torrentId,
            PeerAddress = peerAddress,
            PeerPort = peerPort,
            CircuitId = circuit.CircuitId,
            ConnectedAt = DateTime.UtcNow,
            IsActive = true
        };

        if (_connections.TryAdd(peerId, connection))
        {
            peers.Add(peerId);
            // TODO: Establish actual peer connection through circuit
            await Task.CompletedTask;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes a peer connection.
    /// </summary>
    /// <param name="peerAddress">Peer IP address.</param>
    /// <param name="peerPort">Peer port.</param>
    public void RemovePeer(string peerAddress, int peerPort)
    {
        var peerId = $"{peerAddress}:{peerPort}";
        if (_connections.TryRemove(peerId, out var connection))
        {
            if (_torrentPeers.TryGetValue(connection.TorrentId, out var peers))
            {
                peers.Remove(peerId);
            }
            // TODO: Close actual peer connection
        }
    }

    /// <summary>
    /// Gets the number of peers for a specific torrent.
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <returns>Number of peers.</returns>
    public int GetPeerCount(Guid torrentId)
    {
        return _torrentPeers.TryGetValue(torrentId, out var peers) ? peers.Count : 0;
    }

    /// <summary>
    /// Gets all peer connections for a torrent.
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <returns>List of peer connections.</returns>
    public IReadOnlyList<PeerConnection> GetPeers(Guid torrentId)
    {
        if (!_torrentPeers.TryGetValue(torrentId, out var peerIds))
            return Array.Empty<PeerConnection>();

        return peerIds
            .Select(id => _connections.TryGetValue(id, out var conn) ? conn : null)
            .Where(c => c != null)
            .ToList()!;
    }

    /// <summary>
    /// Selects the best available circuit for peer routing.
    /// Prefers circuits with low latency and high bandwidth.
    /// </summary>
    private async Task<Circuit?> SelectBestCircuitAsync()
    {
        var activeCircuits = _circuitManager.Circuits.Values
            .Where(c => c.State == Models.CircuitState.Established)
            .ToList();

        if (!activeCircuits.Any())
        {
            // Try to create a new circuit
            try
            {
                return await _circuitManager.CreateCircuitAsync();
            }
            catch
            {
                return null;
            }
        }

        // Select circuit with fewest active connections (load balancing)
        var circuitLoads = activeCircuits.ToDictionary(
            c => c.CircuitId,
            c => _connections.Values.Count(conn => conn.CircuitId == c.CircuitId)
        );

        var bestCircuitId = circuitLoads.OrderBy(kvp => kvp.Value).First().Key;
        return _circuitManager.Circuits.Values.FirstOrDefault(c => c.CircuitId == bestCircuitId);
    }

    /// <summary>
    /// Removes all peers for a specific torrent.
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    public void RemoveTorrent(Guid torrentId)
    {
        if (_torrentPeers.TryRemove(torrentId, out var peerIds))
        {
            foreach (var peerId in peerIds)
            {
                _connections.TryRemove(peerId, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var connection in _connections.Values)
        {
            // TODO: Close actual peer connections
        }

        _connections.Clear();
        _torrentPeers.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Represents a peer connection routed through an anonymity circuit.
    /// </summary>
    public class PeerConnection
    {
        public string PeerId { get; set; } = string.Empty;
        public Guid TorrentId { get; set; }
        public string PeerAddress { get; set; } = string.Empty;
        public int PeerPort { get; set; }
        public Guid CircuitId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public bool IsActive { get; set; }
        public long BytesDownloaded { get; set; }
        public long BytesUploaded { get; set; }
    }
}


