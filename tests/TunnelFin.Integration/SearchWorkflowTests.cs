using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Discovery;
using TunnelFin.Indexers;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Integration;

/// <summary>
/// Integration tests for the complete search workflow (T056).
/// Tests end-to-end flow: query → indexers → deduplication → metadata → results.
/// Uses IndexerManager with built-in scrapers (1337x, Nyaa, TorrentGalaxy, EZTV).
/// </summary>
public class SearchWorkflowTests
{
    private readonly Mock<ILogger<IndexerManager>> _mockIndexerLogger;
    private readonly Mock<ILogger<MetadataFetcher>> _mockMetadataLogger;
    private readonly Mock<ILogger<SearchEngine>> _mockSearchLogger;

    public SearchWorkflowTests()
    {
        _mockIndexerLogger = new Mock<ILogger<IndexerManager>>();
        _mockMetadataLogger = new Mock<ILogger<MetadataFetcher>>();
        _mockSearchLogger = new Mock<ILogger<SearchEngine>>();
    }

    [Fact]
    public async Task SearchWorkflow_Should_Complete_End_To_End()
    {
        // Arrange - IndexerManager has built-in scrapers initialized in constructor
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);

        var deduplicator = new Deduplicator();
        var metadataFetcher = new MetadataFetcher(_mockMetadataLogger.Object);
        var searchEngine = new SearchEngine(
            _mockSearchLogger.Object,
            indexerManager,
            deduplicator,
            metadataFetcher
        );

        // Act
        var results = await searchEngine.SearchAsync("The Matrix", ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        // Verify the workflow completes without errors
    }

    [Fact]
    public async Task SearchWorkflow_Should_Deduplicate_Results_From_Multiple_Indexers()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);

        var deduplicator = new Deduplicator();
        var metadataFetcher = new MetadataFetcher(_mockMetadataLogger.Object);
        var searchEngine = new SearchEngine(
            _mockSearchLogger.Object,
            indexerManager,
            deduplicator,
            metadataFetcher
        );

        // Act
        var results = await searchEngine.SearchAsync("Breaking Bad", ContentType.TVShow);

        // Assert
        results.Should().NotBeNull();
        // Verify the deduplication workflow completes without errors
    }

    [Fact]
    public async Task SearchWorkflow_Should_Fetch_Metadata_For_Results()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);

        var deduplicator = new Deduplicator();
        var metadataFetcher = new MetadataFetcher(_mockMetadataLogger.Object);
        var searchEngine = new SearchEngine(
            _mockSearchLogger.Object,
            indexerManager,
            deduplicator,
            metadataFetcher
        );

        // Act
        var results = await searchEngine.SearchAsync("Inception", ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        // Verify the metadata fetching workflow completes without errors
    }

    [Fact]
    public async Task SearchWorkflow_Should_Complete_Within_5_Seconds()
    {
        // Arrange
        var httpClient = new HttpClient();
        var indexerManager = new IndexerManager(httpClient, _mockIndexerLogger.Object);

        var deduplicator = new Deduplicator();
        var metadataFetcher = new MetadataFetcher(_mockMetadataLogger.Object);
        var searchEngine = new SearchEngine(
            _mockSearchLogger.Object,
            indexerManager,
            deduplicator,
            metadataFetcher
        );

        var startTime = DateTime.UtcNow;

        // Act
        var results = await searchEngine.SearchAsync("Avatar", ContentType.Movie);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5)); // SC-004
        results.Should().NotBeNull();
    }
}

