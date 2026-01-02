using FluentAssertions;
using TunnelFin.Streaming;
using Xunit;

namespace TunnelFin.Tests.Streaming;

/// <summary>
/// Tests for StreamMetrics (T100).
/// Tests active streams counter per FR-043.
/// </summary>
public class StreamMetricsTests
{
    private readonly StreamMetrics _metrics;

    public StreamMetricsTests()
    {
        _metrics = new StreamMetrics();
    }

    [Fact]
    public void ActiveStreamsCount_Should_Start_At_Zero()
    {
        // Assert
        _metrics.ActiveStreamsCount.Should().Be(0);
    }

    [Fact]
    public void IncrementActiveStreams_Should_Increase_Count()
    {
        // Act
        _metrics.IncrementActiveStreams();

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(1);
    }

    [Fact]
    public void DecrementActiveStreams_Should_Decrease_Count()
    {
        // Arrange
        _metrics.IncrementActiveStreams();
        _metrics.IncrementActiveStreams();

        // Act
        _metrics.DecrementActiveStreams();

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(1);
    }

    [Fact]
    public void DecrementActiveStreams_Should_Not_Go_Below_Zero()
    {
        // Act
        _metrics.DecrementActiveStreams();
        _metrics.DecrementActiveStreams();

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(0);
    }

    [Fact]
    public void RecordStreamStart_Should_Track_Stream_ID()
    {
        // Arrange
        var streamId = Guid.NewGuid();

        // Act
        _metrics.RecordStreamStart(streamId);

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(1);
        _metrics.IsStreamActive(streamId).Should().BeTrue();
    }

    [Fact]
    public void RecordStreamEnd_Should_Remove_Stream_ID()
    {
        // Arrange
        var streamId = Guid.NewGuid();
        _metrics.RecordStreamStart(streamId);

        // Act
        _metrics.RecordStreamEnd(streamId);

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(0);
        _metrics.IsStreamActive(streamId).Should().BeFalse();
    }

    [Fact]
    public void GetTotalStreamsStarted_Should_Return_Cumulative_Count()
    {
        // Arrange
        _metrics.RecordStreamStart(Guid.NewGuid());
        _metrics.RecordStreamStart(Guid.NewGuid());
        _metrics.RecordStreamEnd(Guid.NewGuid());

        // Act
        var total = _metrics.GetTotalStreamsStarted();

        // Assert
        total.Should().Be(2, "should count all started streams");
    }

    [Fact]
    public void GetAverageStreamDuration_Should_Calculate_Average()
    {
        // Arrange
        var stream1 = Guid.NewGuid();
        var stream2 = Guid.NewGuid();
        
        _metrics.RecordStreamStart(stream1);
        Thread.Sleep(100);
        _metrics.RecordStreamEnd(stream1);
        
        _metrics.RecordStreamStart(stream2);
        Thread.Sleep(100);
        _metrics.RecordStreamEnd(stream2);

        // Act
        var avgDuration = _metrics.GetAverageStreamDuration();

        // Assert
        avgDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(50));
        avgDuration.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Reset_Should_Clear_All_Metrics()
    {
        // Arrange
        _metrics.RecordStreamStart(Guid.NewGuid());
        _metrics.RecordStreamStart(Guid.NewGuid());

        // Act
        _metrics.Reset();

        // Assert
        _metrics.ActiveStreamsCount.Should().Be(0);
        _metrics.GetTotalStreamsStarted().Should().Be(0);
    }

    [Fact]
    public void ActiveStreamsCount_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        for (int i = 0; i < 100; i++)
        {
            _metrics.RecordStreamStart(Guid.NewGuid());
        }

        // Act
        var start = DateTime.UtcNow;
        var count = _metrics.ActiveStreamsCount;
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        count.Should().Be(100);
    }
}

