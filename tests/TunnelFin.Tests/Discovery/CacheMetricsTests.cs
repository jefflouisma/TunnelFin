using FluentAssertions;
using TunnelFin.Discovery;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for CacheMetrics (T100).
/// Tests cache hit rate tracking per FR-047.
/// </summary>
public class CacheMetricsTests
{
    private readonly CacheMetrics _metrics;

    public CacheMetricsTests()
    {
        _metrics = new CacheMetrics();
    }

    [Fact]
    public void RecordMetadataCacheHit_Should_Increase_Hit_Count()
    {
        // Act
        _metrics.RecordMetadataCacheHit();

        // Assert
        _metrics.MetadataCacheHits.Should().Be(1);
    }

    [Fact]
    public void RecordMetadataCacheMiss_Should_Increase_Miss_Count()
    {
        // Act
        _metrics.RecordMetadataCacheMiss();

        // Assert
        _metrics.MetadataCacheMisses.Should().Be(1);
    }

    [Fact]
    public void GetMetadataCacheHitRate_Should_Calculate_Percentage()
    {
        // Arrange
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheMiss();

        // Act
        var hitRate = _metrics.GetMetadataCacheHitRate();

        // Assert
        hitRate.Should().BeApproximately(0.75, 0.01, "3 hits out of 4 requests = 75%");
    }

    [Fact]
    public void RecordTorrentCacheHit_Should_Increase_Hit_Count()
    {
        // Act
        _metrics.RecordTorrentCacheHit();

        // Assert
        _metrics.TorrentCacheHits.Should().Be(1);
    }

    [Fact]
    public void RecordTorrentCacheMiss_Should_Increase_Miss_Count()
    {
        // Act
        _metrics.RecordTorrentCacheMiss();

        // Assert
        _metrics.TorrentCacheMisses.Should().Be(1);
    }

    [Fact]
    public void GetTorrentCacheHitRate_Should_Calculate_Percentage()
    {
        // Arrange
        _metrics.RecordTorrentCacheHit();
        _metrics.RecordTorrentCacheHit();
        _metrics.RecordTorrentCacheMiss();
        _metrics.RecordTorrentCacheMiss();

        // Act
        var hitRate = _metrics.GetTorrentCacheHitRate();

        // Assert
        hitRate.Should().BeApproximately(0.50, 0.01, "2 hits out of 4 requests = 50%");
    }

    [Fact]
    public void GetOverallCacheHitRate_Should_Calculate_Combined_Percentage()
    {
        // Arrange
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheMiss();
        _metrics.RecordTorrentCacheHit();
        _metrics.RecordTorrentCacheMiss();

        // Act
        var hitRate = _metrics.GetOverallCacheHitRate();

        // Assert
        hitRate.Should().BeApproximately(0.60, 0.01, "3 hits out of 5 requests = 60%");
    }

    [Fact]
    public void GetCacheSummary_Should_Return_Summary_String()
    {
        // Arrange
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordMetadataCacheMiss();
        _metrics.RecordTorrentCacheHit();

        // Act
        var summary = _metrics.GetCacheSummary();

        // Assert
        summary.Should().Contain("Metadata");
        summary.Should().Contain("Torrent");
        summary.Should().Contain("50"); // 50% hit rate for metadata
    }

    [Fact]
    public void Reset_Should_Clear_All_Metrics()
    {
        // Arrange
        _metrics.RecordMetadataCacheHit();
        _metrics.RecordTorrentCacheHit();

        // Act
        _metrics.Reset();

        // Assert
        _metrics.MetadataCacheHits.Should().Be(0);
        _metrics.MetadataCacheMisses.Should().Be(0);
        _metrics.TorrentCacheHits.Should().Be(0);
        _metrics.TorrentCacheMisses.Should().Be(0);
    }

    [Fact]
    public void GetMetadataCacheHitRate_Should_Return_Zero_When_No_Requests()
    {
        // Act
        var hitRate = _metrics.GetMetadataCacheHitRate();

        // Assert
        hitRate.Should().Be(0);
    }

    [Fact]
    public void GetMetadataCacheHitRate_Should_Complete_In_Under_1_Second()
    {
        // Arrange
        for (int i = 0; i < 1000; i++)
        {
            if (i % 2 == 0)
                _metrics.RecordMetadataCacheHit();
            else
                _metrics.RecordMetadataCacheMiss();
        }

        // Act
        var start = DateTime.UtcNow;
        var hitRate = _metrics.GetMetadataCacheHitRate();
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        hitRate.Should().BeApproximately(0.50, 0.01);
    }
}

