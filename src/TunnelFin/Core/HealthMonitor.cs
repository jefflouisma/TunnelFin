using System.Text;

namespace TunnelFin.Core;

/// <summary>
/// HealthMonitor tracks plugin and component health status (T094).
/// Implements FR-042: Health status metrics (plugin running/stopped, component health).
/// </summary>
public class HealthMonitor
{
    private PluginStatus _pluginStatus = PluginStatus.Stopped;
    private readonly Dictionary<string, ComponentHealth> _componentHealth = new();
    private readonly object _lock = new();

    /// <summary>
    /// Sets the plugin status.
    /// </summary>
    public void SetPluginStatus(PluginStatus status)
    {
        lock (_lock)
        {
            _pluginStatus = status;
        }
    }

    /// <summary>
    /// Gets the current plugin status.
    /// </summary>
    public PluginStatus GetPluginStatus()
    {
        lock (_lock)
        {
            return _pluginStatus;
        }
    }

    /// <summary>
    /// Sets the health status of a component.
    /// </summary>
    public void SetComponentHealth(string component, ComponentHealth health)
    {
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("Component cannot be empty", nameof(component));

        lock (_lock)
        {
            _componentHealth[component] = health;
        }
    }

    /// <summary>
    /// Gets the health status of a component.
    /// </summary>
    public ComponentHealth GetComponentHealth(string component)
    {
        lock (_lock)
        {
            return _componentHealth.TryGetValue(component, out var health) 
                ? health 
                : ComponentHealth.Unknown;
        }
    }

    /// <summary>
    /// Gets all component health statuses.
    /// </summary>
    public Dictionary<string, ComponentHealth> GetAllComponentHealth()
    {
        lock (_lock)
        {
            return new Dictionary<string, ComponentHealth>(_componentHealth);
        }
    }

    /// <summary>
    /// Checks if all components are healthy.
    /// </summary>
    public bool IsHealthy()
    {
        lock (_lock)
        {
            return _componentHealth.Values.All(h => h == ComponentHealth.Healthy);
        }
    }

    /// <summary>
    /// Gets a summary of health status.
    /// </summary>
    public string GetHealthSummary()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Plugin Status: {_pluginStatus}");
            sb.AppendLine("Component Health:");
            
            foreach (var (component, health) in _componentHealth)
            {
                sb.AppendLine($"  {component}: {health}");
            }

            return sb.ToString();
        }
    }
}

/// <summary>
/// Plugin status enumeration (FR-042).
/// </summary>
public enum PluginStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}

/// <summary>
/// Component health enumeration (FR-042).
/// </summary>
public enum ComponentHealth
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

