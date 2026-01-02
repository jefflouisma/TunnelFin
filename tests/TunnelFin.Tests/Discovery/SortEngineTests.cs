using FluentAssertions;
using TunnelFin.Discovery;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for SortEngine (T074).
/// Tests multi-criteria sorting per FR-023, <1s for 100+ results per SC-005.
/// </summary>
public class SortEngineTests
{
    private readonly SortEngine _sortEngine;

    public SortEngineTests()
    {
        _sortEngine = new SortEngine();
    }

    [Fact]
    public void Sort_Should_Sort_By_Seeders_Descending_By_Default()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie A", Seeders = 50 },
            new SearchResult { Title = "Movie B", Seeders = 200 },
            new SearchResult { Title = "Movie C", Seeders = 100 }
        };

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        sorted[0].Seeders.Should().Be(200);
        sorted[1].Seeders.Should().Be(100);
        sorted[2].Seeders.Should().Be(50);
    }

    [Fact]
    public void Sort_Should_Sort_By_Quality_When_Specified()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie 720p", Quality = "720p" },
            new SearchResult { Title = "Movie 2160p", Quality = "2160p" },
            new SearchResult { Title = "Movie 1080p", Quality = "1080p" }
        };

        _sortEngine.SetSortCriteria(SortAttribute.Quality, SortDirection.Descending);

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        sorted[0].Quality.Should().Be("2160p");
        sorted[1].Quality.Should().Be("1080p");
        sorted[2].Quality.Should().Be("720p");
    }

    [Fact]
    public void Sort_Should_Sort_By_Size_When_Specified()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie A", Size = 2_000_000_000 }, // 2GB
            new SearchResult { Title = "Movie B", Size = 5_000_000_000 }, // 5GB
            new SearchResult { Title = "Movie C", Size = 1_000_000_000 }  // 1GB
        };

        _sortEngine.SetSortCriteria(SortAttribute.Size, SortDirection.Ascending);

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        sorted[0].Size.Should().Be(1_000_000_000);
        sorted[1].Size.Should().Be(2_000_000_000);
        sorted[2].Size.Should().Be(5_000_000_000);
    }

    [Fact]
    public void Sort_Should_Support_Multi_Criteria_Sorting()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie A", Quality = "1080p", Seeders = 50 },
            new SearchResult { Title = "Movie B", Quality = "1080p", Seeders = 200 },
            new SearchResult { Title = "Movie C", Quality = "720p", Seeders = 300 }
        };

        _sortEngine.SetSortCriteria(
            new[] { SortAttribute.Quality, SortAttribute.Seeders },
            new[] { SortDirection.Descending, SortDirection.Descending }
        );

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        sorted[0].Quality.Should().Be("1080p");
        sorted[0].Seeders.Should().Be(200); // Higher seeders within 1080p
        sorted[1].Quality.Should().Be("1080p");
        sorted[1].Seeders.Should().Be(50);
        sorted[2].Quality.Should().Be("720p");
    }

    [Fact]
    public void Sort_Should_Complete_Within_1_Second_For_100_Results()
    {
        // Arrange
        var results = new List<SearchResult>();
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            results.Add(new SearchResult
            {
                Title = $"Movie {i}",
                Seeders = random.Next(1, 1000),
                Quality = i % 3 == 0 ? "2160p" : i % 2 == 0 ? "1080p" : "720p",
                Size = (long)random.Next(1_000_000_000, 2_000_000_000) * 5 // Cast to long
            });
        }

        _sortEngine.SetSortCriteria(
            new[] { SortAttribute.Quality, SortAttribute.Seeders, SortAttribute.Size },
            new[] { SortDirection.Descending, SortDirection.Descending, SortDirection.Ascending }
        );

        var startTime = DateTime.UtcNow;

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        duration.Should().BeLessThan(TimeSpan.FromSeconds(1)); // SC-005
        sorted.Should().HaveCount(100);
    }

    [Fact]
    public void Sort_Should_Sort_By_Upload_Date_When_Specified()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie A", UploadedAt = DateTime.UtcNow.AddDays(-5) },
            new SearchResult { Title = "Movie B", UploadedAt = DateTime.UtcNow.AddDays(-1) },
            new SearchResult { Title = "Movie C", UploadedAt = DateTime.UtcNow.AddDays(-10) }
        };

        _sortEngine.SetSortCriteria(SortAttribute.UploadDate, SortDirection.Descending);

        // Act
        var sorted = _sortEngine.Sort(results);

        // Assert
        sorted[0].Title.Should().Be("Movie B"); // Most recent
        sorted[1].Title.Should().Be("Movie A");
        sorted[2].Title.Should().Be("Movie C"); // Oldest
    }
}



