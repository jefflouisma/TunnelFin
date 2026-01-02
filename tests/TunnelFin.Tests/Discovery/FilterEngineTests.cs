using FluentAssertions;
using TunnelFin.Discovery;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for FilterEngine (T073).
/// Tests Required, Preferred, Excluded, Include filter types per FR-019, FR-020, FR-021, FR-022.
/// </summary>
public class FilterEngineTests
{
    private readonly FilterEngine _filterEngine;

    public FilterEngineTests()
    {
        _filterEngine = new FilterEngine();
    }

    [Fact]
    public void ApplyFilters_Should_Return_All_Results_When_No_Filters()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie 1080p", Quality = "1080p" },
            new SearchResult { Title = "Movie 720p", Quality = "720p" }
        };

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyFilters_Should_Exclude_Results_Matching_Excluded_Filter()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie 1080p", Quality = "1080p" },
            new SearchResult { Title = "Movie 720p", Quality = "720p" },
            new SearchResult { Title = "Movie 480p", Quality = "480p" }
        };

        _filterEngine.AddExcludedFilter("quality", "720p");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().NotContain(r => r.Quality == "720p");
    }

    [Fact]
    public void ApplyFilters_Should_Only_Include_Results_Matching_Required_Filter()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie 1080p BluRay", Quality = "1080p" },
            new SearchResult { Title = "Movie 1080p WEBRip", Quality = "1080p" },
            new SearchResult { Title = "Movie 720p BluRay", Quality = "720p" }
        };

        _filterEngine.AddRequiredFilter("quality", "1080p");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().OnlyContain(r => r.Quality == "1080p");
    }

    [Fact]
    public void ApplyFilters_Should_Prioritize_Results_Matching_Preferred_Filter()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie WEBRip", Quality = "1080p" },
            new SearchResult { Title = "Movie BluRay", Quality = "1080p" }
        };

        _filterEngine.AddPreferredFilter("title", "BluRay");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
        filtered[0].Title.Should().Contain("BluRay"); // Preferred should be first
    }

    [Fact]
    public void ApplyFilters_Should_Support_Keyword_Matching()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie.1080p.x264.AAC" },
            new SearchResult { Title = "Movie.1080p.x265.AAC" },
            new SearchResult { Title = "Movie.720p.x264.AAC" }
        };

        _filterEngine.AddIncludeFilter("title", "x265");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].Title.Should().Contain("x265");
    }

    [Fact]
    public void ApplyFilters_Should_Support_Regex_Matching()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie.S01E01.1080p" },
            new SearchResult { Title = "Movie.S01E02.1080p" },
            new SearchResult { Title = "Movie.2023.1080p" }
        };

        _filterEngine.AddRegexFilter(@"S\d{2}E\d{2}");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
        filtered.Should().OnlyContain(r => r.Title.Contains("S01E"));
    }

    [Fact]
    public void ApplyFilters_Should_Extract_Resolution_From_Title()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie.1080p.BluRay" },
            new SearchResult { Title = "Movie.720p.WEBRip" }
        };

        _filterEngine.AddRequiredFilter("resolution", "1080p");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].Title.Should().Contain("1080p");
    }

    [Fact]
    public void ApplyFilters_Should_Support_Multiple_Filters()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie.1080p.BluRay.x265", Seeders = 100 },
            new SearchResult { Title = "Movie.1080p.WEBRip.x264", Seeders = 50 },
            new SearchResult { Title = "Movie.720p.BluRay.x265", Seeders = 200 }
        };

        _filterEngine.AddRequiredFilter("quality", "1080p");
        _filterEngine.AddPreferredFilter("title", "BluRay");
        _filterEngine.AddExcludedFilter("codec", "x264");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].Title.Should().Contain("1080p");
        filtered[0].Title.Should().Contain("BluRay");
        filtered[0].Title.Should().Contain("x265");
    }

    [Fact]
    public void ApplyFilters_Should_Support_Conditional_Filtering()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie.1080p.BluRay", Seeders = 100 },
            new SearchResult { Title = "Movie.1080p.WEBRip", Seeders = 50 },
            new SearchResult { Title = "Movie.720p.BluRay", Seeders = 200 }
        };

        // Conditional: exclude 720p if >1 results at 1080p
        _filterEngine.AddConditionalFilter("exclude 720p if count(1080p) > 1");

        // Act
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
        // Quality is parsed from title, so check title contains 1080p
        filtered.Should().OnlyContain(r => r.Title.Contains("1080p"));
    }

    [Fact]
    public void ClearFilters_Should_Remove_All_Filters()
    {
        // Arrange
        _filterEngine.AddRequiredFilter("quality", "1080p");
        _filterEngine.AddExcludedFilter("quality", "720p");

        var results = new List<SearchResult>
        {
            new SearchResult { Title = "Movie 1080p", Quality = "1080p" },
            new SearchResult { Title = "Movie 720p", Quality = "720p" }
        };

        // Act
        _filterEngine.ClearFilters();
        var filtered = _filterEngine.ApplyFilters(results);

        // Assert
        filtered.Should().HaveCount(2);
    }
}


