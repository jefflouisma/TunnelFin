using FluentAssertions;
using TunnelFin.Networking;
using Xunit;

namespace TunnelFin.Tests.Networking;

/// <summary>
/// Unit tests for BandwidthTracker (FR-005, SC-010).
/// Tests proportional relay bandwidth contribution tracking.
/// </summary>
public class BandwidthTrackerTests
{
    [Fact]
    public void Constructor_Should_Initialize_With_Zero_Bandwidth()
    {
        // Act
        var tracker = new BandwidthTracker();

        // Assert
        tracker.TotalDownloadedBytes.Should().Be(0);
        tracker.TotalUploadedBytes.Should().Be(0);
        tracker.TotalRelayedBytes.Should().Be(0);
    }

    [Fact]
    public void RecordDownload_Should_Increase_Downloaded_Bytes()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        tracker.RecordDownload(1024);
        tracker.RecordDownload(2048);

        // Assert
        tracker.TotalDownloadedBytes.Should().Be(3072);
    }

    [Fact]
    public void RecordUpload_Should_Increase_Uploaded_Bytes()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        tracker.RecordUpload(512);
        tracker.RecordUpload(1024);

        // Assert
        tracker.TotalUploadedBytes.Should().Be(1536);
    }

    [Fact]
    public void RecordRelay_Should_Increase_Relayed_Bytes()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        tracker.RecordRelay(2048);
        tracker.RecordRelay(4096);

        // Assert
        tracker.TotalRelayedBytes.Should().Be(6144);
    }

    [Fact]
    public void GetRelayRatio_Should_Return_Zero_When_No_Downloads()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var ratio = tracker.GetRelayRatio();

        // Assert
        ratio.Should().Be(0.0);
    }

    [Fact]
    public void GetRelayRatio_Should_Calculate_Correct_Ratio()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(500);

        // Act
        var ratio = tracker.GetRelayRatio();

        // Assert
        ratio.Should().BeApproximately(0.5, 0.01, "relayed 500 bytes out of 1000 downloaded");
    }

    [Fact]
    public void GetRelayRatio_Should_Return_One_When_Fully_Proportional()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(1000);

        // Act
        var ratio = tracker.GetRelayRatio();

        // Assert
        ratio.Should().BeApproximately(1.0, 0.01, "relayed same amount as downloaded");
    }

    [Fact]
    public void GetRequiredRelayBytes_Should_Return_Zero_When_No_Downloads()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var required = tracker.GetRequiredRelayBytes();

        // Assert
        required.Should().Be(0);
    }

    [Fact]
    public void GetRequiredRelayBytes_Should_Return_Remaining_Bytes()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(300);

        // Act
        var required = tracker.GetRequiredRelayBytes();

        // Assert
        required.Should().Be(700, "need to relay 700 more bytes to match 1000 downloaded");
    }

    [Fact]
    public void GetRequiredRelayBytes_Should_Return_Zero_When_Fully_Relayed()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(1000);

        // Act
        var required = tracker.GetRequiredRelayBytes();

        // Assert
        required.Should().Be(0, "already relayed proportional amount");
    }

    [Fact]
    public void IsProportional_Should_Return_True_When_Within_Threshold()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(960); // 96% ratio, within 5% threshold

        // Act
        var isProportional = tracker.IsProportional(threshold: 0.05);

        // Assert
        isProportional.Should().BeTrue("96% is within 5% of 100%");
    }

    [Fact]
    public void IsProportional_Should_Return_False_When_Outside_Threshold()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(900); // 90% ratio, outside 5% threshold

        // Act
        var isProportional = tracker.IsProportional(threshold: 0.05);

        // Assert
        isProportional.Should().BeFalse("90% is not within 5% of 100%");
    }

    [Fact]
    public void RecordDownload_Should_Throw_When_Bytes_Is_Negative()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var act = () => tracker.RecordDownload(-100);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bytes");
    }

    [Fact]
    public void RecordUpload_Should_Throw_When_Bytes_Is_Negative()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var act = () => tracker.RecordUpload(-100);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bytes");
    }

    [Fact]
    public void RecordRelay_Should_Throw_When_Bytes_Is_Negative()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var act = () => tracker.RecordRelay(-100);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("bytes");
    }

    [Fact]
    public void IsProportional_Should_Throw_When_Threshold_Is_Negative()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var act = () => tracker.IsProportional(-0.1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("threshold");
    }

    [Fact]
    public void IsProportional_Should_Throw_When_Threshold_Is_Greater_Than_One()
    {
        // Arrange
        var tracker = new BandwidthTracker();

        // Act
        var act = () => tracker.IsProportional(1.5);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("threshold");
    }

    [Fact]
    public void GetRequiredRelayBytes_Should_Return_Zero_When_Over_Contributing()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(1500); // Over-contributing

        // Act
        var required = tracker.GetRequiredRelayBytes();

        // Assert
        required.Should().Be(0, "already relayed more than downloaded");
    }

    [Fact]
    public void GetRelayRatio_Should_Return_Greater_Than_One_When_Over_Contributing()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(2000); // 200% ratio

        // Act
        var ratio = tracker.GetRelayRatio();

        // Assert
        ratio.Should().BeApproximately(2.0, 0.01, "relayed twice as much as downloaded");
    }

    [Fact]
    public void IsProportional_Should_Return_True_When_Exactly_Proportional()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(1000);

        // Act
        var isProportional = tracker.IsProportional(threshold: 0.05);

        // Assert
        isProportional.Should().BeTrue("ratio is exactly 1.0");
    }

    [Fact]
    public void IsProportional_Should_Return_True_When_Over_Contributing_Within_Threshold()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        tracker.RecordDownload(1000);
        tracker.RecordRelay(1040); // 104% ratio, within 5% threshold

        // Act
        var isProportional = tracker.IsProportional(threshold: 0.05);

        // Assert
        isProportional.Should().BeTrue("104% is within 5% of 100%");
    }

    [Fact]
    public void RecordDownload_Should_Be_Thread_Safe()
    {
        // Arrange
        var tracker = new BandwidthTracker();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => tracker.RecordDownload(10)));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        tracker.TotalDownloadedBytes.Should().Be(1000, "100 tasks * 10 bytes each");
    }
}

