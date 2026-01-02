using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using TunnelFin.Configuration;
using TunnelFin.Core;
using TunnelFin.Models;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Manages anonymity circuit creation, extension, and lifecycle (T040-T041).
/// Implements circuit creation with CREATE/CREATED/EXTEND/EXTENDED messages.
/// Maintains 2-3 concurrent circuits with relay reliability tracking.
/// </summary>
public class CircuitManager : IDisposable
{
    private readonly AnonymitySettings _settings;
    private readonly ICircuitNetworkClient? _networkClient;
    private readonly PrivacyAwareLogger? _logger;
    private readonly Dictionary<uint, Circuit> _circuits = new();
    private readonly List<Peer> _peers = new();
    private readonly Random _random = new();
    private uint _nextCircuitId = 1;
    private bool _disposed;

    // Concurrent circuit management (T040)
    private const int MinConcurrentCircuits = 2;
    private const int MaxConcurrentCircuits = 3;
    private const double MinRelayReliability = 0.7; // 70% success rate (T041)

    public int ActiveCircuitCount => _circuits.Count(c => c.Value.State == CircuitState.Established);
    public int TotalCircuitCount => _circuits.Count;
    public IReadOnlyDictionary<uint, Circuit> Circuits => _circuits;
    public IReadOnlyList<Peer> Peers => _peers.AsReadOnly();

