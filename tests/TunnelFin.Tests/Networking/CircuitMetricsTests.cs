using FluentAssertions;
using TunnelFin.Networking;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Tests for CircuitMetrics (T100).
/// Tests circuit health metrics per FR-045.
/// </summary>
public class CircuitMetricsTests
{
    private readonly CircuitMetrics _metrics;

    public CircuitMetricsTests()
    {
        _metrics = new CircuitMetrics();
    }

    [Fact]
    public void ActiveCircuitsCount_Should_Start_At_Zero()
    {
        // Assert
        _metrics.ActiveCircuitsCount.Should().Be(0);
    }

    [Fact]
    public void RecordCircuitCreated_Should_Increase_Count()
    {
        // Act
        _metrics.RecordCircuitCreated(3);

        // Assert
        _metrics.ActiveCircuitsCount.Should().Be(1);
    }

    [Fact]
    public void RecordCircuitClosed_Should_Decrease_Count()
    {
        // Arrange
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitCreated(2);

        // Act
        _metrics.RecordCircuitClosed();

        // Assert
        _metrics.ActiveCircuitsCount.Should().Be(1);
    }

    [Fact]
    public void GetHopDistribution_Should_Track_Hop_Counts()
    {
        // Arrange
        _metrics.RecordCircuitCreated(1);
        _metrics.RecordCircuitCreated(2);
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitCreated(3);

        // Act
        var distribution = _metrics.GetHopDistribution();

        // Assert
        distribution.Should().ContainKey(1).WhoseValue.Should().Be(1);
        distribution.Should().ContainKey(2).WhoseValue.Should().Be(1);
        distribution.Should().ContainKey(3).WhoseValue.Should().Be(2);
    }

    [Fact]
    public void RecordCircuitFailure_Should_Increase_Failure_Count()
    {
        // Act
        _metrics.RecordCircuitFailure("Timeout");
        _metrics.RecordCircuitFailure("Peer unreachable");

        // Assert
        _metrics.TotalCircuitFailures.Should().Be(2);
    }

    [Fact]
    public void GetFailureReasons_Should_Track_Failure_Types()
    {
        // Arrange
        _metrics.RecordCircuitFailure("Timeout");
        _metrics.RecordCircuitFailure("Timeout");
        _metrics.RecordCircuitFailure("Peer unreachable");

        // Act
        var reasons = _metrics.GetFailureReasons();

        // Assert
        reasons.Should().ContainKey("Timeout").WhoseValue.Should().Be(2);
        reasons.Should().ContainKey("Peer unreachable").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void GetCircuitSuccessRate_Should_Calculate_Percentage()
    {
        // Arrange
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitFailure("Timeout");

        // Act
        var successRate = _metrics.GetCircuitSuccessRate();

        // Assert
        successRate.Should().BeApproximately(0.75, 0.01, "3 successes out of 4 attempts = 75%");
    }

    [Fact]
    public void GetAverageCircuitLifetime_Should_Calculate_Average()
    {
        // Arrange
        var circuit1 = Guid.NewGuid();
        var circuit2 = Guid.NewGuid();
        
        _metrics.RecordCircuitCreated(3, circuit1);
        Thread.Sleep(100);
        _metrics.RecordCircuitClosed(circuit1);
        
        _metrics.RecordCircuitCreated(3, circuit2);
        Thread.Sleep(100);
        _metrics.RecordCircuitClosed(circuit2);

        // Act
        var avgLifetime = _metrics.GetAverageCircuitLifetime();

        // Assert
        avgLifetime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        avgLifetime.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Reset_Should_Clear_All_Metrics()
    {
        // Arrange
        _metrics.RecordCircuitCreated(3);
        _metrics.RecordCircuitFailure("Timeout");

        // Act
        _metrics.Reset();

        // Assert
        _metrics.ActiveCircuitsCount.Should().Be(0);
        _metrics.TotalCircuitFailures.Should().Be(0);
    }

    [Fact]
    public void GetHopDistribution_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _metrics.RecordCircuitCreated(i % 3 + 1);
        }

        // Act
        var start = DateTime.UtcNow;
        var distribution = _metrics.GetHopDistribution();
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        distribution.Should().HaveCount(3);
    }
}

