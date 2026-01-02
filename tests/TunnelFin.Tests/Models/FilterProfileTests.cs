using FluentAssertions;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Models;

/// <summary>
/// Unit tests for FilterProfile model.
/// Tests property initialization and filter configuration.
/// </summary>
public class FilterProfileTests
{
    [Fact]
    public void FilterProfile_Should_Initialize_With_Default_Values()
    {
        // Arrange & Act
        var profile = new FilterProfile();

        // Assert
        profile.ProfileId.Should().BeEmpty();
        profile.Name.Should().BeEmpty();
        profile.Description.Should().BeNull();
        profile.IsEnabled.Should().BeTrue();
        profile.ContentTypes.Should().NotBeNull();
        profile.ContentTypes.Should().BeEmpty();
        profile.MinSeeders.Should().BeNull();
        profile.MaxSeeders.Should().BeNull();
        profile.MinSize.Should().BeNull();
        profile.MaxSize.Should().BeNull();
        profile.AllowedQualities.Should().NotBeNull();
        profile.AllowedQualities.Should().BeEmpty();
        profile.BlockedQualities.Should().NotBeNull();
        profile.BlockedQualities.Should().BeEmpty();
        profile.Priority.Should().Be(0);
    }

    [Fact]
    public void FilterProfile_Should_Allow_Setting_All_Properties()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var modifiedAt = DateTime.UtcNow;

        // Act
        var profile = new FilterProfile
        {
            ProfileId = profileId,
            Name = "High Quality Movies",
            Description = "1080p+ movies with good seeders",
            IsEnabled = true,
            ContentTypes = new List<ContentType> { ContentType.Movie },
            MinSeeders = 10,
            MaxSeeders = 10000,
            MinSize = 1073741824L, // 1GB
            MaxSize = 10737418240L, // 10GB
            AllowedQualities = new List<string> { "1080p", "2160p" },
            BlockedQualities = new List<string> { "CAM", "TS" },
            AllowedCodecs = new List<string> { "x265", "HEVC" },
            BlockedCodecs = new List<string> { "xvid" },
            AllowedAudioFormats = new List<string> { "AAC", "DTS" },
            BlockedAudioFormats = new List<string> { "MP3" },
            AllowedLanguages = new List<string> { "English" },
            BlockedLanguages = new List<string> { "Unknown" },
            AllowedReleaseGroups = new List<string> { "RARBG", "YTS" },
            BlockedReleaseGroups = new List<string> { "YIFY" },
            RequiredKeywords = new List<string> { "BluRay" },
            ExcludedKeywords = new List<string> { "HDCAM" },
            Priority = 1,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt
        };

        // Assert
        profile.ProfileId.Should().Be(profileId);
        profile.Name.Should().Be("High Quality Movies");
        profile.Description.Should().Be("1080p+ movies with good seeders");
        profile.IsEnabled.Should().BeTrue();
        profile.ContentTypes.Should().HaveCount(1);
        profile.ContentTypes.Should().Contain(ContentType.Movie);
        profile.MinSeeders.Should().Be(10);
        profile.MaxSeeders.Should().Be(10000);
        profile.MinSize.Should().Be(1073741824L);
        profile.MaxSize.Should().Be(10737418240L);
        profile.AllowedQualities.Should().HaveCount(2);
        profile.AllowedQualities.Should().Contain("1080p");
        profile.AllowedQualities.Should().Contain("2160p");
        profile.BlockedQualities.Should().HaveCount(2);
        profile.BlockedQualities.Should().Contain("CAM");
        profile.BlockedQualities.Should().Contain("TS");
        profile.AllowedCodecs.Should().Contain("x265");
        profile.BlockedCodecs.Should().Contain("xvid");
        profile.AllowedAudioFormats.Should().Contain("AAC");
        profile.BlockedAudioFormats.Should().Contain("MP3");
        profile.AllowedLanguages.Should().Contain("English");
        profile.BlockedLanguages.Should().Contain("Unknown");
        profile.AllowedReleaseGroups.Should().Contain("RARBG");
        profile.BlockedReleaseGroups.Should().Contain("YIFY");
        profile.RequiredKeywords.Should().Contain("BluRay");
        profile.ExcludedKeywords.Should().Contain("HDCAM");
        profile.Priority.Should().Be(1);
        profile.CreatedAt.Should().Be(createdAt);
        profile.ModifiedAt.Should().Be(modifiedAt);
    }

    [Fact]
    public void FilterProfile_Should_Support_Multiple_Content_Types()
    {
        // Arrange & Act
        var profile = new FilterProfile
        {
            ContentTypes = new List<ContentType> { ContentType.Movie, ContentType.TVShow, ContentType.Anime }
        };

        // Assert
        profile.ContentTypes.Should().HaveCount(3);
        profile.ContentTypes.Should().Contain(ContentType.Movie);
        profile.ContentTypes.Should().Contain(ContentType.TVShow);
        profile.ContentTypes.Should().Contain(ContentType.Anime);
    }

    [Fact]
    public void FilterProfile_Should_Support_Anime_Specific_Filters()
    {
        // Arrange & Act
        var profile = new FilterProfile
        {
            Name = "Anime 1080p",
            ContentTypes = new List<ContentType> { ContentType.Anime },
            AllowedQualities = new List<string> { "1080p" },
            AllowedLanguages = new List<string> { "Japanese" },
            RequiredKeywords = new List<string> { "SubsPlease" }
        };

        // Assert
        profile.Name.Should().Be("Anime 1080p");
        profile.ContentTypes.Should().Contain(ContentType.Anime);
        profile.AllowedQualities.Should().Contain("1080p");
        profile.AllowedLanguages.Should().Contain("Japanese");
        profile.RequiredKeywords.Should().Contain("SubsPlease");
    }

    [Fact]
    public void FilterProfile_Should_Support_Disabled_State()
    {
        // Arrange & Act
        var profile = new FilterProfile
        {
            Name = "Disabled Profile",
            IsEnabled = false
        };

        // Assert
        profile.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void FilterProfile_Should_Support_Priority_Ordering()
    {
        // Arrange & Act
        var profile1 = new FilterProfile { Priority = 1 };
        var profile2 = new FilterProfile { Priority = 2 };
        var profile3 = new FilterProfile { Priority = 0 };

        // Assert
        profile3.Priority.Should().BeLessThan(profile1.Priority);
        profile1.Priority.Should().BeLessThan(profile2.Priority);
    }

    [Fact]
    public void FilterProfile_Should_Allow_Empty_Content_Types_For_All_Types()
    {
        // Arrange & Act
        var profile = new FilterProfile
        {
            Name = "Universal Filter",
            ContentTypes = new List<ContentType>() // Empty means applies to all
        };

        // Assert
        profile.ContentTypes.Should().BeEmpty();
    }
}

