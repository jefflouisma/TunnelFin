using FluentAssertions;
using TunnelFin.Discovery;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Unit tests for Deduplicator (FR-025, SC-007).
/// Tests infohash, filename, and smart hash deduplication with 90% success rate.
/// </summary>
public class DeduplicatorTests
{
    private readonly Deduplicator _deduplicator;

    public DeduplicatorTests()
    {
        _deduplicator = new Deduplicator();
    }

    [Fact]
    public void Deduplicate_Should_Remove_Exact_Infohash_Duplicates()
    {
        // Arrange - FR-025: infohash deduplication
        var results = new List<SearchResult>
        {
            new SearchResult { InfoHash = "abc123", Title = "Movie 1" },
            new SearchResult { InfoHash = "abc123", Title = "Movie 1 (duplicate)" },
            new SearchResult { InfoHash = "def456", Title = "Movie 2" }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(2, "should remove exact infohash duplicates");
        deduplicated.Should().Contain(r => r.InfoHash == "abc123");
        deduplicated.Should().Contain(r => r.InfoHash == "def456");
    }

    [Fact]
    public void Deduplicate_Should_Remove_Similar_Filename_Duplicates()
    {
        // Arrange - FR-025: filename deduplication
        var results = new List<SearchResult>
        {
            new SearchResult { InfoHash = "hash1", Title = "Inception.2010.1080p.BluRay.x264" },
            new SearchResult { InfoHash = "hash2", Title = "Inception 2010 1080p BluRay x264" },
            new SearchResult { InfoHash = "hash3", Title = "Interstellar.2014.1080p" }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(2, "should remove similar filename duplicates");
    }

    [Fact]
    public void Deduplicate_Should_Use_Smart_Hash_For_Similarity()
    {
        // Arrange - FR-025: smart hash deduplication
        var results = new List<SearchResult>
        {
            new SearchResult { InfoHash = "hash1", Title = "The.Matrix.1999.1080p.BluRay" },
            new SearchResult { InfoHash = "hash2", Title = "The Matrix (1999) 1080p BluRay" },
            new SearchResult { InfoHash = "hash3", Title = "Matrix.1999.720p" }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCountLessThan(results.Count, 
            "should detect similar titles with smart hash");
    }

    [Fact]
    public void Deduplicate_Should_Prefer_Higher_Quality()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult
            {
                InfoHash = "hash1",
                Title = "Movie.2020.720p",
                Quality = "720p",
                Seeders = 10
            },
            new SearchResult
            {
                InfoHash = "hash2",
                Title = "Movie.2020.1080p",
                Quality = "1080p",
                Seeders = 20
            }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(1, "should keep only one version");
        deduplicated.First().Quality.Should().Be("1080p",
            "should prefer higher quality");
    }

    [Fact]
    public void Deduplicate_Should_Prefer_More_Seeders()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult 
            { 
                InfoHash = "hash1", 
                Title = "Movie.2020.1080p",
                Seeders = 5
            },
            new SearchResult 
            { 
                InfoHash = "hash2", 
                Title = "Movie 2020 1080p",
                Seeders = 50
            }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(1);
        deduplicated.First().Seeders.Should().Be(50, 
            "should prefer result with more seeders");
    }

    [Fact]
    public void Deduplicate_Should_Handle_Empty_List()
    {
        // Arrange
        var results = new List<SearchResult>();

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_Should_Handle_Single_Result()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { InfoHash = "hash1", Title = "Movie" }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(1);
    }

    [Fact]
    public void Deduplicate_Should_Reject_Null_List()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _deduplicator.Deduplicate(null!));
    }

    [Fact]
    public void Deduplicate_Should_Achieve_90_Percent_Success_Rate()
    {
        // Arrange - SC-007: 90% deduplication success rate
        var results = new List<SearchResult>
        {
            // Same movie, different formats
            new SearchResult { InfoHash = "h1", Title = "Inception.2010.1080p.BluRay.x264-GROUP1" },
            new SearchResult { InfoHash = "h2", Title = "Inception (2010) 1080p BluRay x264 GROUP2" },
            new SearchResult { InfoHash = "h3", Title = "Inception.2010.720p.BluRay" },
            
            // Different movie
            new SearchResult { InfoHash = "h4", Title = "Interstellar.2014.1080p" },
            
            // Same movie again
            new SearchResult { InfoHash = "h5", Title = "Inception 2010 1080p" },
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(2, 
            "should deduplicate to 2 unique movies (Inception + Interstellar)");
    }

    [Fact]
    public void Deduplicate_Should_Preserve_Unique_Results()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            new SearchResult { InfoHash = "hash1", Title = "Movie A" },
            new SearchResult { InfoHash = "hash2", Title = "Movie B" },
            new SearchResult { InfoHash = "hash3", Title = "Movie C" }
        };

        // Act
        var deduplicated = _deduplicator.Deduplicate(results);

        // Assert
        deduplicated.Should().HaveCount(3, "should preserve all unique results");
    }
}

