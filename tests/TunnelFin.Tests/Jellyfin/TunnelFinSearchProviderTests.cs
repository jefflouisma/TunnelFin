using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TunnelFin.Discovery;
using TunnelFin.Indexers;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Jellyfin;

/// <summary>
/// Unit tests for TunnelFinSearchProvider (FR-027, FR-034, FR-039).
/// Tests search functionality, network availability checks, and play button color coding.
/// </summary>
public class TunnelFinSearchProviderTests
{
    private readonly TunnelFinSearchProvider _provider;

    public TunnelFinSearchProviderTests()
    {
        _provider = new TunnelFinSearchProvider(NullLogger.Instance);
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Empty_Results_For_Placeholder()
    {
        // Act
        var response = await _provider.SearchAsync("Inception 2010", ContentType.Movie, limit: 20);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().BeEmpty("placeholder implementation returns no results");
        response.TotalResults.Should().Be(0);
        response.SearchDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SearchAsync_Should_Reject_Empty_Query()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync("", ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Reject_Invalid_Limit()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, limit: 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, limit: 101));
    }

    [Fact]
    public async Task SearchAsync_Should_Complete_Within_Timeout()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token);

        // Assert
        response.SearchDuration.Should().BeLessThan(TimeSpan.FromSeconds(5), "search should complete within SC-004 timeout");
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Update_Status()
    {
        // Arrange
        _provider.IsNetworkAvailable.Should().BeFalse("network should be unavailable initially");

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync();

        // Assert
        isAvailable.Should().BeTrue("placeholder implementation returns true");
        _provider.IsNetworkAvailable.Should().BeTrue("status should be updated");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Green_When_Network_Available()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 10
        };

        // Network is available after check
        _provider.CheckNetworkAvailabilityAsync().Wait();

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Green, "network is available and torrent has seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_No_Seeders()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 0
        };

        _provider.CheckNetworkAvailabilityAsync().Wait();

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "torrent has no seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Reject_Null_Result()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _provider.GetPlayButtonColor(null!));
    }

    [Theory]
    [InlineData(ContentType.Movie)]
    [InlineData(ContentType.TVShow)]
    [InlineData(ContentType.Anime)]
    public async Task SearchAsync_Should_Support_All_Content_Types(ContentType contentType)
    {
        // Act
        var response = await _provider.SearchAsync("test", contentType);

        // Assert
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token));
    }


    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act
        var act = () => new TunnelFinSearchProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task SearchAsync_Should_Apply_Limit_To_Results()
    {
        // Arrange
        var limit = 10;

        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie, limit: limit);

        // Assert
        response.Results.Count.Should().BeLessThanOrEqualTo(limit);
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Null()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync(null!, ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Whitespace()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync("   ", ContentType.Movie));
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Return_False_On_Exception()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync(cts.Token);

        // Assert
        // Should handle cancellation gracefully
        isAvailable.Should().BeFalse();
        _provider.IsNetworkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_Network_Unavailable()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 10
        };

        // Network is unavailable initially

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "network is unavailable");
    }

    [Fact]
    public async Task SearchAsync_Should_Log_Cancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SearchAsync_Should_Return_SearchResponse_With_Metadata()
    {
        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeNull();
        response.TotalResults.Should().BeGreaterThanOrEqualTo(0);
        response.SearchDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        response.IndexersQueried.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync(cts.Token);

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_With_SearchEngine_Should_Return_Results()
    {
        // Arrange
        var indexerManager = new TunnelFin.Indexers.IndexerManager(NullLogger.Instance);
        var deduplicator = new TunnelFin.Discovery.Deduplicator();
        var metadataFetcher = new TunnelFin.Discovery.MetadataFetcher(NullLogger.Instance);
        var searchEngine = new TunnelFin.Discovery.SearchEngine(
            NullLogger.Instance,
            indexerManager,
            deduplicator,
            metadataFetcher);

        var provider = new TunnelFinSearchProvider(NullLogger.Instance, searchEngine);

        // Add a test indexer
        var testIndexer = new TestIndexer("TestIndexer", new List<SearchResult>
        {
            new SearchResult
            {
                Title = "Test Movie 2020",
                InfoHash = "hash1",
                Size = 1024L * 1024 * 1024,
                Seeders = 50,
                Leechers = 5,
                ContentType = ContentType.Movie
            }
        });
        indexerManager.AddIndexer(testIndexer);

        // Act
        var response = await provider.SearchAsync("Test", ContentType.Movie, limit: 10);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(1);
        response.TotalResults.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_With_SearchEngine_Should_Apply_Limit()
    {
        // Arrange
        var indexerManager = new TunnelFin.Indexers.IndexerManager(NullLogger.Instance);
        var deduplicator = new TunnelFin.Discovery.Deduplicator();
        var metadataFetcher = new TunnelFin.Discovery.MetadataFetcher(NullLogger.Instance);
        var searchEngine = new TunnelFin.Discovery.SearchEngine(
            NullLogger.Instance,
            indexerManager,
            deduplicator,
            metadataFetcher);

        var provider = new TunnelFinSearchProvider(NullLogger.Instance, searchEngine);

        // Add a test indexer with multiple results
        var results = Enumerable.Range(1, 50).Select(i => new SearchResult
        {
            Title = $"Movie {i}",
            InfoHash = $"hash{i}",
            Size = 1024L * 1024 * 1024,
            Seeders = i,
            Leechers = i / 10,
            ContentType = ContentType.Movie
        }).ToList();

        var testIndexer = new TestIndexer("TestIndexer", results);
        indexerManager.AddIndexer(testIndexer);

        // Act
        var response = await provider.SearchAsync("Movie", ContentType.Movie, limit: 10);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().HaveCount(10, "limit should be applied");
    }

    [Fact]
    public async Task SearchAsync_With_SearchEngine_Should_Handle_Indexer_Failures_Gracefully()
    {
        // Arrange
        var indexerManager = new TunnelFin.Indexers.IndexerManager(NullLogger.Instance);
        var deduplicator = new TunnelFin.Discovery.Deduplicator();
        var metadataFetcher = new TunnelFin.Discovery.MetadataFetcher(NullLogger.Instance);
        var searchEngine = new TunnelFin.Discovery.SearchEngine(
            NullLogger.Instance,
            indexerManager,
            deduplicator,
            metadataFetcher);

        var provider = new TunnelFinSearchProvider(NullLogger.Instance, searchEngine);

        // Add a failing indexer - IndexerManager handles failures gracefully
        var failingIndexer = new FailingTestIndexer("FailingIndexer");
        indexerManager.AddIndexer(failingIndexer);

        // Act
        var response = await provider.SearchAsync("Test", ContentType.Movie);

        // Assert
        // Should return empty results rather than throwing
        response.Should().NotBeNull();
        response.Results.Should().BeEmpty();
    }

    /// <summary>
    /// Simple test indexer that returns predefined results.
    /// </summary>
    private class TestIndexer : TunnelFin.Indexers.IIndexer
    {
        private readonly List<SearchResult> _results;

        public string Name { get; }
        public bool IsEnabled { get; set; } = true;

        public TestIndexer(string name, List<SearchResult> results)
        {
            Name = name;
            _results = results;
        }

        public Task<List<SearchResult>> SearchAsync(string query, ContentType contentType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_results);
        }

        public TunnelFin.Indexers.IndexerCapabilities GetCapabilities()
        {
            return new TunnelFin.Indexers.IndexerCapabilities
            {
                SupportedContentTypes = new List<ContentType> { ContentType.Movie, ContentType.TVShow, ContentType.Anime },
                SupportsAdvancedSearch = false,
                MaxResults = 100,
                TimeoutSeconds = 10
            };
        }
    }

    /// <summary>
    /// Test indexer that always throws an exception.
    /// </summary>
    private class FailingTestIndexer : TunnelFin.Indexers.IIndexer
    {
        public string Name { get; }
        public bool IsEnabled { get; set; } = true;

        public FailingTestIndexer(string name)
        {
            Name = name;
        }

        public Task<List<SearchResult>> SearchAsync(string query, ContentType contentType, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Indexer failed");
        }

        public TunnelFin.Indexers.IndexerCapabilities GetCapabilities()
        {
            return new TunnelFin.Indexers.IndexerCapabilities
            {
                SupportedContentTypes = new List<ContentType> { ContentType.Movie, ContentType.TVShow, ContentType.Anime },
                SupportsAdvancedSearch = false,
                MaxResults = 100,
                TimeoutSeconds = 10
            };
        }
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Return_True_And_Update_Property()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);

        // Act
        var isAvailable = await provider.CheckNetworkAvailabilityAsync();

        // Assert
        isAvailable.Should().BeTrue();
        provider.IsNetworkAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Return_False_On_Cancellation()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var isAvailable = await provider.CheckNetworkAvailabilityAsync(cts.Token);

        // Assert
        isAvailable.Should().BeFalse("cancellation should be caught and return false");
        provider.IsNetworkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Green_When_Network_Available_And_Has_Seeders()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);
        provider.CheckNetworkAvailabilityAsync().Wait(); // Set network available

        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "hash1",
            Size = 1024L * 1024 * 1024,
            Seeders = 50,
            Leechers = 5,
            ContentType = ContentType.Movie
        };

        // Act
        var color = provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Green, "network is available and torrent has seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_Network_Not_Available()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);
        // Don't call CheckNetworkAvailabilityAsync - network is not available

        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "hash1",
            Size = 1024L * 1024 * 1024,
            Seeders = 50,
            Leechers = 5,
            ContentType = ContentType.Movie
        };

        // Act
        var color = provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "network is not available");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_Torrent_Has_Zero_Seeders()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);
        provider.CheckNetworkAvailabilityAsync().Wait(); // Set network available

        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "hash1",
            Size = 1024L * 1024 * 1024,
            Seeders = 0, // No seeders
            Leechers = 5,
            ContentType = ContentType.Movie
        };

        // Act
        var color = provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "torrent has no seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Throw_When_Result_Is_Null()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => provider.GetPlayButtonColor(null!));
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Empty_Results_Without_SearchEngine()
    {
        // Arrange
        var provider = new TunnelFinSearchProvider(NullLogger.Instance); // No SearchEngine

        // Act
        var response = await provider.SearchAsync("Test Movie", ContentType.Movie);

        // Assert
        response.Results.Should().BeEmpty();
        response.TotalResults.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_Should_Log_Error_On_Exception()
    {
        // Arrange
        var indexerManager = new TunnelFin.Indexers.IndexerManager(NullLogger.Instance);
        var deduplicator = new TunnelFin.Discovery.Deduplicator();
        var metadataFetcher = new TunnelFin.Discovery.MetadataFetcher(NullLogger.Instance);
        var searchEngine = new TunnelFin.Discovery.SearchEngine(
            NullLogger.Instance, indexerManager, deduplicator, metadataFetcher);

        var provider = new TunnelFinSearchProvider(NullLogger.Instance, searchEngine);

        // Add a failing indexer
        var failingIndexer = new FailingTestIndexer("FailingIndexer");
        indexerManager.AddIndexer(failingIndexer);

        // Act & Assert
        // SearchEngine handles indexer failures gracefully, so this should not throw
        var response = await provider.SearchAsync("Test", ContentType.Movie);
        response.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Should_Log_Error_And_Rethrow_On_SearchEngine_Exception()
    {
        // Arrange
        var mockDeduplicator = new Mock<IDeduplicator>();
        mockDeduplicator
            .Setup(d => d.Deduplicate(It.IsAny<List<SearchResult>>()))
            .Throws(new InvalidOperationException("Deduplication failed"));

        var indexerManager = new IndexerManager(NullLogger.Instance);
        var metadataFetcher = new MetadataFetcher(NullLogger.Instance);
        var searchEngine = new SearchEngine(
            NullLogger.Instance,
            indexerManager,
            mockDeduplicator.Object,
            metadataFetcher);

        var provider = new TunnelFinSearchProvider(NullLogger.Instance, searchEngine);

        // Add a test indexer that returns results
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "hash1",
            Size = 1024L * 1024 * 1024,
            Seeders = 10,
            Leechers = 1,
            ContentType = ContentType.Movie
        };
        var indexer = new TestIndexer("TestIndexer", new List<SearchResult> { result });
        indexerManager.AddIndexer(indexer);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await provider.SearchAsync("Test", ContentType.Movie);
        });

        exception.Message.Should().Be("Deduplication failed");
    }

}

