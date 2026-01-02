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
        var profile = new FilterProfile { Name = "Original Name" };
        var created = _manager.CreateProfile(profile);

        // Act
        created.Name = "Updated Name";
        var updated = _manager.UpdateProfile(created);

        // Assert
        updated.Should().BeTrue();
        var retrieved = _manager.GetProfile(created.ProfileId);
        retrieved!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public void UpdateProfile_Should_Return_False_For_NonExistent_Profile()
    {
        // Arrange
        var profile = new FilterProfile { ProfileId = Guid.NewGuid(), Name = "Test" };

        // Act
        var updated = _manager.UpdateProfile(profile);

        // Assert
        updated.Should().BeFalse();
    }

    [Fact]
    public void DeleteProfile_Should_Remove_Profile()
    {
        // Arrange
        var profile = new FilterProfile { Name = "To Delete" };
        var created = _manager.CreateProfile(profile);

        // Act
        var deleted = _manager.DeleteProfile(created.ProfileId);

        // Assert
        deleted.Should().BeTrue();
        _manager.GetProfile(created.ProfileId).Should().BeNull();
    }

    [Fact]
    public void DeleteProfile_Should_Return_False_For_NonExistent_Profile()
    {
        // Act
        var deleted = _manager.DeleteProfile(Guid.NewGuid());

        // Assert
        deleted.Should().BeFalse();
    }
}

