using FluentAssertions;
using TunnelFin.Discovery;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Discovery;

/// <summary>
/// Tests for FilterProfileManager (T082).
/// Tests separate profiles for Movies, TV Shows, Anime per FR-024.
/// </summary>
public class FilterProfileManagerTests
{
    private readonly FilterProfileManager _manager;

    public FilterProfileManagerTests()
    {
        _manager = new FilterProfileManager();
    }

    [Fact]
    public void CreateProfile_Should_Create_New_Profile()
    {
        // Arrange
        var profile = new FilterProfile
        {
            Name = "High Quality Movies",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            AllowedQualities = new List<string> { "1080p", "2160p" }
        };

        // Act
        var created = _manager.CreateProfile(profile);

        // Assert
        created.Should().NotBeNull();
        created.ProfileId.Should().NotBeEmpty();
        created.Name.Should().Be("High Quality Movies");
    }

    [Fact]
    public void GetProfile_Should_Return_Profile_By_Id()
    {
        // Arrange
        var profile = new FilterProfile { Name = "Test Profile" };
        var created = _manager.CreateProfile(profile);

        // Act
        var retrieved = _manager.GetProfile(created.ProfileId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProfileId.Should().Be(created.ProfileId);
        retrieved.Name.Should().Be("Test Profile");
    }

    [Fact]
    public void GetProfile_Should_Return_Null_For_NonExistent_Id()
    {
        // Act
        var retrieved = _manager.GetProfile(Guid.NewGuid());

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void GetAllProfiles_Should_Return_All_Profiles()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile { Name = "Profile 1" });
        _manager.CreateProfile(new FilterProfile { Name = "Profile 2" });
        _manager.CreateProfile(new FilterProfile { Name = "Profile 3" });

        // Act
        var profiles = _manager.GetAllProfiles();

        // Assert
        profiles.Should().HaveCount(3);
    }

    [Fact]
    public void GetProfilesByContentType_Should_Return_Matching_Profiles()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Movie Profile",
            ContentTypes = new List<ContentType> { ContentType.Movie }
        });
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Anime Profile",
            ContentTypes = new List<ContentType> { ContentType.Anime }
        });
        _manager.CreateProfile(new FilterProfile
        {
            Name = "TV Profile",
            ContentTypes = new List<ContentType> { ContentType.TVShow }
        });

        // Act
        var movieProfiles = _manager.GetProfilesByContentType(ContentType.Movie);

        // Assert
        movieProfiles.Should().HaveCount(1);
        movieProfiles[0].Name.Should().Be("Movie Profile");
    }

    [Fact]
    public void UpdateProfile_Should_Update_Existing_Profile()
    {
        // Arrange
        var profile = _manager.CreateProfile(new FilterProfile
        {
            Name = "Original Name",
            MinSeeders = 10
        });

        // Act
        profile.Name = "Updated Name";
        profile.MinSeeders = 20;
        var updated = _manager.UpdateProfile(profile);

        // Assert
        updated.Should().BeTrue();
        var retrieved = _manager.GetProfile(profile.ProfileId);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Updated Name");
        retrieved.MinSeeders.Should().Be(20);
    }

    [Fact]
    public void UpdateProfile_Should_Return_False_For_NonExistent_Profile()
    {
        // Arrange
        var profile = new FilterProfile
        {
            ProfileId = Guid.NewGuid(),
            Name = "Non-existent"
        };

        // Act
        var updated = _manager.UpdateProfile(profile);

        // Assert
        updated.Should().BeFalse();
    }

    [Fact]
    public void DeleteProfile_Should_Remove_Profile()
    {
        // Arrange
        var profile = _manager.CreateProfile(new FilterProfile { Name = "To Delete" });

        // Act
        var deleted = _manager.DeleteProfile(profile.ProfileId);

        // Assert
        deleted.Should().BeTrue();
        _manager.GetProfile(profile.ProfileId).Should().BeNull();
    }

    [Fact]
    public void DeleteProfile_Should_Return_False_For_NonExistent_Profile()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var deleted = _manager.DeleteProfile(nonExistentId);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public void GetDefaultProfile_Should_Return_First_Enabled_Profile_By_Priority()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Low Priority",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = true,
            Priority = 10
        });
        _manager.CreateProfile(new FilterProfile
        {
            Name = "High Priority",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = true,
            Priority = 1
        });
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Medium Priority",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = true,
            Priority = 5
        });

        // Act
        var defaultProfile = _manager.GetDefaultProfile(ContentType.Movie);

        // Assert
        defaultProfile.Should().NotBeNull();
        defaultProfile!.Name.Should().Be("High Priority");
    }

    [Fact]
    public void GetDefaultProfile_Should_Ignore_Disabled_Profiles()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Disabled High Priority",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = false,
            Priority = 1
        });
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Enabled Low Priority",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = true,
            Priority = 10
        });

        // Act
        var defaultProfile = _manager.GetDefaultProfile(ContentType.Movie);

        // Assert
        defaultProfile.Should().NotBeNull();
        defaultProfile!.Name.Should().Be("Enabled Low Priority");
    }

    [Fact]
    public void GetDefaultProfile_Should_Return_Null_When_No_Enabled_Profiles()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Disabled",
            ContentTypes = new List<ContentType> { ContentType.Movie },
            IsEnabled = false
        });

        // Act
        var defaultProfile = _manager.GetDefaultProfile(ContentType.Movie);

        // Assert
        defaultProfile.Should().BeNull();
    }

    [Fact]
    public void ClearAll_Should_Remove_All_Profiles()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile { Name = "Profile 1" });
        _manager.CreateProfile(new FilterProfile { Name = "Profile 2" });
        _manager.CreateProfile(new FilterProfile { Name = "Profile 3" });

        // Act
        _manager.ClearAll();

        // Assert
        _manager.GetAllProfiles().Should().BeEmpty();
    }

    [Fact]
    public void CreateProfile_Should_Generate_ProfileId_When_Empty()
    {
        // Arrange
        var profile = new FilterProfile
        {
            ProfileId = Guid.Empty,
            Name = "Auto ID"
        };

        // Act
        var created = _manager.CreateProfile(profile);

        // Assert
        created.ProfileId.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateProfile_Should_Preserve_Existing_ProfileId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var profile = new FilterProfile
        {
            ProfileId = existingId,
            Name = "Existing ID"
        };

        // Act
        var created = _manager.CreateProfile(profile);

        // Assert
        created.ProfileId.Should().Be(existingId);
    }

    [Fact]
    public void GetProfilesByContentType_Should_Return_Profiles_With_Multiple_ContentTypes()
    {
        // Arrange
        _manager.CreateProfile(new FilterProfile
        {
            Name = "Multi-Type Profile",
            ContentTypes = new List<ContentType> { ContentType.Movie, ContentType.TVShow }
        });

        // Act
        var movieProfiles = _manager.GetProfilesByContentType(ContentType.Movie);
        var tvProfiles = _manager.GetProfilesByContentType(ContentType.TVShow);

        // Assert
        movieProfiles.Should().HaveCount(1);
        tvProfiles.Should().HaveCount(1);
        movieProfiles[0].Name.Should().Be("Multi-Type Profile");
        tvProfiles[0].Name.Should().Be("Multi-Type Profile");
    }
}

