using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Discovery;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Jellyfin;

/// <summary>
/// Tests for MetadataProvider (T072).
/// </summary>
public class MetadataProviderTests
{
    private readonly Mock<ILogger<MetadataProvider>> _mockLogger;
    private readonly Mock<IMetadataFetcher> _mockMetadataFetcher;
    private readonly MetadataProvider _metadataProvider;

    public MetadataProviderTests()
    {
        _mockLogger = new Mock<ILogger<MetadataProvider>>();
        _mockMetadataFetcher = new Mock<IMetadataFetcher>();
        _metadataProvider = new MetadataProvider(_mockLogger.Object, _mockMetadataFetcher.Object);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetadataProvider(null!, _mockMetadataFetcher.Object));
    }

    [Fact]
    public void Constructor_Should_Throw_When_MetadataFetcher_Is_Null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MetadataProvider(_mockLogger.Object, null!));
    }

    [Fact]
    public async Task GetMetadataAsync_Should_Throw_When_InfoHash_Is_Empty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _metadataProvider.GetMetadataAsync("", "Test Title", ContentType.Movie));
    }

    [Fact]
    public async Task GetMetadataAsync_Should_Throw_When_Title_Is_Empty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _metadataProvider.GetMetadataAsync("abc123", "", ContentType.Movie));
    }

    [Fact]
    public async Task GetMetadataAsync_Should_Return_Metadata_For_Valid_Input()
    {
        // Arrange
        var expectedMetadata = new MediaMetadata
        {
            Title = "The Matrix",
            Year = 1999,
            Source = MetadataSource.TMDB,
            MatchConfidence = 0.95
        };

        _mockMetadataFetcher
            .Setup(x => x.FetchMetadataAsync(It.IsAny<SearchResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetadata);

        // Act
        var result = await _metadataProvider.GetMetadataAsync("abc123", "The Matrix (1999)", ContentType.Movie);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("The Matrix");
        result.Year.Should().Be(1999);
        result.Source.Should().Be(MetadataSource.TMDB);
    }

    [Fact]
    public async Task GetMetadataAsync_Should_Return_Null_When_Fetcher_Throws()
    {
        // Arrange
        _mockMetadataFetcher
            .Setup(x => x.FetchMetadataAsync(It.IsAny<SearchResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));

        // Act
        var result = await _metadataProvider.GetMetadataAsync("abc123", "Test Title", ContentType.Movie);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ConvertToJellyfinMetadata_Should_Throw_When_Metadata_Is_Null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _metadataProvider.ConvertToJellyfinMetadata(null!));
    }

    [Fact]
    public void ConvertToJellyfinMetadata_Should_Map_All_Fields()
    {
        // Arrange
        var metadata = new MediaMetadata
        {
            Title = "The Matrix",
            OriginalTitle = "The Matrix",
            Year = 1999,
            Overview = "A computer hacker learns...",
            PosterUrl = "https://example.com/poster.jpg",
            BackdropUrl = "https://example.com/backdrop.jpg",
            TmdbId = 603,
            ImdbId = "tt0133093",
            ContentRating = "R",
            Rating = 8.7,
            Genres = new List<string> { "Action", "Sci-Fi" },
            Cast = new List<string> { "Keanu Reeves", "Laurence Fishburne" },
            Directors = new List<string> { "Lana Wachowski", "Lilly Wachowski" },
            RuntimeMinutes = 136
        };

        // Act
        var result = _metadataProvider.ConvertToJellyfinMetadata(metadata);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("The Matrix");
        result.Year.Should().Be(1999);
        result.Overview.Should().Be("A computer hacker learns...");
        result.TmdbId.Should().Be(603);
        result.ImdbId.Should().Be("tt0133093");
        result.Rating.Should().Be(8.7);
        result.Genres.Should().Contain("Action");
        result.Cast.Should().Contain("Keanu Reeves");
        result.Directors.Should().Contain("Lana Wachowski");
        result.RuntimeMinutes.Should().Be(136);
    }

    [Fact]
    public void HasMetadata_Should_Return_True_For_Title_With_Year()
    {
        // Act
        var result = _metadataProvider.HasMetadata("The Matrix (1999)", ContentType.Movie);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasMetadata_Should_Return_True_For_Title_With_Episode()
    {
        // Act
        var result = _metadataProvider.HasMetadata("Breaking Bad S01E01", ContentType.TVShow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasMetadata_Should_Return_False_For_Empty_Title()
    {
        // Act
        var result = _metadataProvider.HasMetadata("", ContentType.Movie);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasMetadata_Should_Return_False_For_Title_Without_Indicators()
    {
        // Act
        var result = _metadataProvider.HasMetadata("Random Title", ContentType.Movie);

        // Assert
        result.Should().BeFalse();
    }
}

