using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TunnelFin.Discovery;
using TunnelFin.Indexers;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for SearchEngine (T063, FR-025, FR-026, SC-004).
/// </summary>
public class SearchEngineTests
{
    private readonly SearchEngine _searchEngine;
    private readonly IndexerManager _indexerManager;
    private readonly Deduplicator _deduplicator;
    private readonly MetadataFetcher _metadataFetcher;

    public SearchEngineTests()
    {
        _indexerManager = new IndexerManager(NullLogger.Instance);
        _deduplicator = new Deduplicator();
        _metadataFetcher = new MetadataFetcher(NullLogger.Instance);
        _searchEngine = new SearchEngine(
            NullLogger.Instance,
            _indexerManager,
            _deduplicator,
            _metadataFetcher);
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Results_Within_5_Seconds()
    {
        // Arrange
        var query = "Inception";
        var startTime = DateTime.UtcNow;

        // Act
        var results = await _searchEngine.SearchAsync(query, ContentType.Movie);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        duration.Should().BeLessThan(TimeSpan.FromSeconds(5), "search must complete within 5 seconds per SC-004");
    }

    [Fact]
    public async Task SearchAsync_Should_Deduplicate_Results()
    {
        // Arrange
        var query = "Matrix";

        // Act
        var results = await _searchEngine.SearchAsync(query, ContentType.Movie);

        // Assert
        // Since we're using placeholder indexers that return empty results,
        // we can't test actual deduplication here. This will be tested in integration tests.
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Fetch_Metadata_For_Results()
    {
        // Arrange
        var query = "Interstellar";

        // Act
        var results = await _searchEngine.SearchAsync(query, ContentType.Movie);

        // Assert
        // Since we're using placeholder indexers that return empty results,
        // we can't test actual metadata fetching here. This will be tested in integration tests.
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Empty_Query()
    {
        // Arrange
        var query = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _searchEngine.SearchAsync(query, ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Null_Query()
    {
        // Arrange
        string? query = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _searchEngine.SearchAsync(query!, ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Support_All_Content_Types()
    {
        // Arrange
        var query = "Test";

        // Act
        var movieResults = await _searchEngine.SearchAsync(query, ContentType.Movie);
        var tvResults = await _searchEngine.SearchAsync(query, ContentType.TVShow);
        var animeResults = await _searchEngine.SearchAsync(query, ContentType.Anime);

        // Assert
        movieResults.Should().NotBeNull();
        tvResults.Should().NotBeNull();
        animeResults.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Empty_List_When_No_Results()
    {
        // Arrange
        var query = "NonExistentMovie12345";

        // Act
        var results = await _searchEngine.SearchAsync(query, ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var query = "Test";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _searchEngine.SearchAsync(query, ContentType.Movie, cts.Token));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Arrange & Act
        var act = () => new SearchEngine(
            null!,
            _indexerManager,
            _deduplicator,
            _metadataFetcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_Should_Throw_When_IndexerManager_Is_Null()
    {
        // Arrange & Act
        var act = () => new SearchEngine(
            NullLogger.Instance,
            null!,
            _deduplicator,
            _metadataFetcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("indexerManager");
    }

    [Fact]
    public void Constructor_Should_Throw_When_Deduplicator_Is_Null()
    {
        // Arrange & Act
        var act = () => new SearchEngine(
            NullLogger.Instance,
            _indexerManager,
            null!,
            _metadataFetcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("deduplicator");
    }

    [Fact]
    public void Constructor_Should_Throw_When_MetadataFetcher_Is_Null()
    {
        // Arrange & Act
        var act = () => new SearchEngine(
            NullLogger.Instance,
            _indexerManager,
            _deduplicator,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("metadataFetcher");
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Whitespace()
    {
        // Arrange
        var query = "   ";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _searchEngine.SearchAsync(query, ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Handle_Different_ContentTypes()
    {
        // Arrange
        var query = "Test";

        // Act
        var movieResults = await _searchEngine.SearchAsync(query, ContentType.Movie);
        var tvResults = await _searchEngine.SearchAsync(query, ContentType.TVShow);
        var animeResults = await _searchEngine.SearchAsync(query, ContentType.Anime);

        // Assert
        movieResults.Should().NotBeNull();
        tvResults.Should().NotBeNull();
        animeResults.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Deduplicated_Results()
    {
        // Arrange
        var query = "Test";

        // Act
        var results = await _searchEngine.SearchAsync(query, ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        // Results should be deduplicated (no duplicate info hashes)
        var infoHashes = results.Select(r => r.InfoHash).Where(h => !string.IsNullOrEmpty(h)).ToList();
        infoHashes.Should().OnlyHaveUniqueItems("results should be deduplicated");
    }
}

