using System.Collections.Concurrent;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// Tracks handshake state per peer with timeout handling (FR-012).
/// Thread-safe implementation using ConcurrentDictionary.
/// </summary>
public class HandshakeStateMachine
{
    private readonly ConcurrentDictionary<string, PeerHandshakeState> _peerStates = new();
    private readonly int _timeoutSeconds;

    /// <summary>
    /// Creates a new handshake state machine.
    /// </summary>
    /// <param name="timeoutSeconds">Handshake timeout in seconds (default: 10s per FR-012).</param>
    public HandshakeStateMachine(int timeoutSeconds = 10)
    {
        if (timeoutSeconds < 1)
            throw new ArgumentException("Timeout must be at least 1 second", nameof(timeoutSeconds));

        _timeoutSeconds = timeoutSeconds;
    }

    /// <summary>
    /// Gets the current state for a peer.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key.</param>
    /// <returns>Current handshake state.</returns>
    public HandshakeState GetState(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        if (_peerStates.TryGetValue(publicKeyHex.ToLowerInvariant(), out var state))
        {
            // Check for timeout
            if (state.State != HandshakeState.IntroResponseReceived &&
                state.State != HandshakeState.PunctureReceived &&
                DateTime.UtcNow - state.LastUpdate > TimeSpan.FromSeconds(_timeoutSeconds))
            {
                state.State = HandshakeState.TimedOut;
            }

            return state.State;
        }

        return HandshakeState.None;
    }

    /// <summary>
    /// Updates the state for a peer.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key.</param>
    /// <param name="newState">New handshake state.</param>
    public void UpdateState(string publicKeyHex, HandshakeState newState)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        var key = publicKeyHex.ToLowerInvariant();
        _peerStates.AddOrUpdate(
            key,
            _ => new PeerHandshakeState { State = newState, LastUpdate = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.State = newState;
                existing.LastUpdate = DateTime.UtcNow;
                return existing;
            });
    }

    /// <summary>
    /// Removes a peer from the state machine.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemovePeer(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        return _peerStates.TryRemove(publicKeyHex.ToLowerInvariant(), out _);
    }

    /// <summary>
    /// Gets all peers in a specific state.
    /// </summary>
    /// <param name="state">State to filter by.</param>
    /// <returns>List of public key hex strings.</returns>
    public List<string> GetPeersInState(HandshakeState state)
    {
        return _peerStates
            .Where(kvp => kvp.Value.State == state)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Clears all peer states.
    /// </summary>
    public void Clear()
    {
        _peerStates.Clear();
    }

    /// <summary>
    /// Gets the number of peers being tracked.
    /// </summary>
    public int Count => _peerStates.Count;

    /// <summary>
    /// Internal class to track per-peer handshake state.
    /// </summary>
    private class PeerHandshakeState
    {
        public HandshakeState State { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}

