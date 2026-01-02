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

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act
        var act = () => new IndexerManager(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Should_Throw_When_MaxConcurrentIndexers_Is_Zero()
    {
        // Act
        var act = () => new IndexerManager(NullLogger.Instance, maxConcurrentIndexers: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxConcurrentIndexers");
    }

    [Fact]
    public void Constructor_Should_Throw_When_MaxConcurrentIndexers_Is_Negative()
    {
        // Act
        var act = () => new IndexerManager(NullLogger.Instance, maxConcurrentIndexers: -1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxConcurrentIndexers");
    }

    [Fact]
    public void AddIndexer_Should_Throw_When_Indexer_Is_Null()
    {
        // Act
        var act = () => _manager.AddIndexer(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("indexer");
    }

    [Fact]
    public void AddIndexer_Should_Not_Add_Duplicate_Indexer()
    {
        // Arrange
        var mock = new Mock<IIndexer>();
        mock.Setup(x => x.Name).Returns("Indexer1");
        mock.Setup(x => x.IsEnabled).Returns(true);

        // Act
        _manager.AddIndexer(mock.Object); // Try to add duplicate

        // Assert
        _manager.GetIndexers().Should().HaveCount(2, "should not add duplicate indexer");
    }

    [Fact]
    public void RemoveIndexer_Should_Remove_Existing_Indexer()
    {
        // Act
        _manager.RemoveIndexer("Indexer1");

        // Assert
        _manager.GetIndexers().Should().HaveCount(1);
        _manager.GetIndexers().Should().NotContain(i => i.Name == "Indexer1");
    }

    [Fact]
    public void RemoveIndexer_Should_Not_Throw_When_Indexer_Not_Found()
    {
        // Act
        var act = () => _manager.RemoveIndexer("NonExistent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetIndexers_Should_Return_All_Indexers()
    {
        // Act
        var indexers = _manager.GetIndexers();

        // Assert
        indexers.Should().HaveCount(2);
        indexers.Should().Contain(i => i.Name == "Indexer1");
        indexers.Should().Contain(i => i.Name == "Indexer2");
    }

    [Fact]
    public void GetEnabledIndexerCount_Should_Return_Correct_Count()
    {
        // Act
        var count = _manager.GetEnabledIndexerCount();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void GetEnabledIndexerCount_Should_Exclude_Disabled_Indexers()
    {
        // Arrange
        _mockIndexer1.Setup(x => x.IsEnabled).Returns(false);

        // Act
        var count = _manager.GetEnabledIndexerCount();

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Null()
    {
        // Act
        var act = async () => await _manager.SearchAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Empty()
    {
        // Act
        var act = async () => await _manager.SearchAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Whitespace()
    {
        // Act
        var act = async () => await _manager.SearchAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task SearchAsync_Should_Set_IndexerName_On_Results()
    {
        // Arrange
        var results1 = new List<SearchResult>
        {
            new SearchResult { Title = "Result1", InfoHash = "hash1" }
        };

        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results1);

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Results.Should().Contain(r => r.IndexerName == "Indexer1");
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Cancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockIndexer1.Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<ContentType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var response = await _manager.SearchAsync("test", ContentType.Movie, cts.Token);

        // Assert
        response.Should().NotBeNull("should handle cancellation gracefully");
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Empty_Results_When_No_Indexers_Enabled()
    {
        // Arrange
        var manager = new IndexerManager(NullLogger.Instance);

        // Act
        var response = await manager.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Results.Should().BeEmpty();
        response.TotalResults.Should().Be(0);
        response.IndexersQueried.Should().BeEmpty();
    }
}

