using FluentAssertions;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Models;

/// <summary>
/// Unit tests for SearchResult model.
/// Tests property initialization and data integrity.
/// </summary>
public class SearchResultTests
{
    [Fact]
    public void SearchResult_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var result = new SearchResult();

        // Assert
        result.ResultId.Should().BeEmpty();
        result.Title.Should().BeEmpty();
        result.InfoHash.Should().BeEmpty();
        result.MagnetUri.Should().BeEmpty();
        result.Size.Should().Be(0);
        result.Seeders.Should().Be(0);
        result.Leechers.Should().Be(0);
        result.IndexerName.Should().BeEmpty();
        result.RelevanceScore.Should().Be(0);
        result.PassesFilters.Should().BeFalse();
        result.MatchedFilters.Should().NotBeNull();
        result.MatchedFilters.Should().BeEmpty();
    }

    [Fact]
    public void SearchResult_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var resultId = Guid.NewGuid();
        var discoveredAt = DateTime.UtcNow;
        var uploadedAt = DateTime.UtcNow.AddDays(-7);

        // Act
        var result = new SearchResult
        {
            ResultId = resultId,
            Title = "Test Movie 1080p",
            InfoHash = "1234567890abcdef1234567890abcdef12345678",
            MagnetUri = "magnet:?xt=urn:btih:1234567890abcdef1234567890abcdef12345678",
            Size = 1073741824L, // 1GB
            Seeders = 100,
            Leechers = 50,
            IndexerName = "TestIndexer",
            IndexerType = IndexerType.BuiltIn,
            ContentType = ContentType.Movie,
            Quality = "1080p",
            Codec = "x264",
            Audio = "AAC",
            Language = "English",
            ReleaseGroup = "RARBG",
            DiscoveredAt = discoveredAt,
            UploadedAt = uploadedAt,
            TmdbId = 12345,
            AniListId = null,
            RelevanceScore = 95.5,
            PassesFilters = true,
            MatchedFilters = new List<string> { "1080p", "x264" }
        };

        // Assert
        result.ResultId.Should().Be(resultId);
        result.Title.Should().Be("Test Movie 1080p");
        result.InfoHash.Should().Be("1234567890abcdef1234567890abcdef12345678");
        result.MagnetUri.Should().Contain("magnet:?xt=urn:btih:");
        result.Size.Should().Be(1073741824L);
        result.Seeders.Should().Be(100);
        result.Leechers.Should().Be(50);
        result.IndexerName.Should().Be("TestIndexer");
        result.IndexerType.Should().Be(IndexerType.BuiltIn);
        result.ContentType.Should().Be(ContentType.Movie);
        result.Quality.Should().Be("1080p");
        result.Codec.Should().Be("x264");
        result.Audio.Should().Be("AAC");
        result.Language.Should().Be("English");
        result.ReleaseGroup.Should().Be("RARBG");
        result.DiscoveredAt.Should().Be(discoveredAt);
        result.UploadedAt.Should().Be(uploadedAt);
        result.TmdbId.Should().Be(12345);
        result.AniListId.Should().BeNull();
        result.RelevanceScore.Should().Be(95.5);
        result.PassesFilters.Should().BeTrue();
        result.MatchedFilters.Should().HaveCount(2);
        result.MatchedFilters.Should().Contain("1080p");
        result.MatchedFilters.Should().Contain("x264");
    }

    [Fact]
    public void SearchResult_Should_Support_Anime_Content_Type()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            ContentType = ContentType.Anime,
            AniListId = 98765,
            Language = "Japanese"
        };

        // Assert
        result.ContentType.Should().Be(ContentType.Anime);
        result.AniListId.Should().Be(98765);
        result.Language.Should().Be("Japanese");
    }

    [Fact]
    public void SearchResult_Should_Support_TVShow_Content_Type()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            ContentType = ContentType.TVShow,
            TmdbId = 54321,
            Title = "Test Series S01E01"
        };

        // Assert
        result.ContentType.Should().Be(ContentType.TVShow);
        result.TmdbId.Should().Be(54321);
        result.Title.Should().Contain("S01E01");
    }

    [Fact]
    public void SearchResult_Should_Support_Torznab_Indexer_Type()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            IndexerType = IndexerType.Torznab,
            IndexerName = "CustomTorznab"
        };

        // Assert
        result.IndexerType.Should().Be(IndexerType.Torznab);
        result.IndexerName.Should().Be("CustomTorznab");
    }

    [Fact]
    public void SearchResult_Should_Allow_Null_Optional_Fields()
    {
        // Arrange & Act
        var result = new SearchResult
        {
            Quality = null,
            Codec = null,
            Audio = null,
            Language = null,
            ReleaseGroup = null,
            UploadedAt = null,
            TmdbId = null,
            AniListId = null
        };

        // Assert
        result.Quality.Should().BeNull();
        result.Codec.Should().BeNull();
        result.Audio.Should().BeNull();
        result.Language.Should().BeNull();
        result.ReleaseGroup.Should().BeNull();
        result.UploadedAt.Should().BeNull();
        result.TmdbId.Should().BeNull();
        result.AniListId.Should().BeNull();
    }
}

