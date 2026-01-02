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

    /// <summary>
    /// Creates a new tunnel socket connector.
    /// </summary>
    /// <param name="circuitManager">Circuit manager for obtaining circuits.</param>
    /// <param name="tunnelProxy">Tunnel proxy for creating tunnel streams.</param>
    /// <param name="settings">Anonymity settings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="fallbackConnector">Fallback connector for direct connections (optional).</param>
    public TunnelSocketConnector(
        CircuitManager circuitManager,
        ITunnelProxy tunnelProxy,
        AnonymitySettings settings,
        ILogger logger,
        ISocketConnector? fallbackConnector = null)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _tunnelProxy = tunnelProxy ?? throw new ArgumentNullException(nameof(tunnelProxy));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _fallbackConnector = fallbackConnector ?? new MonoTorrent.Connections.SocketConnector();
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

        // Parse endpoint from URI
        var endpoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);

        try
        {
            // Get or create a circuit
            var circuit = await GetOrCreateCircuitAsync(token);

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

            // Create tunnel stream through circuit
            var tunnelStream = await _tunnelProxy.CreateTunnelAsync(circuit, endpoint, token);

            // Wrap in TunnelSocket
            var tunnelSocket = new TunnelSocket(tunnelStream, endpoint);

            _logger.LogInformation("Connected to peer through circuit {CircuitId}", circuit.IPv8CircuitId);
            return tunnelSocket;
        }
        catch (Exception ex)
        {
            if (_settings.AllowNonAnonymousFallback)
            {
                _logger.LogError("Failed to connect through circuit, falling back to direct connection", ex);
                return await _fallbackConnector.ConnectAsync(uri, token);
            }
            else
            {
                _logger.LogError("Failed to connect through circuit and non-anonymous fallback is disabled", ex);
                throw;
            }
        }
    }

    /// <summary>
    /// Gets an existing circuit or creates a new one.
    /// </summary>
    private async Task<Circuit?> GetOrCreateCircuitAsync(CancellationToken token)
    {
        // Try to get an existing established circuit
        var existingCircuit = _circuitManager.Circuits.Values
            .FirstOrDefault(c => c.State == CircuitState.Established && !c.IsExpired);

        if (existingCircuit != null)
        {
            return existingCircuit;
        }

        // Create a new circuit
        try
        {
            var circuit = await _circuitManager.CreateCircuitAsync(_settings.DefaultHopCount);
            if (circuit.State == CircuitState.Established)
            {
                return circuit;
            }

            // Wait a bit for circuit to establish (up to 5 seconds)
            var timeout = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;

            while (circuit.State == CircuitState.Creating && DateTime.UtcNow - startTime < timeout)
            {
                await Task.Delay(100, token);
            }

            return circuit.State == CircuitState.Established ? circuit : null;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to create circuit", ex);
            return null;
        }
    }
}

