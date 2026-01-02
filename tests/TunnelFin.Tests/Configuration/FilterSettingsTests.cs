using FluentAssertions;
using TunnelFin.Configuration;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Configuration;

/// <summary>
/// Unit tests for FilterSettings class.
/// Tests filter configuration and validation.
/// </summary>
public class FilterSettingsTests
{
    [Fact]
    public void Constructor_Should_Set_Default_Values()
    {
        // Arrange & Act
        var settings = new FilterSettings();

        // Assert
        settings.EnableAutoFiltering.Should().BeTrue();
        settings.DefaultSortAttribute.Should().Be(SortAttribute.Seeders);
        settings.DefaultSortDirection.Should().Be(SortDirection.Descending);
        settings.MinSeeders.Should().Be(1);
        settings.MinFileSize.Should().Be(104857600L); // 100MB
        settings.MaxFileSize.Should().Be(0);
        settings.FilterLowQuality.Should().BeTrue();
        settings.BlockedQualities.Should().Contain("CAM");
        settings.FilterSuspiciousSeeders.Should().BeTrue();
        settings.MaxSuspiciousSeeders.Should().Be(10000);
        settings.PreferVerifiedUploaders.Should().BeTrue();
        settings.EnableMetadataEnrichment.Should().BeTrue();
        settings.RequireMetadataMatch.Should().BeFalse();
        settings.MinRelevanceScore.Should().Be(0.0);
        settings.DeduplicateResults.Should().BeTrue();
        settings.GroupByQuality.Should().BeFalse();
        settings.MaxResultsPerQualityGroup.Should().Be(5);
    }

    [Fact]
    public void IsValid_Should_Return_True_For_Valid_Settings()
    {
        // Arrange
        var settings = new FilterSettings
        {
            MinSeeders = 5,
            MinFileSize = 100000000,
            MaxFileSize = 5000000000,
            MaxSuspiciousSeeders = 5000,
            MinRelevanceScore = 50.0,
            MaxResultsPerQualityGroup = 10
        };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_Should_Fail_When_MinSeeders_Is_Negative()
    {
        // Arrange
        var settings = new FilterSettings { MinSeeders = -1 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MinSeeders cannot be negative");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MinFileSize_Is_Negative()
    {
        // Arrange
        var settings = new FilterSettings { MinFileSize = -1 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MinFileSize cannot be negative");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MaxFileSize_Is_Negative()
    {
        // Arrange
        var settings = new FilterSettings { MaxFileSize = -1 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MaxFileSize cannot be negative");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MaxFileSize_Less_Than_MinFileSize()
    {
        // Arrange
        var settings = new FilterSettings
        {
            MinFileSize = 1000000000,
            MaxFileSize = 500000000
        };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MaxFileSize must be greater than MinFileSize");
    }

    [Fact]
    public void IsValid_Should_Pass_When_MaxFileSize_Is_Zero()
    {
        // Arrange
        var settings = new FilterSettings
        {
            MinFileSize = 1000000000,
            MaxFileSize = 0 // 0 means unlimited
        };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void IsValid_Should_Fail_When_MaxSuspiciousSeeders_Is_Negative()
    {
        // Arrange
        var settings = new FilterSettings { MaxSuspiciousSeeders = -1 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MaxSuspiciousSeeders cannot be negative");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MinRelevanceScore_Is_Negative()
    {
        // Arrange
        var settings = new FilterSettings { MinRelevanceScore = -1.0 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MinRelevanceScore must be between 0 and 100");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MinRelevanceScore_Is_Greater_Than_100()
    {
        // Arrange
        var settings = new FilterSettings { MinRelevanceScore = 101.0 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MinRelevanceScore must be between 0 and 100");
    }

    [Fact]
    public void IsValid_Should_Fail_When_MaxResultsPerQualityGroup_Is_Less_Than_1()
    {
        // Arrange
        var settings = new FilterSettings { MaxResultsPerQualityGroup = 0 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain("MaxResultsPerQualityGroup must be at least 1");
    }

    [Fact]
    public void IsValid_Should_Return_Multiple_Errors()
    {
        // Arrange
        var settings = new FilterSettings
        {
            MinSeeders = -1,
            MinFileSize = -1,
            MaxFileSize = -1,
            MaxSuspiciousSeeders = -1,
            MinRelevanceScore = -1,
            MaxResultsPerQualityGroup = 0
        };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().HaveCount(6);
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        // Arrange
        var settings = new FilterSettings();

        // Act
        settings.EnableAutoFiltering = false;
        settings.DefaultFilterProfileId = Guid.NewGuid();
        settings.DefaultSortAttribute = SortAttribute.Size;
        settings.DefaultSortDirection = SortDirection.Ascending;
        settings.MinSeeders = 10;
        settings.MinFileSize = 500000000;
        settings.MaxFileSize = 10000000000;
        settings.FilterLowQuality = false;
        settings.BlockedQualities = new List<string> { "CUSTOM" };
        settings.FilterSuspiciousSeeders = false;
        settings.MaxSuspiciousSeeders = 20000;
        settings.PreferVerifiedUploaders = false;
        settings.EnableMetadataEnrichment = false;
        settings.RequireMetadataMatch = true;
        settings.MinRelevanceScore = 75.0;
        settings.DeduplicateResults = false;
        settings.GroupByQuality = true;
        settings.MaxResultsPerQualityGroup = 20;

        // Assert
        settings.EnableAutoFiltering.Should().BeFalse();
        settings.DefaultFilterProfileId.Should().NotBeNull();
        settings.DefaultSortAttribute.Should().Be(SortAttribute.Size);
        settings.DefaultSortDirection.Should().Be(SortDirection.Ascending);
        settings.MinSeeders.Should().Be(10);
        settings.MinFileSize.Should().Be(500000000);
        settings.MaxFileSize.Should().Be(10000000000);
        settings.FilterLowQuality.Should().BeFalse();
        settings.BlockedQualities.Should().Contain("CUSTOM");
        settings.FilterSuspiciousSeeders.Should().BeFalse();
        settings.MaxSuspiciousSeeders.Should().Be(20000);
        settings.PreferVerifiedUploaders.Should().BeFalse();
        settings.EnableMetadataEnrichment.Should().BeFalse();
        settings.RequireMetadataMatch.Should().BeTrue();
        settings.MinRelevanceScore.Should().Be(75.0);
        settings.DeduplicateResults.Should().BeFalse();
        settings.GroupByQuality.Should().BeTrue();
        settings.MaxResultsPerQualityGroup.Should().Be(20);
    }
}

