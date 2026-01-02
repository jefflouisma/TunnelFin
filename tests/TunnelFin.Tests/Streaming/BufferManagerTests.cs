using FluentAssertions;
using TunnelFin.Streaming;
using Xunit;

namespace TunnelFin.Tests.Streaming;

/// <summary>
/// Unit tests for BufferManager (FR-010, SC-003).
/// Tests buffer status tracking and >10s buffer requirement.
/// </summary>
public class BufferManagerTests
{
    [Fact]
    public void GetBufferStatus_Should_Return_Initial_State()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.Should().NotBeNull("buffer status should be available");
        status.StreamId.Should().Be(streamId);
        status.BufferedSeconds.Should().Be(0, "no data buffered initially");
        status.IsBuffering.Should().BeTrue("should be buffering initially");
        status.IsReadyForPlayback.Should().BeFalse("not ready until minimum buffer reached");
    }

    [Fact]
    public void UpdateBuffer_Should_Track_Buffered_Data()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act
        manager.UpdateBuffer(streamId, bufferedBytes: 1_000_000, downloadSpeedBytesPerSecond: 100_000);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.BufferedSeconds.Should().BeGreaterThan(0, "should calculate buffered seconds from bytes and speed");
    }

    [Fact]
    public void IsReadyForPlayback_Should_Require_Minimum_Buffer()
    {
        // Arrange - FR-010, SC-003
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act - Simulate buffering 5 seconds (not enough)
        manager.UpdateBuffer(streamId, bufferedBytes: 500_000, downloadSpeedBytesPerSecond: 100_000);
        var status1 = manager.GetBufferStatus(streamId);

        // Simulate buffering 15 seconds (enough)
        manager.UpdateBuffer(streamId, bufferedBytes: 1_500_000, downloadSpeedBytesPerSecond: 100_000);
        var status2 = manager.GetBufferStatus(streamId);

        // Assert
        status1.IsReadyForPlayback.Should().BeFalse("5 seconds is below minimum");
        status2.IsReadyForPlayback.Should().BeTrue("15 seconds exceeds minimum");
    }

    [Fact]
    public void UpdateBuffer_Should_Calculate_Seconds_From_Bitrate()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act - 1MB buffered at 100KB/s = 10 seconds
        manager.UpdateBuffer(streamId, bufferedBytes: 1_000_000, downloadSpeedBytesPerSecond: 100_000);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.BufferedSeconds.Should().BeApproximately(10, 0.1, "1MB at 100KB/s should be ~10 seconds");
    }

    [Fact]
    public void GetBufferStatus_Should_Handle_Zero_Download_Speed()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act - Zero download speed (stalled)
        manager.UpdateBuffer(streamId, bufferedBytes: 1_000_000, downloadSpeedBytesPerSecond: 0);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.BufferedSeconds.Should().Be(0, "zero speed means infinite time, treat as 0");
        status.IsBuffering.Should().BeTrue("should still be buffering");
    }

    [Fact]
    public void UpdatePlaybackPosition_Should_Reduce_Buffer()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();
        manager.UpdateBuffer(streamId, bufferedBytes: 2_000_000, downloadSpeedBytesPerSecond: 100_000);

        // Act - Playback consumes 500KB
        manager.UpdatePlaybackPosition(streamId, consumedBytes: 500_000);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.BufferedSeconds.Should().BeLessThan(20, "consumed bytes should reduce buffer");
    }

    [Fact]
    public void IsBuffering_Should_Be_False_When_Buffer_Sufficient()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();

        // Act - Buffer 20 seconds
        manager.UpdateBuffer(streamId, bufferedBytes: 2_000_000, downloadSpeedBytesPerSecond: 100_000);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.IsBuffering.Should().BeFalse("should not be buffering when buffer is sufficient");
    }

    [Fact]
    public void Constructor_Should_Set_Minimum_Buffer()
    {
        // Arrange & Act
        var manager = new BufferManager(minimumBufferSeconds: 15);

        // Assert
        manager.MinimumBufferSeconds.Should().Be(15);
    }

    [Fact]
    public void GetBufferStatus_Should_Create_New_Stream_If_Not_Exists()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var newStreamId = Guid.NewGuid();

        // Act
        var status = manager.GetBufferStatus(newStreamId);

        // Assert
        status.Should().NotBeNull("should create new buffer status for unknown stream");
        status.StreamId.Should().Be(newStreamId);
    }

    [Fact]
    public void RemoveStream_Should_Cleanup_Buffer_Tracking()
    {
        // Arrange
        var manager = new BufferManager(minimumBufferSeconds: 10);
        var streamId = Guid.NewGuid();
        manager.UpdateBuffer(streamId, bufferedBytes: 1_000_000, downloadSpeedBytesPerSecond: 100_000);

        // Act
        manager.RemoveStream(streamId);
        var status = manager.GetBufferStatus(streamId);

        // Assert
        status.BufferedSeconds.Should().Be(0, "removed stream should reset to initial state");
    }
}

