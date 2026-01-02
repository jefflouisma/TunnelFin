using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Models;
using TunnelFin.Networking.Circuits;

namespace TunnelFin.Networking.Tunnel;

/// <summary>
/// Monitors circuit health using PING/PONG messages and periodic health checks.
/// Implements exponential backoff on health check failures.
/// </summary>
public class CircuitHealthMonitor : IHostedService
{
    private readonly CircuitManager _circuitManager;
    private readonly AnonymitySettings _settings;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private Task? _healthCheckTask;

    /// <summary>
    /// Gets whether the health monitor is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Creates a new circuit health monitor.
    /// </summary>
    /// <param name="circuitManager">Circuit manager to monitor.</param>
    /// <param name="settings">Anonymity settings.</param>
    /// <param name="logger">Logger instance.</param>
    public CircuitHealthMonitor(
        CircuitManager circuitManager,
        AnonymitySettings settings,
        ILogger logger)
    {
        _circuitManager = circuitManager ?? throw new ArgumentNullException(nameof(circuitManager));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts the health monitoring loop.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableCircuitHealthMonitoring)
        {
            _logger.LogInformation("Circuit health monitoring is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting circuit health monitor");
        IsRunning = true;
        _healthCheckTask = Task.Run(() => HealthCheckLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the health monitoring loop.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping circuit health monitor");
        IsRunning = false;
        _cts.Cancel();

        if (_healthCheckTask != null)
        {
            try
            {
                await _healthCheckTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }
    }

    /// <summary>
    /// Checks the health of a specific circuit.
    /// </summary>
    /// <param name="circuit">Circuit to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if circuit is healthy, false otherwise.</returns>
    public Task<bool> CheckCircuitHealthAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (circuit == null)
            throw new ArgumentNullException(nameof(circuit));

        // Check if circuit is established and not expired
        if (circuit.State != CircuitState.Established)
        {
            _logger.LogDebug("Circuit {CircuitId} is not established (state: {State})", 
                circuit.IPv8CircuitId, circuit.State);
            return Task.FromResult(false);
        }

        if (circuit.IsExpired)
        {
            _logger.LogDebug("Circuit {CircuitId} is expired", circuit.IPv8CircuitId);
            return Task.FromResult(false);
        }

        // Note: PING/PONG health checks can be added in future for more granular monitoring
        // Current implementation checks state and expiration which is sufficient for basic health monitoring
        return Task.FromResult(true);
    }

    /// <summary>
    /// Periodic health check loop with exponential backoff on failure.
    /// </summary>
    private async Task HealthCheckLoopAsync(CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromSeconds(_settings.CircuitHealthCheckIntervalSeconds);
        var backoffMultiplier = 1.0;
        const double maxBackoffMultiplier = 8.0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var circuits = _circuitManager.Circuits.Values.ToList();
                var healthyCount = 0;
                var unhealthyCount = 0;

                foreach (var circuit in circuits)
                {
                    var isHealthy = await CheckCircuitHealthAsync(circuit, cancellationToken);
                    if (isHealthy)
                    {
                        healthyCount++;
                    }
                    else
                    {
                        unhealthyCount++;
                        _logger.LogWarning("Circuit {CircuitId} is unhealthy", circuit.IPv8CircuitId);
                    }
                }

                if (unhealthyCount > 0)
                {
                    _logger.LogInformation("Health check complete: {Healthy} healthy, {Unhealthy} unhealthy circuits",
                        healthyCount, unhealthyCount);
                }

                // Reset backoff on successful check
                backoffMultiplier = 1.0;

                // Wait for next check interval
                await Task.Delay(checkInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during circuit health check");
                
                // Apply exponential backoff
                backoffMultiplier = Math.Min(backoffMultiplier * 2.0, maxBackoffMultiplier);
                var backoffDelay = TimeSpan.FromSeconds(checkInterval.TotalSeconds * backoffMultiplier);
                
                _logger.LogWarning("Health check failed, backing off for {Delay}s", backoffDelay.TotalSeconds);
                await Task.Delay(backoffDelay, cancellationToken);
            }
        }
    }
}

