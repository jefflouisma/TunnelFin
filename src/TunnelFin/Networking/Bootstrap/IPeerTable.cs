using TunnelFin.Networking.IPv8;

namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Interface for peer table management (FR-007).
/// Tracks discovered peers with reliability scoring and relay selection.
/// </summary>
public interface IPeerTable
{
    /// <summary>
    /// Gets all peers in the table.
    /// </summary>
    IReadOnlyDictionary<string, Peer> Peers { get; }

    /// <summary>
    /// Gets the number of peers in the table.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the minimum required peer count for circuit operations.
    /// </summary>
    int MinimumPeerCount { get; }

    /// <summary>
    /// Gets the maximum peer count.
    /// </summary>
    int MaxPeerCount { get; }

    /// <summary>
    /// Gets the last refresh timestamp.
    /// </summary>
    DateTime? LastRefresh { get; }

    /// <summary>
    /// Gets the refresh interval in seconds.
    /// </summary>
    int RefreshIntervalSeconds { get; }

    /// <summary>
    /// Adds a peer to the table.
    /// </summary>
    /// <param name="peer">Peer to add.</param>
    /// <returns>True if added, false if already exists or table is full.</returns>
    bool AddPeer(Peer peer);

    /// <summary>
    /// Removes a peer from the table.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool RemovePeer(string publicKeyHex);

    /// <summary>
    /// Gets a peer by public key.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key.</param>
    /// <returns>Peer if found, null otherwise.</returns>
    Peer? GetPeer(string publicKeyHex);

    /// <summary>
    /// Gets peers suitable for relay (high reliability, low RTT).
    /// </summary>
    /// <param name="count">Number of relay candidates to return.</param>
    /// <returns>List of relay-capable peers.</returns>
    List<Peer> GetRelayPeers(int count);

    /// <summary>
    /// Updates the last refresh timestamp.
    /// </summary>
    void MarkRefreshed();

    /// <summary>
    /// Checks if a refresh is needed based on the refresh interval.
    /// </summary>
    /// <returns>True if refresh is needed.</returns>
    bool NeedsRefresh();

    /// <summary>
    /// Clears all peers from the table.
    /// </summary>
    void Clear();
}

