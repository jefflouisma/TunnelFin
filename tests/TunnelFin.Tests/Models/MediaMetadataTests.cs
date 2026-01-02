using FluentAssertions;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Models;

/// <summary>
/// Unit tests for MediaMetadata (FR-029).
/// Tests metadata properties and enum values.
/// </summary>
public class MediaMetadataTests
{
    [Fact]
    public void MediaMetadata_Should_Initialize_With_Default_Values()
    {
        // Act
        var metadata = new MediaMetadata();

        // Assert
        metadata.Id.Should().Be(Guid.Empty);
        metadata.Title.Should().BeEmpty();
        metadata.OriginalTitle.Should().BeNull();
        metadata.Year.Should().BeNull();
        metadata.Overview.Should().BeNull();
        metadata.PosterUrl.Should().BeNull();
        metadata.BackdropUrl.Should().BeNull();
        metadata.TmdbId.Should().BeNull();
        metadata.AniListId.Should().BeNull();
        metadata.ImdbId.Should().BeNull();
        metadata.ContentRating.Should().BeNull();
        metadata.Rating.Should().BeNull();
        metadata.VoteCount.Should().BeNull();
        metadata.Genres.Should().NotBeNull().And.BeEmpty();
        metadata.Cast.Should().NotBeNull().And.BeEmpty();
        metadata.Directors.Should().NotBeNull().And.BeEmpty();
        metadata.RuntimeMinutes.Should().BeNull();
        metadata.Season.Should().BeNull();
        metadata.Episode.Should().BeNull();
        metadata.EpisodeTitle.Should().BeNull();
        metadata.Source.Should().Be(MetadataSource.TMDB);
        metadata.MatchConfidence.Should().Be(0.0);
        metadata.FetchedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void MediaMetadata_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var fetchedAt = DateTime.UtcNow;

        // Act
        var metadata = new MediaMetadata
        {
            Id = id,
            Title = "Inception",
            OriginalTitle = "Inception (Original)",
            Year = 2010,
            Overview = "A thief who steals corporate secrets...",
            PosterUrl = "https://image.tmdb.org/poster.jpg",
            BackdropUrl = "https://image.tmdb.org/backdrop.jpg",
            TmdbId = 27205,
            AniListId = null,
            ImdbId = "tt1375666",
            ContentRating = "PG-13",
            Rating = 8.8,
            VoteCount = 35000,
            Genres = new List<string> { "Action", "Sci-Fi", "Thriller" },
            Cast = new List<string> { "Leonardo DiCaprio", "Joseph Gordon-Levitt" },
            Directors = new List<string> { "Christopher Nolan" },
            RuntimeMinutes = 148,
            Season = null,
            Episode = null,
            EpisodeTitle = null,
            Source = MetadataSource.TMDB,
            MatchConfidence = 0.95,
            FetchedAt = fetchedAt
        };

        // Assert
        metadata.Id.Should().Be(id);
        metadata.Title.Should().Be("Inception");
        metadata.OriginalTitle.Should().Be("Inception (Original)");
        metadata.Year.Should().Be(2010);
        metadata.Overview.Should().Be("A thief who steals corporate secrets...");
        metadata.PosterUrl.Should().Be("https://image.tmdb.org/poster.jpg");
        metadata.BackdropUrl.Should().Be("https://image.tmdb.org/backdrop.jpg");
        metadata.TmdbId.Should().Be(27205);
        metadata.AniListId.Should().BeNull();
        metadata.ImdbId.Should().Be("tt1375666");
        metadata.ContentRating.Should().Be("PG-13");
        metadata.Rating.Should().Be(8.8);
        metadata.VoteCount.Should().Be(35000);
        metadata.Genres.Should().Equal("Action", "Sci-Fi", "Thriller");
        metadata.Cast.Should().Equal("Leonardo DiCaprio", "Joseph Gordon-Levitt");
        metadata.Directors.Should().Equal("Christopher Nolan");
        metadata.RuntimeMinutes.Should().Be(148);
        metadata.Season.Should().BeNull();
        metadata.Episode.Should().BeNull();
        metadata.EpisodeTitle.Should().BeNull();
        metadata.Source.Should().Be(MetadataSource.TMDB);
        metadata.MatchConfidence.Should().Be(0.95);
        metadata.FetchedAt.Should().Be(fetchedAt);
    }

    [Fact]
    public void MediaMetadata_Should_Support_TV_Show_Properties()
    {
        // Act
        var metadata = new MediaMetadata
        {
            Title = "Breaking Bad",
            Year = 2008,
            Season = 5,
            Episode = 14,
            EpisodeTitle = "Ozymandias",
            Source = MetadataSource.TMDB,
            MatchConfidence = 1.0
        };

        // Assert
        metadata.Season.Should().Be(5);
        metadata.Episode.Should().Be(14);
        metadata.EpisodeTitle.Should().Be("Ozymandias");
    }

    [Fact]
    public void MediaMetadata_Should_Support_Anime_Properties()
    {
        // Act
        var metadata = new MediaMetadata
        {
            Title = "Attack on Titan",
            Year = 2013,
            AniListId = 16498,
            Source = MetadataSource.AniList,
            MatchConfidence = 0.98
        };

        // Assert
        metadata.AniListId.Should().Be(16498);
        metadata.Source.Should().Be(MetadataSource.AniList);
    }

    [Fact]
    public void MetadataSource_Should_Have_Three_Values()
    {
        // Assert
        Enum.GetValues<MetadataSource>().Should().HaveCount(3);
        Enum.GetValues<MetadataSource>().Should().Contain(MetadataSource.TMDB);
        Enum.GetValues<MetadataSource>().Should().Contain(MetadataSource.AniList);
        Enum.GetValues<MetadataSource>().Should().Contain(MetadataSource.Filename);
    }
}

