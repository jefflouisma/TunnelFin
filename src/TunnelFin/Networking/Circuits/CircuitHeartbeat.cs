using System.Timers;
using Microsoft.Extensions.Logging;
using TunnelFin.Core;
using TunnelFin.Models;

namespace TunnelFin.Networking.Circuits;

/// <summary>
/// Manages circuit keepalive heartbeats (T038, FR-017).
/// Sends periodic PING messages to detect circuit failures.
/// </summary>
public class CircuitHeartbeat : IDisposable
{
    private readonly CircuitManager _circuitManager;
    private readonly PrivacyAwareLogger _logger;
    private readonly System.Timers.Timer _heartbeatTimer;
    private readonly int _intervalSeconds;
    private readonly int _timeoutSeconds;
    private bool _disposed;

    /// <summary>
    /// Event raised when a circuit times out.
    /// </summary>
    public event EventHandler<CircuitTimeoutEventArgs>? CircuitTimedOut;

    /// <summary>
    /// Creates a new CircuitHeartbeat.
    /// </summary>
    /// <param name="circuitManager">Circuit manager to monitor.</param>
    /// <param name="logger">Logger for privacy-aware logging.</param>
    /// <param name="intervalSeconds">Heartbeat interval in seconds (default: 30s per FR-017).</param>
    /// <param name="timeoutSeconds">Circuit timeout in seconds (default: 90s = 3 missed heartbeats).</param>
    public CircuitHeartbeat(
        CircuitManager circuitManager,
        ILogger logger,
        int intervalSeconds = 30,
        int timeoutSeconds = 90)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _intervalSeconds = intervalSeconds;
        _timeoutSeconds = timeoutSeconds;

        _heartbeatTimer = new System.Timers.Timer(intervalSeconds * 1000);
        _heartbeatTimer.Elapsed += OnHeartbeatTick;
        _heartbeatTimer.AutoReset = true;
    }

    /// <summary>
    /// Starts the heartbeat timer.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitHeartbeat));

        _heartbeatTimer.Start();
        _logger.LogInformation("Circuit heartbeat started (interval={IntervalSeconds}s, timeout={TimeoutSeconds}s)", 
            _intervalSeconds, _timeoutSeconds);
    }

    /// <summary>
    /// Stops the heartbeat timer.
    /// </summary>
    public void Stop()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CircuitHeartbeat));

        _heartbeatTimer.Stop();
        _logger.LogInformation("Circuit heartbeat stopped");
    }

    /// <summary>
    /// Handles heartbeat timer ticks.
    /// </summary>
    private void OnHeartbeatTick(object? sender, ElapsedEventArgs e)
    {
        try
        {
            CheckCircuitHealth();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during heartbeat check", ex);
        }
    }

    /// <summary>
    /// Checks the health of all circuits and detects timeouts.
    /// </summary>
    private void CheckCircuitHealth()
    {
        var now = DateTime.UtcNow;
        var timedOutCircuits = new List<Circuit>();

        foreach (var circuit in _circuitManager.Circuits.Values)
        {
            // Skip circuits that are not established
            if (circuit.State != CircuitState.Established)
                continue;

            // Check if circuit has timed out
            var timeSinceLastActivity = now - circuit.LastActivityAt;
            if (timeSinceLastActivity.TotalSeconds > _timeoutSeconds)
            {
                _logger.LogWarning("Circuit {CircuitId} timed out (last activity: {LastActivity})", 
                    circuit.IPv8CircuitId, circuit.LastActivityAt);

                circuit.MarkFailed($"Circuit timed out after {timeSinceLastActivity.TotalSeconds:F1}s");
                timedOutCircuits.Add(circuit);
            }
        }

        // Raise timeout events
        foreach (var circuit in timedOutCircuits)
        {
            CircuitTimedOut?.Invoke(this, new CircuitTimeoutEventArgs(circuit));
        }
    }

    /// <summary>
    /// Disposes the CircuitHeartbeat.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _heartbeatTimer.Stop();
        _heartbeatTimer.Dispose();

        _disposed = true;
    }
}

/// <summary>
/// Event args for circuit timeout events.
/// </summary>
public class CircuitTimeoutEventArgs : EventArgs
{
    /// <summary>
    /// The circuit that timed out.
    /// </summary>
    public Circuit Circuit { get; }

    /// <summary>
    /// Creates a new CircuitTimeoutEventArgs.
    /// </summary>
    public CircuitTimeoutEventArgs(Circuit circuit)
    {
        Circuit = circuit ?? throw new ArgumentNullException(nameof(circuit));
    }
}

