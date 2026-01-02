using FluentAssertions;
using TunnelFin.BitTorrent;
using Xunit;

namespace TunnelFin.Tests.BitTorrent;

/// <summary>
/// Unit tests for PiecePrioritizer (FR-008).
/// Tests sequential piece selection for streaming playback.
/// </summary>
public class PiecePrioritizerTests
{
    [Fact]
    public void GetNextPieces_Should_Return_Sequential_Pieces()
    {
        // Arrange - FR-008
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = new[] { 0, 1, 2, 3, 4, 5, 10, 15, 20, 50 };

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 0);

        // Assert
        nextPieces.Should().NotBeEmpty("should return pieces to download");
        nextPieces.First().Should().Be(0, "should start from current position");
        nextPieces.Should().BeInAscendingOrder("pieces should be sequential");
    }

    [Fact]
    public void GetNextPieces_Should_Prioritize_Within_Buffer_Window()
    {
        // Arrange - FR-008 (10-20 pieces ahead)
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = new[] { 5, 6, 7, 8, 9, 10, 11, 12, 50, 60, 70 };

        // Act - Current position is 5
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 5);

        // Assert
        nextPieces.Should().NotContain(50, "piece 50 is outside buffer window");
        nextPieces.Should().NotContain(60, "piece 60 is outside buffer window");
        nextPieces.Should().NotContain(70, "piece 70 is outside buffer window");
        nextPieces.Should().Contain(5, "piece 5 is at current position");
        nextPieces.Should().Contain(6, "piece 6 is within buffer window");
    }

    [Fact]
    public void GetNextPieces_Should_Handle_Empty_Available_Pieces()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = Array.Empty<int>();

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 0);

        // Assert
        nextPieces.Should().BeEmpty("no pieces available to download");
    }

    [Fact]
    public void UpdatePlaybackPosition_Should_Adjust_Priority_Window()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = new[] { 0, 1, 2, 10, 11, 12, 20, 21, 22 };

        // Act - Move playback position forward
        var pieces1 = prioritizer.GetNextPieces(availablePieces, currentPosition: 0);
        var pieces2 = prioritizer.GetNextPieces(availablePieces, currentPosition: 10);

        // Assert
        pieces1.Should().Contain(0, "piece 0 is in first window");
        pieces2.Should().Contain(10, "piece 10 is in second window");
        pieces2.Should().NotContain(0, "piece 0 is behind playback position");
    }

    [Fact]
    public void GetNextPieces_Should_Limit_To_Buffer_Window_Size()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 5);
        var availablePieces = Enumerable.Range(0, 100).ToArray();

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 0);

        // Assert
        nextPieces.Should().HaveCountLessThanOrEqualTo(5, "should not exceed buffer window size");
    }

    [Fact]
    public void Constructor_Should_Set_Configuration()
    {
        // Arrange & Act
        var prioritizer = new PiecePrioritizer(totalPieces: 200, bufferWindowSize: 20);

        // Assert
        prioritizer.TotalPieces.Should().Be(200);
        prioritizer.BufferWindowSize.Should().Be(20);
    }

    [Fact]
    public void GetNextPieces_Should_Handle_Position_Near_End()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = new[] { 95, 96, 97, 98, 99 };

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 95);

        // Assert
        nextPieces.Should().NotBeEmpty("should return remaining pieces");
        nextPieces.Should().BeInAscendingOrder("pieces should be sequential");
        nextPieces.All(p => p >= 95).Should().BeTrue("all pieces should be at or after current position");
    }

    [Fact]
    public void GetNextPieces_Should_Skip_Already_Downloaded_Pieces()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        // Simulate pieces 0-4 already downloaded (not in available list)
        var availablePieces = new[] { 5, 6, 7, 8, 9, 10 };

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 0);

        // Assert
        nextPieces.Should().NotContain(0, "piece 0 is already downloaded");
        nextPieces.Should().NotContain(1, "piece 1 is already downloaded");
        nextPieces.Should().Contain(5, "piece 5 is next available");
    }

    [Fact]
    public void Constructor_Should_Validate_Parameters()
    {
        // Arrange & Act
        var act1 = () => new PiecePrioritizer(totalPieces: 0, bufferWindowSize: 10);
        var act2 = () => new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 0);

        // Assert
        act1.Should().Throw<ArgumentException>("total pieces must be positive");
        act2.Should().Throw<ArgumentException>("buffer window size must be positive");
    }
}

