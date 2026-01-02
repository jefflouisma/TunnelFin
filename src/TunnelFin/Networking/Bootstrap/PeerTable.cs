using System.Collections.Concurrent;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Manages discovered peers with reliability tracking (FR-007).
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public class PeerTable : IPeerTable
{
    private readonly ConcurrentDictionary<string, Peer> _peers = new();
    private readonly object _lock = new();
    private DateTime? _lastRefresh;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, Peer> Peers => _peers;

    /// <inheritdoc/>
    public int Count => _peers.Count;

    /// <inheritdoc/>
    public int MinimumPeerCount { get; }

    /// <inheritdoc/>
    public int MaxPeerCount { get; }

    /// <inheritdoc/>
    public DateTime? LastRefresh => _lastRefresh;

    /// <inheritdoc/>
    public int RefreshIntervalSeconds { get; }

    /// <summary>
    /// Creates a new peer table.
    /// </summary>
    /// <param name="minimumPeerCount">Minimum peers required for circuits (default: 20).</param>
    /// <param name="maxPeerCount">Maximum peers to track (default: 200).</param>
    /// <param name="refreshIntervalSeconds">Refresh interval in seconds (default: 300 = 5 min).</param>
    public PeerTable(int minimumPeerCount = 20, int maxPeerCount = 200, int refreshIntervalSeconds = 300)
    {
        if (minimumPeerCount < 1)
            throw new ArgumentException("Minimum peer count must be at least 1", nameof(minimumPeerCount));

        if (maxPeerCount < minimumPeerCount)
            throw new ArgumentException("Max peer count must be >= minimum peer count", nameof(maxPeerCount));

        if (refreshIntervalSeconds < 1)
            throw new ArgumentException("Refresh interval must be at least 1 second", nameof(refreshIntervalSeconds));

        MinimumPeerCount = minimumPeerCount;
        MaxPeerCount = maxPeerCount;
        RefreshIntervalSeconds = refreshIntervalSeconds;
    }

    /// <inheritdoc/>
    public bool AddPeer(Peer peer)
    {
        if (peer == null)
            throw new ArgumentNullException(nameof(peer));

        var publicKeyHex = Convert.ToHexString(peer.PublicKey).ToLowerInvariant();

        // Check if table is full
        if (_peers.Count >= MaxPeerCount && !_peers.ContainsKey(publicKeyHex))
            return false;

        return _peers.TryAdd(publicKeyHex, peer);
    }

    /// <inheritdoc/>
    public bool RemovePeer(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        return _peers.TryRemove(publicKeyHex.ToLowerInvariant(), out _);
    }

    /// <inheritdoc/>
    public Peer? GetPeer(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        _peers.TryGetValue(publicKeyHex.ToLowerInvariant(), out var peer);
        return peer;
    }

    /// <inheritdoc/>
    public List<Peer> GetRelayPeers(int count)
    {
        if (count < 1)
            throw new ArgumentException("Count must be at least 1", nameof(count));

        // Select peers with:
        // 1. IsRelayCandidate = true
        // 2. High reliability score (>= 0.7)
        // 3. Low RTT (< 500ms)
        // Order by reliability score descending, then RTT ascending
        return _peers.Values
            .Where(p => p.IsRelayCandidate && p.ReliabilityScore >= 0.7 && p.RttMs < 500)
            .OrderByDescending(p => p.ReliabilityScore)
            .ThenBy(p => p.RttMs)
            .Take(count)
            .ToList();
    }

    /// <inheritdoc/>
    public void MarkRefreshed()
    {
        lock (_lock)
        {
            _lastRefresh = DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public bool NeedsRefresh()
    {
        lock (_lock)
        {
            if (_lastRefresh == null)
                return true;

            var elapsed = DateTime.UtcNow - _lastRefresh.Value;
            return elapsed.TotalSeconds >= RefreshIntervalSeconds;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _peers.Clear();
        lock (_lock)
        {
            _lastRefresh = null;
        }
    }
}

