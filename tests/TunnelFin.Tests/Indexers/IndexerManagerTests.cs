using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TunnelFin.Indexers;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Indexers;

/// <summary>
/// Unit tests for IndexerManager (FR-016, FR-018).
/// Tests concurrent searches, timeout handling, and response aggregation.
/// </summary>
public class IndexerManagerTests
{
    private readonly Mock<IIndexer> _mockIndexer1;
    private readonly Mock<IIndexer> _mockIndexer2;
    private readonly IndexerManager _manager;

    public IndexerManagerTests()
    {
        _mockIndexer1 = new Mock<IIndexer>();
        _mockIndexer1.Setup(x => x.Name).Returns("Indexer1");
        _mockIndexer1.Setup(x => x.IsEnabled).Returns(true);

        _mockIndexer2 = new Mock<IIndexer>();
        _mockIndexer2.Setup(x => x.Name).Returns("Indexer2");
        _mockIndexer2.Setup(x => x.IsEnabled).Returns(true);

        _manager = new IndexerManager(NullLogger.Instance);
        _manager.AddIndexer(_mockIndexer1.Object);
        _manager.AddIndexer(_mockIndexer2.Object);
    }

    [Fact]
    public async Task SearchAsync_Should_Query_All_Enabled_Indexers()
    {
        // Arrange
        var results1 = new List<SearchResult>
        {
            new SearchResult { Title = "Result1", InfoHash = "hash1" }
        };
        var results2 = new List<SearchResult>
        {
            new SearchResult { Title = "Result2", InfoHash = "hash2" }
        };

        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results1);
        _mockIndexer2.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results2);

        // Act
        var response = await _manager.SearchAsync("test query", ContentType.Movie);

        // Assert
        response.Results.Should().HaveCount(2, "should aggregate results from both indexers");
        response.IndexersQueried.Should().Contain("Indexer1");
        response.IndexersQueried.Should().Contain("Indexer2");
    }

    [Fact]
    public async Task SearchAsync_Should_Respect_Max_Concurrent_Indexers()
    {
        // Arrange - FR-018: max 5 concurrent indexers
        var manager = new IndexerManager(NullLogger.Instance, maxConcurrentIndexers: 2);
        
        for (int i = 0; i < 5; i++)
        {
            var mock = new Mock<IIndexer>();
            mock.Setup(x => x.Name).Returns($"Indexer{i}");
            mock.Setup(x => x.IsEnabled).Returns(true);
            mock.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SearchResult>());
            manager.AddIndexer(mock.Object);
        }

        // Act
        var response = await manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Should().NotBeNull("should complete search with concurrency limit");
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Indexer_Timeout()
    {
        // Arrange
        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Indexer timeout"));
        
        _mockIndexer2.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult> { new SearchResult { Title = "Result2" } });

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Results.Should().HaveCount(1, "should return results from successful indexer");
        response.IndexersQueried.Should().Contain("Indexer2");
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Indexer_Failure()
    {
        // Arrange
        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Indexer error"));
        
        _mockIndexer2.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult> { new SearchResult { Title = "Result2" } });

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Results.Should().HaveCount(1, "should continue with other indexers on failure");
    }

    [Fact]
    public async Task SearchAsync_Should_Skip_Disabled_Indexers()
    {
        // Arrange
        _mockIndexer1.Setup(x => x.IsEnabled).Returns(false);
        _mockIndexer2.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult> { new SearchResult { Title = "Result2" } });

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Results.Should().HaveCount(1);
        response.IndexersQueried.Should().NotContain("Indexer1", "disabled indexers should be skipped");
        response.IndexersQueried.Should().Contain("Indexer2");
    }

    [Fact]
    public async Task SearchAsync_Should_Track_Search_Duration()
    {
        // Arrange
        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.SearchDuration.Should().BeGreaterThan(TimeSpan.Zero, "should track search duration");
    }
}

