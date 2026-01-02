using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using TunnelFin.Core;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// TCP-over-circuit proxy for routing BitTorrent traffic through anonymity circuits.
/// Manages tunnel stream creation, multiplexing, and lifecycle.
/// </summary>
public class TunnelProxy : ITunnelProxy
{
    private readonly ConcurrentDictionary<ushort, TunnelStream> _activeStreams;
    private readonly PrivacyAwareLogger _logger;
    private ushort _nextStreamId;
    private bool _disposed;
    private bool _isRunning;

    /// <summary>
    /// Creates a new tunnel proxy.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public TunnelProxy(ILogger logger)
    {
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _activeStreams = new ConcurrentDictionary<ushort, TunnelStream>();
        _nextStreamId = 1;
    }

    /// <summary>
    /// Starts the tunnel proxy service.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TunnelProxy));
        if (_isRunning)
            throw new InvalidOperationException("TunnelProxy is already running");

        _isRunning = true;
        _logger.LogInformation("TunnelProxy started");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the tunnel proxy service.
    /// </summary>
    public async Task StopAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TunnelProxy));
        if (!_isRunning)
            return;

        _isRunning = false;

        // Close all active streams
        var streams = _activeStreams.Values.ToList();
        foreach (var stream in streams)
        {
            try
            {
                await CloseTunnelAsync(stream.StreamId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to close tunnel stream {StreamId}", ex, stream.StreamId);
            }
        }

        _logger.LogInformation("TunnelProxy stopped");
    }

    /// <summary>
    /// Creates a tunnel stream through the specified circuit to the remote endpoint.
    /// </summary>
    public Task<TunnelStream> CreateTunnelAsync(Circuit circuit, IPEndPoint remoteEndpoint, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TunnelProxy));
        if (!_isRunning)
            throw new InvalidOperationException("TunnelProxy is not running");
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));
        if (remoteEndpoint == null)
            throw new ArgumentNullException(nameof(remoteEndpoint));
        if (circuit.State != CircuitState.Established)
            throw new InvalidOperationException($"Circuit must be established, current state: {circuit.State}");

        // Generate unique stream ID
        var streamId = GenerateStreamId();

        // Create tunnel stream
        var stream = new TunnelStream(circuit, remoteEndpoint, streamId);

        // Register stream
        if (!_activeStreams.TryAdd(streamId, stream))
        {
            stream.Dispose();
            throw new InvalidOperationException($"Stream ID {streamId} already exists");
        }

        _logger.LogInformation("Created tunnel stream {StreamId} through circuit {CircuitId}", streamId, circuit.IPv8CircuitId);
        return Task.FromResult(stream);
    }

    /// <summary>
    /// Closes a tunnel stream by its stream ID.
    /// </summary>
    public Task CloseTunnelAsync(ushort streamId, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TunnelProxy));

        if (_activeStreams.TryRemove(streamId, out var stream))
        {
            stream.Dispose();
            _logger.LogInformation("Closed tunnel stream {StreamId}", streamId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a unique stream ID.
    /// </summary>
    private ushort GenerateStreamId()
    {
        // Simple increment with wraparound
        // In production, this should check for collisions
        return _nextStreamId++;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Stop if running
        if (_isRunning)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        // Dispose all streams
        foreach (var stream in _activeStreams.Values)
        {
            stream.Dispose();
        }
        _activeStreams.Clear();

        _disposed = true;
    }
}

