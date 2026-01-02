using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Core;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking;

/// <summary>
/// Service for checking Tribler network availability and notifying UI of status changes (T113-T118).
/// Monitors circuit availability and fires events when status changes.
/// </summary>
public class NetworkAvailabilityService : IHostedService
{
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;
    private readonly PrivacyAwareLogger _logger;
    private readonly CancellationTokenSource _cts;
    private Task? _monitoringTask;
    private bool _lastAvailabilityStatus;
    private bool _isRunning;

    /// <summary>
    /// Event fired when network availability status changes.
    /// </summary>
    public event EventHandler<bool>? StatusChanged;

    /// <summary>
    /// Gets the number of available (established, non-expired) circuits.
    /// </summary>
    public int AvailableCircuitCount { get; private set; }

    /// <summary>
    /// Gets whether the service is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Creates a new network availability service.
    /// </summary>
    /// <param name="circuitManager">Circuit manager to query for circuit availability.</param>
    /// <param name="settings">Anonymity settings.</param>
    /// <param name="logger">Logger instance.</param>
    public NetworkAvailabilityService(
        CircuitManager circuitManager,
        AnonymitySettings settings,
        ILogger logger)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
        _cts = new CancellationTokenSource();
        _lastAvailabilityStatus = false;
        AvailableCircuitCount = 0;
    }

    /// <summary>
    /// Starts the periodic network availability monitoring (T117).
    /// Checks every 30 seconds and updates UI within 5 seconds of change.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting network availability monitoring");
        _isRunning = true;
        _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the periodic network availability monitoring.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping network availability monitoring");
        _isRunning = false;
        _cts.Cancel();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
    }

    /// <summary>
    /// Checks network availability by querying CircuitManager for active circuits (T114).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if at least one established, non-expired circuit exists, false otherwise.</returns>
    public Task<bool> CheckNetworkAvailabilityAsync(CancellationToken cancellationToken)
    {
        // Query CircuitManager for established, non-expired circuits
        var availableCircuits = _circuitManager.Circuits.Values
            .Where(c => c.State == CircuitState.Established && !c.IsExpired)
            .ToList();

        var previousCount = AvailableCircuitCount;
        AvailableCircuitCount = availableCircuits.Count;
        var isAvailable = AvailableCircuitCount > 0;

        // Fire status change event if availability changed (T115)
        if (isAvailable != _lastAvailabilityStatus)
        {
            _logger.LogInformation("Network availability changed: {PreviousStatus} -> {CurrentStatus} ({CircuitCount} circuits)",
                _lastAvailabilityStatus ? "available" : "unavailable",
                isAvailable ? "available" : "unavailable",
                AvailableCircuitCount);

            _lastAvailabilityStatus = isAvailable;
            StatusChanged?.Invoke(this, isAvailable);
        }
        else if (previousCount != AvailableCircuitCount)
        {
            // Log circuit count changes even if availability status didn't change (T118)
            _logger.LogDebug("Circuit count changed: {PreviousCount} -> {CurrentCount}",
                previousCount, AvailableCircuitCount);
        }

        return Task.FromResult(isAvailable);
    }

    /// <summary>
    /// Periodic monitoring loop that checks availability every 30 seconds (T117).
    /// </summary>
    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromSeconds(30);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckNetworkAvailabilityAsync(cancellationToken);
                await Task.Delay(checkInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during network availability check", ex);
                await Task.Delay(checkInterval, cancellationToken);
            }
        }
    }
}

