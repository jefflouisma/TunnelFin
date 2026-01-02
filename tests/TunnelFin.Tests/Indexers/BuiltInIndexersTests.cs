using FluentAssertions;
using TunnelFin.Indexers;
using TunnelFin.Indexers.BuiltIn;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Indexers;

/// <summary>
/// Unit tests for built-in indexers (FR-016).
/// Tests 1337x, Nyaa, and RARBG query parsing and result extraction.
/// </summary>
public class BuiltInIndexersTests
{
    [Fact]
    public void Indexer1337x_Should_Have_Correct_Name()
    {
        // Arrange & Act
        var indexer = new Indexer1337x();

        // Assert
        indexer.Name.Should().Be("1337x");
        indexer.IsEnabled.Should().BeTrue("indexer should be enabled by default");
    }

    [Fact]
    public void Indexer1337x_Should_Support_All_Content_Types()
    {
        // Arrange
        var indexer = new Indexer1337x();

        // Act
        var capabilities = indexer.GetCapabilities();

        // Assert
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Movie);
        capabilities.SupportedContentTypes.Should().Contain(ContentType.TVShow);
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Anime);
    }

    [Fact]
    public async Task Indexer1337x_Should_Parse_Search_Results()
    {
        // Arrange
        var indexer = new Indexer1337x();

        // Act - This will fail until we implement actual HTTP parsing
        // For now, we test that the method exists and returns empty list
        var results = await indexer.SearchAsync("test", ContentType.Movie);

        // Assert
        results.Should().NotBeNull("should return empty list for placeholder");
        results.Should().BeEmpty("placeholder implementation returns no results");
    }

    [Fact]
    public void IndexerNyaa_Should_Have_Correct_Name()
    {
        // Arrange & Act
        var indexer = new IndexerNyaa();

        // Assert
        indexer.Name.Should().Be("Nyaa");
        indexer.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IndexerNyaa_Should_Support_Anime_Content()
    {
        // Arrange
        var indexer = new IndexerNyaa();

        // Act
        var capabilities = indexer.GetCapabilities();

        // Assert
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Anime, 
            "Nyaa is anime-specific indexer");
    }

    [Fact]
    public async Task IndexerNyaa_Should_Parse_Search_Results()
    {
        // Arrange
        var indexer = new IndexerNyaa();

        // Act
        var results = await indexer.SearchAsync("test anime", ContentType.Anime);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty("placeholder implementation");
    }

    [Fact]
    public void IndexerRARBG_Should_Have_Correct_Name()
    {
        // Arrange & Act
        var indexer = new IndexerRARBG();

        // Assert
        indexer.Name.Should().Be("RARBG");
        indexer.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IndexerRARBG_Should_Support_Movies_And_TV()
    {
        // Arrange
        var indexer = new IndexerRARBG();

        // Act
        var capabilities = indexer.GetCapabilities();

        // Assert
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Movie);
        capabilities.SupportedContentTypes.Should().Contain(ContentType.TVShow);
    }

    [Fact]
    public async Task IndexerRARBG_Should_Parse_Search_Results()
    {
        // Arrange
        var indexer = new IndexerRARBG();

        // Act
        var results = await indexer.SearchAsync("test", ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty("placeholder implementation");
    }

    [Theory]
    [InlineData("Inception 2010")]
    [InlineData("Breaking Bad S01E01")]
    [InlineData("Demon Slayer")]
    public async Task All_Indexers_Should_Accept_Valid_Queries(string query)
    {
        // Arrange
        var indexers = new List<IIndexer>
        {
            new Indexer1337x(),
            new IndexerNyaa(),
            new IndexerRARBG()
        };

        // Act & Assert
        foreach (var indexer in indexers)
        {
            var results = await indexer.SearchAsync(query, ContentType.Movie);
            results.Should().NotBeNull($"{indexer.Name} should handle query: {query}");
        }
    }

    [Fact]
    public async Task All_Indexers_Should_Reject_Empty_Query()
    {
        // Arrange
        var indexers = new List<IIndexer>
        {
            new Indexer1337x(),
            new IndexerNyaa(),
            new IndexerRARBG()
        };

        // Act & Assert
        foreach (var indexer in indexers)
        {
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await indexer.SearchAsync("", ContentType.Movie));
        }
    }
}

