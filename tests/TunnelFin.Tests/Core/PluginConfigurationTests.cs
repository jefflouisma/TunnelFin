using FluentAssertions;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Unit tests for PluginConfiguration validation.
/// </summary>
public class PluginConfigurationTests
{
    [Fact]
    public void PluginConfiguration_Should_Have_Valid_Defaults()
    {
        // Arrange & Act
        var config = new PluginConfiguration();

        // Assert
        config.MaxConcurrentStreams.Should().Be(3);
        config.MaxCacheSize.Should().Be(10737418240L);
        config.MaxConcurrentSearches.Should().Be(5);
        config.DefaultHopCount.Should().Be(3);
        config.MinHopCount.Should().Be(1);
        config.MaxHopCount.Should().Be(3);
        config.EnableBandwidthContribution.Should().BeTrue();
        config.AllowNonAnonymousFallback.Should().BeFalse();
        config.StreamInitializationTimeoutSeconds.Should().Be(60);
        config.CircuitEstablishmentTimeoutSeconds.Should().Be(30);
        config.MinimumBufferSeconds.Should().Be(10);
        config.SearchCacheDurationMinutes.Should().Be(10);
        config.MetadataFailureCacheDurationMinutes.Should().Be(5);
        config.LoggingLevel.Should().Be(LoggingLevel.Minimal);
        config.EnableScheduledCatalogSync.Should().BeFalse();
        config.CatalogSyncIntervalHours.Should().Be(24);
        config.BuiltInIndexers.Should().HaveCount(3);
        config.CustomIndexers.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Validate_Successfully_With_Defaults()
    {
        // Arrange
        var config = new PluginConfiguration();

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void PluginConfiguration_Should_Fail_Validation_With_Invalid_MaxConcurrentStreams(int value)
    {
        // Arrange
        var config = new PluginConfiguration { MaxConcurrentStreams = value };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxConcurrentStreams"));
    }

    [Fact]
    public void PluginConfiguration_Should_Fail_Validation_With_Too_Small_MaxCacheSize()
    {
        // Arrange
        var config = new PluginConfiguration { MaxCacheSize = 536870912L }; // 512MB

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxCacheSize"));
    }

    [Fact]
    public void PluginConfiguration_Should_Fail_Validation_With_Invalid_DefaultHopCount()
    {
        // Arrange
        var config = new PluginConfiguration { DefaultHopCount = 5 };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("DefaultHopCount"));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(301)]
    public void PluginConfiguration_Should_Fail_Validation_With_Invalid_StreamInitializationTimeout(int value)
    {
        // Arrange
        var config = new PluginConfiguration { StreamInitializationTimeoutSeconds = value };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("StreamInitializationTimeoutSeconds"));
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Valid_Configuration()
    {
        // Arrange
        var config = new PluginConfiguration
        {
            MaxConcurrentStreams = 5,
            MaxCacheSize = 21474836480L, // 20GB
            DefaultHopCount = 2,
            StreamInitializationTimeoutSeconds = 120
        };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Initialize_BuiltInIndexers()
    {
        // Arrange & Act
        var config = new PluginConfiguration();

        // Assert
        config.BuiltInIndexers.Should().HaveCount(3);
        config.BuiltInIndexers.Should().Contain(i => i.Name == "1337x");
        config.BuiltInIndexers.Should().Contain(i => i.Name == "Nyaa");
        config.BuiltInIndexers.Should().Contain(i => i.Name == "RARBG");
        config.BuiltInIndexers.Should().OnlyContain(i => i.Enabled);
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Minimum_Valid_MaxConcurrentStreams()
    {
        // Arrange
        var config = new PluginConfiguration { MaxConcurrentStreams = 1 };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Maximum_Valid_MaxConcurrentStreams()
    {
        // Arrange
        var config = new PluginConfiguration { MaxConcurrentStreams = 10 };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Minimum_Valid_StreamInitializationTimeout()
    {
        // Arrange
        var config = new PluginConfiguration { StreamInitializationTimeoutSeconds = 10 };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Maximum_Valid_StreamInitializationTimeout()
    {
        // Arrange
        var config = new PluginConfiguration { StreamInitializationTimeoutSeconds = 300 };

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Accept_Minimum_Valid_MaxCacheSize()
    {
        // Arrange
        var config = new PluginConfiguration { MaxCacheSize = 1073741824L }; // Exactly 1GB

        // Act
        var isValid = config.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfiguration_Should_Allow_Setting_TmdbApiKey()
    {
        // Arrange & Act
        var config = new PluginConfiguration { TmdbApiKey = "test-api-key-123" };

        // Assert
        config.TmdbApiKey.Should().Be("test-api-key-123");
    }

    [Fact]
    public void PluginConfiguration_Should_Allow_Setting_AniListClientId()
    {
        // Arrange & Act
        var config = new PluginConfiguration { AniListClientId = "test-client-id-456" };

        // Assert
        config.AniListClientId.Should().Be("test-client-id-456");
    }
}


