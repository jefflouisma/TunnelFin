using FluentAssertions;
using TunnelFin.Indexers;
using Xunit;

namespace TunnelFin.Tests.Indexers;

/// <summary>
/// Tests for IndexerMetrics (T100).
/// Tests indexer response time tracking per FR-046.
/// </summary>
public class IndexerMetricsTests
{
    private readonly IndexerMetrics _metrics;

    public IndexerMetricsTests()
    {
        _metrics = new IndexerMetrics();
    }

    [Fact]
    public void RecordRequest_Should_Track_Response_Time()
    {
        // Arrange
        var indexerName = "1337x";
        var responseTime = TimeSpan.FromMilliseconds(250);

        // Act
        _metrics.RecordRequest(indexerName, responseTime, success: true);

        // Assert
        var avgTime = _metrics.GetAverageResponseTime(indexerName);
        avgTime.Should().Be(responseTime);
    }

    [Fact]
    public void GetAverageResponseTime_Should_Calculate_Average()
    {
        // Arrange
        var indexerName = "Nyaa";
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(200), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(300), success: true);

        // Act
        var avgTime = _metrics.GetAverageResponseTime(indexerName);

        // Assert
        avgTime.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void GetSuccessRate_Should_Calculate_Percentage()
    {
        // Arrange
        var indexerName = "RARBG";
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(200), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(300), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(400), success: false);

        // Act
        var successRate = _metrics.GetSuccessRate(indexerName);

        // Assert
        successRate.Should().BeApproximately(0.75, 0.01, "3 successes out of 4 requests = 75%");
    }

    [Fact]
    public void GetTotalRequests_Should_Return_Request_Count()
    {
        // Arrange
        var indexerName = "1337x";
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(200), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(300), success: false);

        // Act
        var total = _metrics.GetTotalRequests(indexerName);

        // Assert
        total.Should().Be(3);
    }

    [Fact]
    public void GetAllIndexerStats_Should_Return_All_Indexers()
    {
        // Arrange
        _metrics.RecordRequest("1337x", TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest("Nyaa", TimeSpan.FromMilliseconds(200), success: true);
        _metrics.RecordRequest("RARBG", TimeSpan.FromMilliseconds(300), success: false);

        // Act
        var stats = _metrics.GetAllIndexerStats();

        // Assert
        stats.Should().HaveCount(3);
        stats.Should().ContainKey("1337x");
        stats.Should().ContainKey("Nyaa");
        stats.Should().ContainKey("RARBG");
    }

    [Fact]
    public void GetPeakResponseTime_Should_Track_Maximum()
    {
        // Arrange
        var indexerName = "1337x";
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(500), success: true);
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(200), success: true);

        // Act
        var peak = _metrics.GetPeakResponseTime(indexerName);

        // Assert
        peak.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Reset_Should_Clear_Indexer_Metrics()
    {
        // Arrange
        var indexerName = "1337x";
        _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100), success: true);

        // Act
        _metrics.Reset(indexerName);

        // Assert
        _metrics.GetTotalRequests(indexerName).Should().Be(0);
    }

    [Fact]
    public void ResetAll_Should_Clear_All_Metrics()
    {
        // Arrange
        _metrics.RecordRequest("1337x", TimeSpan.FromMilliseconds(100), success: true);
        _metrics.RecordRequest("Nyaa", TimeSpan.FromMilliseconds(200), success: true);

        // Act
        _metrics.ResetAll();

        // Assert
        _metrics.GetAllIndexerStats().Should().BeEmpty();
    }

    [Fact]
    public void GetAverageResponseTime_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        var indexerName = "1337x";
        for (int i = 0; i < 1000; i++)
        {
            _metrics.RecordRequest(indexerName, TimeSpan.FromMilliseconds(100 + i % 100), success: true);
        }

        // Act
        var start = DateTime.UtcNow;
        var avgTime = _metrics.GetAverageResponseTime(indexerName);
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        avgTime.Should().BeGreaterThan(TimeSpan.Zero);
    }
}

