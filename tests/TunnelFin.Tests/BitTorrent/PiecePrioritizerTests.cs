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
        var act3 = () => new PiecePrioritizer(totalPieces: -1, bufferWindowSize: 10);
        var act4 = () => new PiecePrioritizer(totalPieces: 100, bufferWindowSize: -1);

        // Assert
        act1.Should().Throw<ArgumentException>("total pieces must be positive");
        act2.Should().Throw<ArgumentException>("buffer window size must be positive");
        act3.Should().Throw<ArgumentException>("total pieces must be positive");
        act4.Should().Throw<ArgumentException>("buffer window size must be positive");
    }

    [Fact]
    public void GetPiecePriority_Should_Return_Distance_For_Pieces_In_Window()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var priority0 = prioritizer.GetPiecePriority(pieceIndex: 5, currentPosition: 5);
        var priority1 = prioritizer.GetPiecePriority(pieceIndex: 6, currentPosition: 5);
        var priority5 = prioritizer.GetPiecePriority(pieceIndex: 10, currentPosition: 5);

        // Assert
        priority0.Should().Be(0, "piece at current position has highest priority");
        priority1.Should().Be(1, "next piece has priority 1");
        priority5.Should().Be(5, "piece 5 ahead has priority 5");
    }

    [Fact]
    public void GetPiecePriority_Should_Return_MaxValue_For_Pieces_Behind_Position()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var priority = prioritizer.GetPiecePriority(pieceIndex: 4, currentPosition: 5);

        // Assert
        priority.Should().Be(int.MaxValue, "pieces behind playback have lowest priority");
    }

    [Fact]
    public void GetPiecePriority_Should_Return_Low_Priority_For_Pieces_Beyond_Window()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var priority = prioritizer.GetPiecePriority(pieceIndex: 20, currentPosition: 5);

        // Assert
        priority.Should().Be(int.MaxValue - 1, "pieces beyond window have low priority");
    }

    [Fact]
    public void IsInBufferWindow_Should_Return_True_For_Pieces_In_Window()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var inWindow1 = prioritizer.IsInBufferWindow(pieceIndex: 5, currentPosition: 5);
        var inWindow2 = prioritizer.IsInBufferWindow(pieceIndex: 10, currentPosition: 5);
        var inWindow3 = prioritizer.IsInBufferWindow(pieceIndex: 14, currentPosition: 5);

        // Assert
        inWindow1.Should().BeTrue("piece at current position is in window");
        inWindow2.Should().BeTrue("piece 5 ahead is in window");
        inWindow3.Should().BeTrue("piece 9 ahead is in window");
    }

    [Fact]
    public void IsInBufferWindow_Should_Return_False_For_Pieces_Behind_Position()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var inWindow = prioritizer.IsInBufferWindow(pieceIndex: 4, currentPosition: 5);

        // Assert
        inWindow.Should().BeFalse("pieces behind position are not in window");
    }

    [Fact]
    public void IsInBufferWindow_Should_Return_False_For_Pieces_Beyond_Window()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var inWindow = prioritizer.IsInBufferWindow(pieceIndex: 15, currentPosition: 5);

        // Assert
        inWindow.Should().BeFalse("pieces beyond window are not in window");
    }

    [Fact]
    public void IsInBufferWindow_Should_Return_False_For_Pieces_Beyond_Total()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var inWindow = prioritizer.IsInBufferWindow(pieceIndex: 100, currentPosition: 95);

        // Assert
        inWindow.Should().BeFalse("pieces beyond total pieces are not in window");
    }

    [Fact]
    public void GetNextPieces_Should_Handle_Null_AvailablePieces()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);

        // Act
        var nextPieces = prioritizer.GetNextPieces(null!, currentPosition: 0);

        // Assert
        nextPieces.Should().BeEmpty("null available pieces should return empty");
    }

    [Fact]
    public void GetNextPieces_Should_Respect_Total_Pieces_Boundary()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = Enumerable.Range(98, 5).ToArray(); // 98, 99, 100, 101, 102

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 98);

        // Assert
        nextPieces.Should().NotContain(100, "piece 100 is beyond total pieces");
        nextPieces.Should().NotContain(101, "piece 101 is beyond total pieces");
        nextPieces.Should().NotContain(102, "piece 102 is beyond total pieces");
        nextPieces.Should().Contain(98);
        nextPieces.Should().Contain(99);
    }

    [Fact]
    public void GetNextPieces_Should_Return_Sequential_Order()
    {
        // Arrange
        var prioritizer = new PiecePrioritizer(totalPieces: 100, bufferWindowSize: 10);
        var availablePieces = new[] { 7, 5, 9, 6, 8 }; // Unordered

        // Act
        var nextPieces = prioritizer.GetNextPieces(availablePieces, currentPosition: 5);

        // Assert
        nextPieces.Should().BeInAscendingOrder("pieces should be returned in sequential order");
        nextPieces.Should().Equal(5, 6, 7, 8, 9);
    }
}

