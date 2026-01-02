using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MonoTorrent.Connections;
using ReusableTasks;
using TunnelFin.Configuration;
using TunnelFin.Core;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Custom socket connector that routes MonoTorrent connections through anonymity circuits.
/// Implements ISocketConnector for transparent integration with MonoTorrent.
/// </summary>
public class TunnelSocketConnector : ISocketConnector
{
    private readonly CircuitManager _circuitManager;
    private readonly ITunnelProxy _tunnelProxy;
    private readonly AnonymitySettings _settings;
    private readonly PrivacyAwareLogger _logger;
    private readonly ISocketConnector _fallbackConnector;
    private readonly CircuitConnectionPool? _connectionPool;
    private readonly CircuitHealthMonitor? _healthMonitor;
    private bool _circuitRoutingEnabled = true;

    /// <summary>
    /// Creates a new tunnel socket connector.
    /// </summary>
    /// <param name="circuitManager">Circuit manager for obtaining circuits.</param>
    /// <param name="tunnelProxy">Tunnel proxy for creating tunnel streams.</param>
    /// <param name="settings">Anonymity settings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="fallbackConnector">Fallback connector for direct connections (optional).</param>
    /// <param name="connectionPool">Circuit connection pool for reuse (optional).</param>
    /// <param name="healthMonitor">Circuit health monitor (optional).</param>
    public TunnelSocketConnector(
        CircuitManager circuitManager,
        ITunnelProxy tunnelProxy,
        AnonymitySettings settings,
        ILogger logger,
        ISocketConnector? fallbackConnector = null,
        CircuitConnectionPool? connectionPool = null,
        CircuitHealthMonitor? healthMonitor = null)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _tunnelProxy = tunnelProxy ?? throw new ArgumentNullException(nameof(tunnelProxy));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _fallbackConnector = fallbackConnector ?? new MonoTorrent.Connections.SocketConnector();
        _connectionPool = connectionPool;
        _healthMonitor = healthMonitor;
    }

    /// <summary>
    /// Connects to a peer through an anonymity circuit or falls back to direct connection.
    /// </summary>
    /// <param name="uri">The peer URI (ipv4://host:port or ipv6://host:port).</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A socket (TunnelSocket or regular Socket).</returns>
    public async ReusableTask<Socket> ConnectAsync(Uri uri, CancellationToken token)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        token.ThrowIfCancellationRequested();

        // If circuit routing is disabled, use fallback
        if (!_circuitRoutingEnabled)
        {
            _logger.LogDebug("Circuit routing disabled, using fallback connector");
            return await _fallbackConnector.ConnectAsync(uri, token);
        }

        // Parse endpoint from URI
        var endpoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);

        // Try up to 3 circuits with exponential backoff
        const int maxRetries = 3;
        var random = new Random();

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            Circuit? circuit = null;
            bool circuitHealthy = false;

            try
            {
                // Get a healthy circuit
                circuit = await GetHealthyCircuitAsync(token);

                if (circuit == null || circuit.State != CircuitState.Established)
                {
                    if (_settings.AllowNonAnonymousFallback)
                    {
                        _logger.LogWarning("No established circuit available, falling back to direct connection");
                        return await _fallbackConnector.ConnectAsync(uri, token);
                    }
                    else
                    {
                        throw new InvalidOperationException("No established circuit available and non-anonymous fallback is disabled");
                    }
                }

                _logger.LogDebug("Attempting connection through circuit {CircuitId} (attempt {Attempt}/{MaxRetries})",
                    circuit.IPv8CircuitId, attempt + 1, maxRetries);

                // Create tunnel stream through circuit
                var tunnelStream = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint, token);

                // Wrap in TunnelSocket
                var tunnelSocket = new TunnelSocket(tunnelStream, endpoint);

                _logger.LogInformation("Connected to peer {Endpoint} through circuit {CircuitId}",
                    endpoint, circuit.IPv8CircuitId);

                circuitHealthy = true;
                return tunnelSocket;
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                // Circuit failed, mark as unhealthy and retry with different circuit
                _logger.LogWarning("Circuit {CircuitId} failed (attempt {Attempt}/{MaxRetries}): {Error}",
                    circuit?.IPv8CircuitId ?? 0, attempt + 1, maxRetries, ex.Message);

                circuitHealthy = false;

                // Exponential backoff with jitter
                var baseDelay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                var jitter = TimeSpan.FromMilliseconds(random.Next(0, 100));
                await Task.Delay(baseDelay + jitter, token);
            }
            catch (Exception ex)
            {
                // Last attempt failed
                _logger.LogError("All circuit connection attempts failed", ex);

                circuitHealthy = false;

                if (_settings.AllowNonAnonymousFallback)
                {
                    _logger.LogWarning("Falling back to direct connection");
                    return await _fallbackConnector.ConnectAsync(uri, token);
                }
                else
                {
                    throw new InvalidOperationException("Failed to connect through circuit and non-anonymous fallback is disabled", ex);
                }
            }
            finally
            {
                // Return circuit to pool if using pool
                if (circuit != null && _connectionPool != null)
                {
                    ReturnCircuit(circuit, circuitHealthy);
                }
            }
        }

        // Should never reach here
        throw new InvalidOperationException("Failed to connect after all retries");
    }

    /// <summary>
    /// Gets a healthy circuit from the pool or creates a new one.
    /// Selects circuit based on RTT, throughput, and reliability.
    /// </summary>
    public async Task<Circuit?> GetHealthyCircuitAsync(CancellationToken token)
    {
        // If using connection pool, get from pool
        if (_connectionPool != null)
        {
            _logger.LogDebug("Getting circuit from connection pool (InUse: {InUse}, Available: {Available})",
                _connectionPool.InUseCount, _connectionPool.AvailableCount);
            return await _connectionPool.GetConnectionAsync(token);
        }

        // Otherwise, get or create circuit directly
        return await GetOrCreateCircuitAsync(token);
    }

    /// <summary>
    /// Returns a circuit to the pool and marks it as healthy or unhealthy.
    /// </summary>
    /// <param name="circuit">The circuit to return.</param>
    /// <param name="healthy">Whether the circuit is healthy.</param>
    public void ReturnCircuit(Circuit circuit, bool healthy)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        if (_connectionPool != null)
        {
            _logger.LogDebug("Returning circuit {CircuitId} to pool (healthy: {Healthy})",
                circuit.IPv8CircuitId, healthy);
            _connectionPool.ReturnConnection(circuit, healthy);
        }
    }

    /// <summary>
    /// Enables or disables circuit routing.
    /// When disabled, all connections use the fallback connector.
    /// </summary>
    /// <param name="enabled">Whether to enable circuit routing.</param>
    public void SetCircuitRoutingEnabled(bool enabled)
    {
        _circuitRoutingEnabled = enabled;
        _logger.LogInformation("Circuit routing {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Gets an existing circuit or creates a new one.
    /// </summary>
    private async Task<Circuit?> GetOrCreateCircuitAsync(CancellationToken token)
    {
        // Try to get an existing established circuit
        // Prefer circuits that are not expired and have been checked by health monitor
        var candidates = _circuitManager.Circuits.Values
            .Where(c => c.State == CircuitState.Established && !c.IsExpired)
            .ToList();

        if (candidates.Count > 0)
        {
            // If health monitor is available, filter by health
            if (_healthMonitor != null)
            {
                var healthyCircuits = new List<Circuit>();
                foreach (var circuit in candidates)
                {
                    if (await _healthMonitor.CheckCircuitHealthAsync(circuit, token))
                    {
                        healthyCircuits.Add(circuit);
                    }
                }

                if (healthyCircuits.Count > 0)
                {
                    // Note: Advanced circuit selection based on RTT, throughput, and reliability metrics
                    // can be added in future. Current implementation uses first healthy circuit.
                    return healthyCircuits[0];
                }
            }
            else
            {
                // No health monitor, just return first established circuit
                return candidates[0];
            }
        }

        // Create a new circuit
        try
        {
            _logger.LogDebug("Creating new circuit with {HopCount} hops", _settings.DefaultHopCount);
            var circuit = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);

            if (circuit.State == CircuitState.Established)
            {
                return circuit;
            }

            // Wait for circuit to establish (up to configured timeout)
            var timeout = TimeSpan.FromSeconds(_settings.CircuitEstablishmentTimeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (circuit.State == CircuitState.Creating && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(100, token);
            }

            if (circuit.State == CircuitState.Established)
            {
                _logger.LogInformation("Circuit {CircuitId} established successfully", circuit.IPv8CircuitId);
                return circuit;
            }
            else
            {
                _logger.LogWarning("Circuit {CircuitId} failed to establish (state: {State})",
                    circuit.IPv8CircuitId, circuit.State);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create circuit", ex);
            return null;
        }
    }
}

