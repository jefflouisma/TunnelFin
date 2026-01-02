using FluentAssertions;
using TunnelFin.Configuration;
using Xunit;

namespace TunnelFin.Tests.Configuration;

/// <summary>
/// Tests for AnonymitySettings (T084).
/// Tests hop count validation (1-3), defaults per FR-003, FR-006.
/// </summary>
public class AnonymitySettingsTests
{
    [Fact]
    public void Constructor_Should_Set_Default_Values()
    {
        // Act
        var settings = new AnonymitySettings();

        // Assert
        settings.DefaultHopCount.Should().Be(3); // FR-006: Default 3 hops for maximum privacy
        settings.MinHopCount.Should().Be(1);
        settings.MaxHopCount.Should().Be(3);
        settings.EnableBandwidthContribution.Should().BeTrue(); // FR-005: Default enabled
        settings.AllowNonAnonymousFallback.Should().BeFalse(); // FR-040: Default disabled
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SetHopCount_Should_Accept_Valid_Values(int hopCount)
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        settings.SetHopCount(hopCount);

        // Assert
        settings.DefaultHopCount.Should().Be(hopCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(10)]
    public void SetHopCount_Should_Reject_Invalid_Values(int hopCount)
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        var act = () => settings.SetHopCount(hopCount);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*must be between 1 and 3*");
    }

    [Fact]
    public void Validate_Should_Return_True_For_Valid_Settings()
    {
        // Arrange
        var settings = new AnonymitySettings
        {
            DefaultHopCount = 2,
            MinHopCount = 1,
            MaxHopCount = 3
        };

        // Act
        var isValid = settings.Validate(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_Should_Return_False_When_DefaultHopCount_Below_Min()
    {
        // Arrange
        var settings = new AnonymitySettings
        {
            DefaultHopCount = 0,
            MinHopCount = 1,
            MaxHopCount = 3
        };

        // Act
        var isValid = settings.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("DefaultHopCount"));
    }

    [Fact]
    public void Validate_Should_Return_False_When_DefaultHopCount_Above_Max()
    {
        // Arrange
        var settings = new AnonymitySettings
        {
            DefaultHopCount = 4,
            MinHopCount = 1,
            MaxHopCount = 3
        };

        // Act
        var isValid = settings.Validate(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("DefaultHopCount"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableBandwidthContribution_Should_Be_Configurable(bool enabled)
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        settings.EnableBandwidthContribution = enabled;

        // Assert
        settings.EnableBandwidthContribution.Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowNonAnonymousFallback_Should_Be_Configurable(bool allowed)
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        settings.AllowNonAnonymousFallback = allowed;

        // Assert
        settings.AllowNonAnonymousFallback.Should().Be(allowed);
    }

    [Fact]
    public void GetEffectiveHopCount_Should_Return_DefaultHopCount()
    {
        // Arrange
        var settings = new AnonymitySettings { DefaultHopCount = 2 };

        // Act
        var hopCount = settings.GetEffectiveHopCount();

        // Assert
        hopCount.Should().Be(2);
    }
}

