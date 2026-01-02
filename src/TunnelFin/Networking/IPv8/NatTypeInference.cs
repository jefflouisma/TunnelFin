using System.Collections.Concurrent;

namespace TunnelFin.Networking.IPv8;

/// <summary>
/// Infers NAT type from puncture success/failure patterns (T030a).
/// >50% failure rate indicates likely symmetric NAT, triggering relay-only mode.
/// </summary>
public class NatTypeInference
{
    private readonly ConcurrentDictionary<string, PunctureStats> _peerStats = new();
    private readonly double _symmetricNatThreshold;

    /// <summary>
    /// Creates a new NAT type inference tracker.
    /// </summary>
    /// <param name="symmetricNatThreshold">Failure rate threshold for symmetric NAT detection (default: 0.5 = 50%).</param>
    public NatTypeInference(double symmetricNatThreshold = 0.5)
    {
        if (symmetricNatThreshold < 0 || symmetricNatThreshold > 1)
            throw new ArgumentException("Threshold must be between 0 and 1", nameof(symmetricNatThreshold));

        _symmetricNatThreshold = symmetricNatThreshold;
    }

    /// <summary>
    /// Records a successful puncture attempt.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    public void RecordSuccess(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        var key = publicKeyHex.ToLowerInvariant();
        _peerStats.AddOrUpdate(
            key,
            _ => new PunctureStats { SuccessCount = 1 },
            (_, stats) =>
            {
                stats.SuccessCount++;
                return stats;
            });
    }

    /// <summary>
    /// Records a failed puncture attempt.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    public void RecordFailure(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        var key = publicKeyHex.ToLowerInvariant();
        _peerStats.AddOrUpdate(
            key,
            _ => new PunctureStats { FailureCount = 1 },
            (_, stats) =>
            {
                stats.FailureCount++;
                return stats;
            });
    }

    /// <summary>
    /// Infers the NAT type for a peer based on puncture statistics.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    /// <returns>Inferred NAT type.</returns>
    public NatType InferNatType(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        var key = publicKeyHex.ToLowerInvariant();
        if (!_peerStats.TryGetValue(key, out var stats))
            return NatType.Unknown;

        var totalAttempts = stats.SuccessCount + stats.FailureCount;
        if (totalAttempts == 0)
            return NatType.Unknown;

        // Not enough data for reliable inference
        if (totalAttempts < 3)
            return NatType.Unknown;

        var failureRate = (double)stats.FailureCount / totalAttempts;

        // High failure rate indicates symmetric NAT
        if (failureRate >= _symmetricNatThreshold)
            return NatType.Symmetric;

        // Low failure rate with some successes indicates cone NAT
        if (stats.SuccessCount > 0)
        {
            // If we have mostly successes, assume port-restricted cone (most common)
            if (failureRate < 0.2)
                return NatType.PortRestrictedCone;

            // Moderate failure rate suggests restricted cone
            return NatType.RestrictedCone;
        }

        return NatType.Unknown;
    }

    /// <summary>
    /// Gets the failure rate for a peer.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    /// <returns>Failure rate (0.0 - 1.0), or null if no data.</returns>
    public double? GetFailureRate(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        var key = publicKeyHex.ToLowerInvariant();
        if (!_peerStats.TryGetValue(key, out var stats))
            return null;

        var totalAttempts = stats.SuccessCount + stats.FailureCount;
        if (totalAttempts == 0)
            return null;

        return (double)stats.FailureCount / totalAttempts;
    }

    /// <summary>
    /// Checks if a peer should use relay-only mode based on NAT type.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    /// <returns>True if relay-only mode should be used, false otherwise.</returns>
    public bool ShouldUseRelayOnly(string publicKeyHex)
    {
        var natType = InferNatType(publicKeyHex);
        return natType == NatType.Symmetric;
    }

    /// <summary>
    /// Clears statistics for a peer.
    /// </summary>
    /// <param name="publicKeyHex">Hex-encoded public key of the peer.</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool ClearPeerStats(string publicKeyHex)
    {
        if (string.IsNullOrWhiteSpace(publicKeyHex))
            throw new ArgumentException("Public key hex cannot be empty", nameof(publicKeyHex));

        return _peerStats.TryRemove(publicKeyHex.ToLowerInvariant(), out _);
    }

    /// <summary>
    /// Internal class to track puncture statistics per peer.
    /// </summary>
    private class PunctureStats
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
    }
}

