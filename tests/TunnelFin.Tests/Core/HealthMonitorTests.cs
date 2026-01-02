using FluentAssertions;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Tests for HealthMonitor (T100).
/// Tests health status metrics per FR-042.
/// </summary>
public class HealthMonitorTests
{
    private readonly HealthMonitor _monitor;

    public HealthMonitorTests()
    {
        _monitor = new HealthMonitor();
    }

    [Fact]
    public void GetPluginStatus_Should_Return_Running_When_Started()
    {
        // Act
        _monitor.SetPluginStatus(PluginStatus.Running);

        // Assert
        _monitor.GetPluginStatus().Should().Be(PluginStatus.Running);
    }

    [Fact]
    public void GetPluginStatus_Should_Return_Stopped_When_Stopped()
    {
        // Act
        _monitor.SetPluginStatus(PluginStatus.Stopped);

        // Assert
        _monitor.GetPluginStatus().Should().Be(PluginStatus.Stopped);
    }

    [Fact]
    public void SetComponentHealth_Should_Update_Component_Status()
    {
        // Act
        _monitor.SetComponentHealth("CircuitManager", ComponentHealth.Healthy);

        // Assert
        var health = _monitor.GetComponentHealth("CircuitManager");
        health.Should().Be(ComponentHealth.Healthy);
    }

    [Fact]
    public void GetComponentHealth_Should_Return_Unknown_For_Unregistered_Component()
    {
        // Act
        var health = _monitor.GetComponentHealth("UnknownComponent");

        // Assert
        health.Should().Be(ComponentHealth.Unknown);
    }

    [Fact]
    public void GetAllComponentHealth_Should_Return_All_Components()
    {
        // Arrange
        _monitor.SetComponentHealth("CircuitManager", ComponentHealth.Healthy);
        _monitor.SetComponentHealth("StreamManager", ComponentHealth.Degraded);
        _monitor.SetComponentHealth("IndexerManager", ComponentHealth.Unhealthy);

        // Act
        var allHealth = _monitor.GetAllComponentHealth();

        // Assert
        allHealth.Should().HaveCount(3);
        allHealth["CircuitManager"].Should().Be(ComponentHealth.Healthy);
        allHealth["StreamManager"].Should().Be(ComponentHealth.Degraded);
        allHealth["IndexerManager"].Should().Be(ComponentHealth.Unhealthy);
    }

    [Fact]
    public void IsHealthy_Should_Return_True_When_All_Components_Healthy()
    {
        // Arrange
        _monitor.SetComponentHealth("CircuitManager", ComponentHealth.Healthy);
        _monitor.SetComponentHealth("StreamManager", ComponentHealth.Healthy);

        // Act
        var isHealthy = _monitor.IsHealthy();

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHealthy_Should_Return_False_When_Any_Component_Unhealthy()
    {
        // Arrange
        _monitor.SetComponentHealth("CircuitManager", ComponentHealth.Healthy);
        _monitor.SetComponentHealth("StreamManager", ComponentHealth.Unhealthy);

        // Act
        var isHealthy = _monitor.IsHealthy();

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public void GetHealthSummary_Should_Return_Summary_String()
    {
        // Arrange
        _monitor.SetPluginStatus(PluginStatus.Running);
        _monitor.SetComponentHealth("CircuitManager", ComponentHealth.Healthy);
        _monitor.SetComponentHealth("StreamManager", ComponentHealth.Degraded);

        // Act
        var summary = _monitor.GetHealthSummary();

        // Assert
        summary.Should().Contain("Running");
        summary.Should().Contain("CircuitManager");
        summary.Should().Contain("Healthy");
        summary.Should().Contain("StreamManager");
        summary.Should().Contain("Degraded");
    }

    [Fact]
    public void GetAllComponentHealth_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        for (int i = 0; i < 50; i++)
        {
            _monitor.SetComponentHealth($"Component{i}", ComponentHealth.Healthy);
        }

        // Act
        var start = DateTime.UtcNow;
        var health = _monitor.GetAllComponentHealth();
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        health.Should().HaveCount(50);
    }
}

