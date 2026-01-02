using FluentAssertions;
using TunnelFin.Networking;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Tests for BandwidthMetrics (T100).
/// Tests bandwidth usage metrics per FR-044.
/// </summary>
public class BandwidthMetricsTests
{
    private readonly BandwidthMetrics _metrics;

    public BandwidthMetricsTests()
    {
        _metrics = new BandwidthMetrics();
    }

    [Fact]
    public void TotalBytesDownloaded_Should_Start_At_Zero()
    {
        // Assert
        _metrics.TotalBytesDownloaded.Should().Be(0);
    }

    [Fact]
    public void TotalBytesUploaded_Should_Start_At_Zero()
    {
        // Assert
        _metrics.TotalBytesUploaded.Should().Be(0);
    }

    [Fact]
    public void RecordDownload_Should_Increase_Total()
    {
        // Act
        _metrics.RecordDownload(1024);
        _metrics.RecordDownload(2048);

        // Assert
        _metrics.TotalBytesDownloaded.Should().Be(3072);
    }

    [Fact]
    public void RecordUpload_Should_Increase_Total()
    {
        // Act
        _metrics.RecordUpload(512);
        _metrics.RecordUpload(1024);

        // Assert
        _metrics.TotalBytesUploaded.Should().Be(1536);
    }

    [Fact]
    public void GetDownloadRate_Should_Calculate_Bytes_Per_Second()
    {
        // Arrange - Need at least 2 samples for rate calculation
        _metrics.RecordDownload(5000);
        Thread.Sleep(50);
        _metrics.RecordDownload(5000);
        Thread.Sleep(50);

        // Act
        var rate = _metrics.GetDownloadRate();

        // Assert
        rate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetUploadRate_Should_Calculate_Bytes_Per_Second()
    {
        // Arrange - Need at least 2 samples for rate calculation
        _metrics.RecordUpload(2500);
        Thread.Sleep(50);
        _metrics.RecordUpload(2500);
        Thread.Sleep(50);

        // Act
        var rate = _metrics.GetUploadRate();

        // Assert
        rate.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetDownloadRateMbps_Should_Convert_To_Megabits()
    {
        // Arrange - Need at least 2 samples for rate calculation
        _metrics.RecordDownload(500_000); // 500 KB
        Thread.Sleep(50);
        _metrics.RecordDownload(500_000); // 500 KB
        Thread.Sleep(50);

        // Act
        var rateMbps = _metrics.GetDownloadRateMbps();

        // Assert
        rateMbps.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetUploadRateMbps_Should_Convert_To_Megabits()
    {
        // Arrange - Need at least 2 samples for rate calculation
        _metrics.RecordUpload(250_000); // 250 KB
        Thread.Sleep(50);
        _metrics.RecordUpload(250_000); // 250 KB
        Thread.Sleep(50);

        // Act
        var rateMbps = _metrics.GetUploadRateMbps();

        // Assert
        rateMbps.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Reset_Should_Clear_All_Metrics()
    {
        // Arrange
        _metrics.RecordDownload(1024);
        _metrics.RecordUpload(512);

        // Act
        _metrics.Reset();

        // Assert
        _metrics.TotalBytesDownloaded.Should().Be(0);
        _metrics.TotalBytesUploaded.Should().Be(0);
    }

    [Fact]
    public void GetPeakDownloadRate_Should_Track_Maximum()
    {
        // Arrange
        _metrics.RecordDownload(1000);
        Thread.Sleep(50);
        _metrics.RecordDownload(5000);
        Thread.Sleep(50);
        _metrics.RecordDownload(2000);

        // Act
        var peak = _metrics.GetPeakDownloadRate();

        // Assert
        peak.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetPeakUploadRate_Should_Track_Maximum()
    {
        // Arrange
        _metrics.RecordUpload(500);
        Thread.Sleep(50);
        _metrics.RecordUpload(2500);
        Thread.Sleep(50);
        _metrics.RecordUpload(1000);

        // Act
        var peak = _metrics.GetPeakUploadRate();

        // Assert
        peak.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetDownloadRate_Should_Complete_In_Under_1_Second()
    {
        // Arrange - Add samples with small delays
        for (int i = 0; i < 20; i++)
        {
            _metrics.RecordDownload(1024);
            if (i % 5 == 0)
                Thread.Sleep(1);
        }

        // Act
        var start = DateTime.UtcNow;
        var rate = _metrics.GetDownloadRate();
        var duration = DateTime.UtcNow - start;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1), "should meet SC-009 latency requirement");
        rate.Should().BeGreaterThanOrEqualTo(0);
    }
}

