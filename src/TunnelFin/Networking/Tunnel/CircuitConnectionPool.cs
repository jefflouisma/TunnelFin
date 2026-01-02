using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Connection pool for managing circuit reuse using ObjectPool pattern.
/// Maintains a pool of healthy circuits for BitTorrent peer connections.
/// </summary>
public class CircuitConnectionPool
{
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<Circuit> _availableCircuits;
    private readonly ConcurrentDictionary<uint, Circuit> _inUseCircuits;
    private readonly SemaphoreSlim _poolSemaphore;

    /// <summary>
    /// Creates a new circuit connection pool.
    /// </summary>
    /// <param name="circuitManager">Circuit manager for creating circuits.</param>
    /// <param name="settings">Anonymity settings.</param>
    /// <param name="logger">Logger instance.</param>
    public CircuitConnectionPool(
        CircuitManager circuitManager,
        AnonymitySettings settings,
        ILogger logger)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _availableCircuits = new ConcurrentQueue<Circuit>();
        _inUseCircuits = new ConcurrentDictionary<uint, Circuit>();
        _poolSemaphore = new SemaphoreSlim(_settings.MaxConcurrentCircuits, _settings.MaxConcurrentCircuits);
    }

    /// <summary>
    /// Gets a circuit from the pool or creates a new one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A healthy circuit.</returns>
    public async Task<Circuit> GetConnectionAsync(CancellationToken cancellationToken)
    {
        // Wait for available slot in pool
        await _poolSemaphore.WaitAsync(cancellationToken);

        try
        {
            // Try to get a healthy circuit from the pool
            while (_availableCircuits.TryDequeue(out var circuit))
            {
                if (IsCircuitHealthy(circuit))
                {
                    _inUseCircuits.TryAdd(circuit.IPv8CircuitId, circuit);
                    _logger.LogDebug("Reusing circuit {CircuitId} from pool", circuit.IPv8CircuitId);
                    return circuit;
                }
                else
                {
                    _logger.LogDebug("Discarding unhealthy circuit {CircuitId} from pool", circuit.IPv8CircuitId);
                }
            }

            // Try to get an existing healthy circuit from CircuitManager
            var existingCircuit = _circuitManager.Circuits.Values
                .FirstOrDefault(c => IsCircuitHealthy(c) && !_inUseCircuits.ContainsKey(c.IPv8CircuitId));

            if (existingCircuit != null)
            {
                _inUseCircuits.TryAdd(existingCircuit.IPv8CircuitId, existingCircuit);
                _logger.LogDebug("Using existing circuit {CircuitId}", existingCircuit.IPv8CircuitId);
                return existingCircuit;
            }

            // Create a new circuit
            _logger.LogDebug("Creating new circuit for pool");
            var newCircuit = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);

            // Wait for circuit to establish (with timeout)
            var timeout = TimeSpan.FromSeconds(_settings.CircuitEstablishmentTimeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (newCircuit.State != CircuitState.Established && 
                   DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (newCircuit.State != CircuitState.Established)
            {
                _poolSemaphore.Release();
                throw new InvalidOperationException(
                    $"Circuit {newCircuit.IPv8CircuitId} failed to establish within {timeout.TotalSeconds}s");
            }

            _inUseCircuits.TryAdd(newCircuit.IPv8CircuitId, newCircuit);
            _logger.LogInformation("Created new circuit {CircuitId} for pool", newCircuit.IPv8CircuitId);
            return newCircuit;
        }
        catch
        {
            _poolSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Returns a circuit to the pool after use.
    /// </summary>
    /// <param name="circuit">Circuit to return.</param>
    /// <param name="healthy">Whether the circuit is still healthy.</param>
    public void ReturnConnection(Circuit circuit, bool healthy)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        _inUseCircuits.TryRemove(circuit.IPv8CircuitId, out _);

        if (healthy && IsCircuitHealthy(circuit))
        {
            _availableCircuits.Enqueue(circuit);
            _logger.LogDebug("Returned healthy circuit {CircuitId} to pool", circuit.IPv8CircuitId);
        }
        else
        {
            _logger.LogDebug("Discarding unhealthy circuit {CircuitId}", circuit.IPv8CircuitId);
            // Circuit will be garbage collected or cleaned up by CircuitManager
        }

        _poolSemaphore.Release();
    }

    /// <summary>
    /// Checks if a circuit is healthy for use.
    /// </summary>
    private bool IsCircuitHealthy(Circuit circuit)
    {
        return circuit.State == CircuitState.Established && !circuit.IsExpired;
    }

    /// <summary>
    /// Gets the number of circuits currently in use.
    /// </summary>
    public int InUseCount => _inUseCircuits.Count;

    /// <summary>
    /// Gets the number of circuits available in the pool.
    /// </summary>
    public int AvailableCount => _availableCircuits.Count;
}