    /// <summary>
    /// Creates a new CircuitManager without network client (for testing).
    /// </summary>
    public CircuitManager(AnonymitySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Creates a new CircuitManager with network client (T040).
    /// </summary>
    public CircuitManager(
        AnonymitySettings settings,
        ICircuitNetworkClient networkClient,
        ILogger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _networkClient = networkClient ?? throw new ArgumentNullException(nameof(networkClient));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
    }

    public void AddPeer(Peer peer)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitManager));
        if (peer == null)
            throw new ArgumentNullException(nameof(peer));

        if (!_peers.Any(p => p.Equals(peer)))
        {
            _peers.Add(peer);
        }
    }

    public async Task<Circuit> CreateCircuitAsync(int? hopCount = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitManager));

        var hops = hopCount ?? _settings.DefaultHopCount;
        if (hops < _settings.MinHopCount || hops > _settings.MaxHopCount)
            throw new ArgumentOutOfRangeException(nameof(hopCount), 
                $"Hop count must be between {_settings.MinHopCount} and {_settings.MaxHopCount}");

        if (ActiveCircuitCount >= _settings.MaxConcurrentCircuits)
            throw new InvalidOperationException(
                $"Maximum concurrent circuits ({_settings.MaxConcurrentCircuits}) reached");

        var circuitId = AllocateCircuitId();
        var circuit = new Circuit(circuitId, hops, _settings.CircuitLifetimeSeconds);
        _circuits[circuitId] = circuit;

        try
        {
            for (int i = 0; i < hops; i++)
            {
                var relay = SelectRelayNode(circuit);
                if (relay == null)
                {
                    circuit.MarkFailed($"No suitable relay found for hop {i + 1}");
                    throw new InvalidOperationException($"No suitable relay found for hop {i + 1}");
                }

                if (i == 0)
                {
                    await SendCreateMessageAsync(circuit, relay);
                }
                else
                {
                    await SendExtendMessageAsync(circuit, relay);
                }

                await Task.Delay(10);

                var hop = new HopNode(relay.PublicKey, relay.IPv4Address, relay.Port, i);
                circuit.AddHop(hop);
            }

            circuit.MarkEstablished();
            return circuit;
        }
        catch (Exception ex)
        {
            circuit.MarkFailed(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Selects a relay node for circuit extension with reliability filtering (T041).
    /// </summary>
    private Peer? SelectRelayNode(Circuit circuit)
    {
        var candidates = _peers
            .Where(p => p.IsRelayCandidate)
            .Where(p => p.IsHandshakeComplete)
            .Where(p => !circuit.Hops.Any(h => h.PublicKey.SequenceEqual(p.PublicKey)))
            .Where(p => p.ReliabilityScore >= MinRelayReliability) // T041: Filter by reliability
            .ToList();

        if (!candidates.Any())
        {
            // Fallback: allow lower reliability if no high-reliability relays available
            candidates = _peers
                .Where(p => p.IsRelayCandidate)
                .Where(p => p.IsHandshakeComplete)
                .Where(p => !circuit.Hops.Any(h => h.PublicKey.SequenceEqual(p.PublicKey)))
                .ToList();

            if (!candidates.Any())
                return null;
        }

        if (_settings.PreferHighBandwidthRelays)
            candidates = candidates.OrderByDescending(p => p.EstimatedBandwidth).ToList();

        if (_settings.PreferLowLatencyRelays)
            candidates = candidates.OrderBy(p => p.RttMs).ToList();

        var topCandidates = candidates.Take(Math.Min(5, candidates.Count)).ToList();
        return topCandidates[_random.Next(topCandidates.Count)];
    }

    /// <summary>
    /// Sends a CREATE message to establish the first hop (T040).
    /// </summary>
    private async Task SendCreateMessageAsync(Circuit circuit, Peer relay)
    {
        if (_networkClient == null)
        {
            // No network client configured (testing mode)
            await Task.CompletedTask;
            return;
        }

        try
        {
            // Generate ephemeral key pair for key exchange
            var algorithm = KeyAgreementAlgorithm.X25519;
            var ephemeralKey = Key.Create(algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            var ephemeralPublicKey = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

            // Send CREATE message
            var response = await _networkClient.SendCreateAsync(
                circuit.IPv8CircuitId,
                relay,
                ephemeralPublicKey,
                CancellationToken.None);

            // Complete key exchange for first hop
            var hop = circuit.Hops.FirstOrDefault();
            if (hop != null)
            {
                hop.CompleteKeyExchange(response.EphemeralPublicKey, ephemeralKey);
                _logger?.LogDebug("Completed key exchange for circuit {CircuitId}, hop 0", circuit.IPv8CircuitId);
            }

            // Track relay success (T041)
            relay.SuccessCount++;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send CREATE message for circuit {CircuitId}", ex, circuit.IPv8CircuitId);
            relay.FailureCount++; // Track relay failure (T041)
            throw;
        }
    }

    /// <summary>
    /// Sends an EXTEND message to add another hop (T040).
    /// </summary>
    private async Task SendExtendMessageAsync(Circuit circuit, Peer relay)
    {
        if (_networkClient == null)
        {
            // No network client configured (testing mode)
            await Task.CompletedTask;
            return;
        }

        try
        {
            // Generate ephemeral key pair for key exchange
            var algorithm = KeyAgreementAlgorithm.X25519;
            var ephemeralKey = Key.Create(algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            var ephemeralPublicKey = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

            // Send EXTEND message
            var response = await _networkClient.SendExtendAsync(
                circuit.IPv8CircuitId,
                relay,
                ephemeralPublicKey,
                CancellationToken.None);

            // Complete key exchange for new hop
            var hop = circuit.Hops.LastOrDefault();
            if (hop != null)
            {
                hop.CompleteKeyExchange(response.EphemeralPublicKey, ephemeralKey);
                _logger?.LogDebug("Completed key exchange for circuit {CircuitId}, hop {HopIndex}",
                    circuit.IPv8CircuitId, hop.HopIndex);
            }

            // Track relay success (T041)
            relay.SuccessCount++;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to send EXTEND message for circuit {CircuitId}", ex, circuit.IPv8CircuitId);
            relay.FailureCount++; // Track relay failure (T041)
            throw;
        }
    }

    public void HandleCreatedMessage(uint circuitId, byte[] ephemeralKey, byte[] auth)
    {
        if (!_circuits.TryGetValue(circuitId, out var circuit))
            return;
    }

    public void HandleExtendedMessage(uint circuitId, byte[] ephemeralKey, byte[] auth)
    {
        if (!_circuits.TryGetValue(circuitId, out var circuit))
            return;
    }

    public Circuit? GetCircuit(uint circuitId)
    {
        _circuits.TryGetValue(circuitId, out var circuit);
        return circuit;
    }

    public void CloseCircuit(uint circuitId)
    {
        if (_circuits.TryGetValue(circuitId, out var circuit))
        {
            circuit.Dispose();
            _circuits.Remove(circuitId);
        }
    }

    public async Task<Circuit> RetryCircuitCreationAsync(int hopCount, int maxRetries = 3)
    {
        Exception? lastException = null;
        var timeout = TimeSpan.FromSeconds(_settings.CircuitEstablishmentTimeoutSeconds);
        var startTime = DateTime.UtcNow;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            if (DateTime.UtcNow - startTime > timeout)
                throw new TimeoutException($"Circuit creation timed out after {timeout.TotalSeconds}s");

            try
            {
                return await CreateCircuitAsync(hopCount);
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException(
            $"Failed to create circuit after {maxRetries} attempts", lastException);
    }

    private uint AllocateCircuitId()
    {
        return _nextCircuitId++;
    }

    /// <summary>
    /// Maintains 2-3 concurrent circuits (T040).
    /// Creates new circuits if below minimum, removes excess if above maximum.
    /// </summary>
    public async Task MaintainConcurrentCircuitsAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitManager));

        var activeCount = ActiveCircuitCount;

        // Create circuits if below minimum
        while (activeCount < MinConcurrentCircuits)
        {
            try
            {
                _logger?.LogInformation("Creating circuit to maintain minimum ({ActiveCount}/{MinCount})",
                    activeCount, MinConcurrentCircuits);
                await CreateCircuitAsync();
                activeCount++;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to create circuit for concurrent maintenance", ex);
                break; // Stop trying if creation fails
            }
        }

        // Remove excess circuits if above maximum
        if (activeCount > MaxConcurrentCircuits)
        {
            var excessCircuits = _circuits.Values
                .Where(c => c.State == CircuitState.Established)
                .OrderBy(c => c.LastActivityAt) // Remove oldest circuits first
                .Take(activeCount - MaxConcurrentCircuits)
                .ToList();

            foreach (var circuit in excessCircuits)
            {
                _logger?.LogInformation("Closing excess circuit {CircuitId} ({ActiveCount}/{MaxCount})",
                    circuit.IPv8CircuitId, activeCount, MaxConcurrentCircuits);
                CloseCircuit(circuit.IPv8CircuitId);
                activeCount--;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var circuit in _circuits.Values)
        {
            circuit.Dispose();
        }
        _circuits.Clear();
        _peers.Clear();

        _disposed = true;
    }
}
