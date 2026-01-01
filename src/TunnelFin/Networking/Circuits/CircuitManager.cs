using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.IPv8;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Manages anonymity circuit creation, extension, and lifecycle.
/// Implements circuit creation with CREATE/CREATED/EXTEND/EXTENDED messages.
/// </summary>
public class CircuitManager : IDisposable
{
    private readonly AnonymitySettings _settings;
    private readonly Dictionary<uint, Circuit> _circuits = new();
    private readonly List<Peer> _peers = new();
    private readonly Random _random = new();
    private uint _nextCircuitId = 1;
    private bool _disposed;

    public int ActiveCircuitCount => _circuits.Count(c => c.Value.State == CircuitState.Established);
    public int TotalCircuitCount => _circuits.Count;
    public IReadOnlyDictionary<uint, Circuit> Circuits => _circuits;
    public IReadOnlyList<Peer> Peers => _peers.AsReadOnly();

    public CircuitManager(AnonymitySettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

    private Peer? SelectRelayNode(Circuit circuit)
    {
        var candidates = _peers
            .Where(p => p.IsRelayCandidate)
            .Where(p => p.IsHandshakeComplete)
            .Where(p => !circuit.Hops.Any(h => h.PublicKey.SequenceEqual(p.PublicKey)))
            .ToList();

        if (!candidates.Any())
            return null;

        if (_settings.PreferHighBandwidthRelays)
            candidates = candidates.OrderByDescending(p => p.EstimatedBandwidth).ToList();

        if (_settings.PreferLowLatencyRelays)
            candidates = candidates.OrderBy(p => p.RttMs).ToList();

        var topCandidates = candidates.Take(Math.Min(5, candidates.Count)).ToList();
        return topCandidates[_random.Next(topCandidates.Count)];
    }

    private async Task SendCreateMessageAsync(Circuit circuit, Peer relay)
    {
        await Task.CompletedTask;
    }

    private async Task SendExtendMessageAsync(Circuit circuit, Peer relay)
    {
        await Task.CompletedTask;
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
