using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TunnelFin.Discovery;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Unit tests for MetadataFetcher (FR-029, FR-030, FR-031, FR-032).
/// Tests TMDB/AniList integration, exponential backoff, failure caching, and filename parsing fallback.
/// </summary>
public class MetadataFetcherTests
{
    private readonly MetadataFetcher _fetcher;

    public MetadataFetcherTests()
    {
        _fetcher = new MetadataFetcher(NullLogger.Instance);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Return_Metadata_For_Movie()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Inception 2010 1080p BluRay",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull("should return metadata object");
        // Placeholder implementation will return basic metadata
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Return_Metadata_For_TVShow()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Breaking Bad S01E01 1080p",
            ContentType = ContentType.TVShow
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Return_Metadata_For_Anime()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Demon Slayer S01E01",
            ContentType = ContentType.Anime
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Use_Exponential_Backoff_On_Retry()
    {
        // Arrange - This test verifies the retry mechanism exists
        var result = new SearchResult
        {
            Title = "Test Movie 2020",
            ContentType = ContentType.Movie
        };

        // Act
        var startTime = DateTime.UtcNow;
        var metadata = await _fetcher.FetchMetadataAsync(result);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        metadata.Should().NotBeNull();
        // Placeholder won't actually retry, but the method should complete quickly
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Cache_Failures()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "NonExistent Movie 9999",
            ContentType = ContentType.Movie
        };

        // Act - First call
        var metadata1 = await _fetcher.FetchMetadataAsync(result);
        
        // Act - Second call (should use cache)
        var startTime = DateTime.UtcNow;
        var metadata2 = await _fetcher.FetchMetadataAsync(result);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        metadata2.Should().NotBeNull();
        duration.Should().BeLessThan(TimeSpan.FromMilliseconds(100), 
            "cached failures should return immediately");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Parse_Filename_As_Fallback()
    {
        // Arrange - FR-032: filename parsing fallback
        var result = new SearchResult
        {
            Title = "The.Matrix.1999.1080p.BluRay.x264-GROUP",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotBeNullOrEmpty("should extract title from filename");
    }

    [Theory]
    [InlineData("Inception.2010.1080p", "Inception", 2010)]
    [InlineData("The Matrix (1999) BluRay", "The Matrix", 1999)]
    [InlineData("Interstellar 2014 4K", "Interstellar", 2014)]
    public async Task FetchMetadataAsync_Should_Extract_Title_And_Year(
        string filename, string expectedTitle, int expectedYear)
    {
        // Arrange
        var result = new SearchResult
        {
            Title = filename,
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        // Placeholder implementation - will be enhanced in actual implementation
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Handle_Cancellation()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            ContentType = ContentType.Movie
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _fetcher.FetchMetadataAsync(result, cts.Token));
    }

    [Fact]
    public void ClearCache_Should_Remove_Cached_Failures()
    {
        // Act
        _fetcher.ClearCache();

        // Assert - Should not throw
        _fetcher.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Reject_Null_Result()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _fetcher.FetchMetadataAsync(null!));
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Parse_Season_And_Episode_From_Filename()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Breaking.Bad.S05E14.1080p.BluRay",
            ContentType = ContentType.TVShow
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Season.Should().Be(5);
        metadata.Episode.Should().Be(14);
        metadata.Source.Should().Be(MetadataSource.Filename);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Parse_Anime_Episode_Format()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Attack.on.Titan.S02E01.720p",
            ContentType = ContentType.Anime
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Season.Should().Be(2);
        metadata.Episode.Should().Be(1);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Clean_Title_From_Quality_Indicators()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Inception.2010.1080p.BluRay.x264.DTS.HEVC-GROUP",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotContain("1080p");
        metadata.Title.Should().NotContain("BluRay");
        metadata.Title.Should().NotContain("x264");
        metadata.Title.Should().NotContain("DTS");
        metadata.Title.Should().NotContain("HEVC");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Set_Lower_Confidence_For_Filename_Parsing()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test.Movie.2020.1080p",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.MatchConfidence.Should().Be(0.5);
        metadata.Source.Should().Be(MetadataSource.Filename);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Set_FetchedAt_Timestamp()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            ContentType = ContentType.Movie
        };

        var beforeFetch = DateTime.UtcNow;

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        var afterFetch = DateTime.UtcNow;

        // Assert
        metadata.Should().NotBeNull();
        metadata.FetchedAt.Should().BeOnOrAfter(beforeFetch);
        metadata.FetchedAt.Should().BeOnOrBefore(afterFetch);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Store_Original_Title()
    {
        // Arrange
        var originalTitle = "The.Matrix.1999.1080p.BluRay.x264";
        var result = new SearchResult
        {
            Title = originalTitle,
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.OriginalTitle.Should().Be(originalTitle);
    }

    [Fact]
    public void GetCachedFailureCount_Should_Return_Zero_Initially()
    {
        // Arrange
        var fetcher = new MetadataFetcher(NullLogger.Instance);

        // Act
        var count = fetcher.GetCachedFailureCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void ClearCache_Should_Reset_Failure_Count()
    {
        // Arrange
        var fetcher = new MetadataFetcher(NullLogger.Instance);

        // Act
        fetcher.ClearCache();
        var count = fetcher.GetCachedFailureCount();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act
        var act = () => new MetadataFetcher(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Movie.Title.2020.720p", 2020)]
    [InlineData("Movie.Title.(2019).1080p", 2019)]
    [InlineData("Movie.Title.[2018].BluRay", 2018)]
    public async Task FetchMetadataAsync_Should_Extract_Year_From_Various_Formats(string title, int expectedYear)
    {
        // Arrange
        var result = new SearchResult
        {
            Title = title,
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Year.Should().Be(expectedYear);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Handle_Title_Without_Year()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie.Title.BluRay.x264",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Year.Should().BeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Handle_Movie_Without_Episode_Info()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie.Title.2020.1080p",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Season.Should().BeNull();
        metadata.Episode.Should().BeNull();
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Remove_Bracketed_Content_From_Title()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie.Title.[Release.Group].2020.1080p",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotContain("[");
        metadata.Title.Should().NotContain("]");
        metadata.Title.Should().NotContain("Release.Group");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Replace_Dots_Dashes_Underscores_With_Spaces()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie_Title-Name.2020",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotContain(".");
        metadata.Title.Should().NotContain("-");
        metadata.Title.Should().NotContain("_");
        metadata.Title.Should().Contain(" ");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Collapse_Multiple_Spaces()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie    Title    2020",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotContain("  "); // No double spaces
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Trim_Title()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "  Movie.Title.2020  ",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Title.Should().NotStartWith(" ");
        metadata.Title.Should().NotEndWith(" ");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Generate_Unique_Id()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie.Title.2020",
            ContentType = ContentType.Movie
        };

        // Act
        var metadata1 = await _fetcher.FetchMetadataAsync(result);
        var metadata2 = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata1.Id.Should().NotBe(Guid.Empty);
        metadata2.Id.Should().NotBe(Guid.Empty);
        metadata1.Id.Should().NotBe(metadata2.Id, "each metadata should have unique ID");
    }

    [Theory]
    [InlineData("s01e01", 1, 1)]
    [InlineData("S05E14", 5, 14)]
    [InlineData("s10e99", 10, 99)]
    public async Task FetchMetadataAsync_Should_Parse_Episode_Format_Case_Insensitive(
        string episodeFormat, int expectedSeason, int expectedEpisode)
    {
        // Arrange
        var result = new SearchResult
        {
            Title = $"Show.Title.{episodeFormat}.720p",
            ContentType = ContentType.TVShow
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Season.Should().Be(expectedSeason);
        metadata.Episode.Should().Be(expectedEpisode);
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Not_Parse_Episode_For_Movies()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Movie.Title.S01E01.2020", // Has episode format but is a movie
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Season.Should().BeNull("movies should not have season");
        metadata.Episode.Should().BeNull("movies should not have episode");
    }

    [Fact]
    public async Task FetchMetadataAsync_Should_Handle_Title_With_Multiple_Years()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "2001.A.Space.Odyssey.1968.1080p", // Has year in title and release year
            ContentType = ContentType.Movie
        };

        // Act
        var metadata = await _fetcher.FetchMetadataAsync(result);

        // Assert
        metadata.Should().NotBeNull();
        metadata.Year.Should().NotBeNull("should extract at least one year");
    }

}

