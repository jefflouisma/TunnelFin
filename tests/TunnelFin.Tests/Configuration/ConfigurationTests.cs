using FluentAssertions;
using TunnelFin.Configuration;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Configuration;

public class ConfigurationTests
{
    [Fact]
    public void AnonymitySettings_Should_Have_Valid_Defaults()
    {
        // Arrange & Act
        var settings = new AnonymitySettings();

        // Assert
        settings.DefaultHopCount.Should().Be(3, "Default hop count should be 3 for maximum anonymity");
        settings.MinHopCount.Should().Be(1);
        settings.MaxHopCount.Should().Be(3);
        settings.AllowNonAnonymousFallback.Should().BeFalse("Non-anonymous fallback should be disabled by default");
        settings.CircuitEstablishmentTimeoutSeconds.Should().Be(30);
        settings.MaxConcurrentCircuits.Should().Be(10);
        settings.CircuitLifetimeSeconds.Should().Be(600);
        settings.AutoRotateCircuits.Should().BeTrue();
        settings.MinRelayNodes.Should().Be(10);
        settings.PreferHighBandwidthRelays.Should().BeTrue();
        settings.PreferLowLatencyRelays.Should().BeTrue();
        settings.MaxCircuitRttMs.Should().Be(1000);
        settings.EnableCircuitHealthMonitoring.Should().BeTrue();
        settings.CircuitHealthCheckIntervalSeconds.Should().Be(60);
        settings.EnableCircuitLogging.Should().BeTrue();
    }

    [Fact]
    public void AnonymitySettings_Should_Validate_Successfully_With_Defaults()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue("Default settings should be valid");
        errors.Should().BeEmpty();
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_HopCount()
    {
        // Arrange
        var settings = new AnonymitySettings
        {
            DefaultHopCount = 5, // Invalid: exceeds MaxHopCount
            MaxHopCount = 3
        };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("DefaultHopCount"));
    }

    [Fact]
    public void AnonymitySettings_SetHopCount_Should_Update_DefaultHopCount()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        settings.SetHopCount(2);

        // Assert
        settings.DefaultHopCount.Should().Be(2);
    }

    [Fact]
    public void AnonymitySettings_SetHopCount_Should_Throw_When_Below_Minimum()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        var act = () => settings.SetHopCount(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hopCount");
    }

    [Fact]
    public void AnonymitySettings_SetHopCount_Should_Throw_When_Above_Maximum()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        var act = () => settings.SetHopCount(4);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("hopCount");
    }

    [Fact]
    public void AnonymitySettings_GetEffectiveHopCount_Should_Return_DefaultHopCount()
    {
        // Arrange
        var settings = new AnonymitySettings { DefaultHopCount = 2 };

        // Act
        var hopCount = settings.GetEffectiveHopCount();

        // Assert
        hopCount.Should().Be(2);
    }

    [Fact]
    public void AnonymitySettings_Validate_Should_Call_IsValid()
    {
        // Arrange
        var settings = new AnonymitySettings();

        // Act
        var isValid = settings.Validate(out var errors);

        // Assert
        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_MinHopCount()
    {
        // Arrange
        var settings = new AnonymitySettings { MinHopCount = 0 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MinHopCount"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_MaxHopCount()
    {
        // Arrange
        var settings = new AnonymitySettings { MaxHopCount = 5 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxHopCount"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_CircuitEstablishmentTimeout()
    {
        // Arrange
        var settings = new AnonymitySettings { CircuitEstablishmentTimeoutSeconds = 3 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("CircuitEstablishmentTimeoutSeconds"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_MaxConcurrentCircuits()
    {
        // Arrange
        var settings = new AnonymitySettings { MaxConcurrentCircuits = 0 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxConcurrentCircuits"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_CircuitLifetime()
    {
        // Arrange
        var settings = new AnonymitySettings { CircuitLifetimeSeconds = 30 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("CircuitLifetimeSeconds"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_MinRelayNodes()
    {
        // Arrange
        var settings = new AnonymitySettings { MinRelayNodes = 2 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MinRelayNodes"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_MaxCircuitRtt()
    {
        // Arrange
        var settings = new AnonymitySettings { MaxCircuitRttMs = 50 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxCircuitRttMs"));
    }

    [Fact]
    public void AnonymitySettings_Should_Fail_Validation_With_Invalid_CircuitHealthCheckInterval()
    {
        // Arrange
        var settings = new AnonymitySettings { CircuitHealthCheckIntervalSeconds = 5 };

        // Act
        var isValid = settings.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("CircuitHealthCheckIntervalSeconds"));
    }

    [Fact]
    public void ResourceLimits_Should_Have_Valid_Defaults()
    {
        // Arrange & Act
        var limits = new ResourceLimits();

        // Assert
        limits.MaxConcurrentStreams.Should().Be(3);
        limits.MaxCacheSize.Should().Be(10737418240L, "Default cache size should be 10GB");
        limits.MaxSearchResults.Should().Be(100);
        limits.MaxConcurrentIndexerQueries.Should().Be(5);
        limits.SearchTimeoutSeconds.Should().Be(30);
        limits.StreamInitializationTimeoutSeconds.Should().Be(60);
        limits.MetadataDownloadTimeoutSeconds.Should().Be(30);
        limits.MaxBufferPieces.Should().Be(20);
        limits.MinBufferPieces.Should().Be(10);
        limits.MaxDownloadSpeed.Should().Be(0, "Download speed should be unlimited by default");
        limits.MaxUploadSpeed.Should().Be(0, "Upload speed should be unlimited by default");
        limits.MaxPeerConnections.Should().Be(50);
        limits.MaxTotalPeerConnections.Should().Be(200);
        limits.EnableDiskCache.Should().BeTrue();
        limits.DiskCacheSize.Should().Be(1073741824L, "Disk cache should be 1GB");
        limits.AutoCleanupCache.Should().BeTrue();
        limits.CacheMaxAgeDays.Should().Be(7);
    }

    [Fact]
    public void ResourceLimits_Should_Validate_Successfully_With_Defaults()
    {
        // Arrange
        var limits = new ResourceLimits();

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeTrue("Default limits should be valid");
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_ConcurrentStreams()
    {
        // Arrange
        var limits = new ResourceLimits
        {
            MaxConcurrentStreams = 15 // Invalid: exceeds maximum of 10
        };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxConcurrentStreams"));
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_With_MinBuffer_Greater_Than_MaxBuffer()
    {
        // Arrange
        var limits = new ResourceLimits
        {
            MinBufferPieces = 25,
            MaxBufferPieces = 20
        };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MinBufferPieces"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MaxConcurrentStreams(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MaxConcurrentStreams = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxConcurrentStreams"));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1001)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MaxSearchResults(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MaxSearchResults = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxSearchResults"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MaxConcurrentIndexerQueries(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MaxConcurrentIndexerQueries = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxConcurrentIndexerQueries"));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_SearchTimeoutSeconds(int value)
    {
        // Arrange
        var limits = new ResourceLimits { SearchTimeoutSeconds = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("SearchTimeoutSeconds"));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(601)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_StreamInitializationTimeoutSeconds(int value)
    {
        // Arrange
        var limits = new ResourceLimits { StreamInitializationTimeoutSeconds = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("StreamInitializationTimeoutSeconds"));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MetadataDownloadTimeoutSeconds(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MetadataDownloadTimeoutSeconds = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MetadataDownloadTimeoutSeconds"));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(101)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MaxBufferPieces(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MaxBufferPieces = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxBufferPieces"));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(501)]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_MaxPeerConnections(int value)
    {
        // Arrange
        var limits = new ResourceLimits { MaxPeerConnections = value };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxPeerConnections"));
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_When_MaxTotalPeerConnections_Less_Than_MaxPeerConnections()
    {
        // Arrange
        var limits = new ResourceLimits
        {
            MaxPeerConnections = 100,
            MaxTotalPeerConnections = 50
        };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxTotalPeerConnections"));
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_With_Too_Small_MaxCacheSize()
    {
        // Arrange
        var limits = new ResourceLimits { MaxCacheSize = 1073741823L }; // 1 byte less than 1GB

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MaxCacheSize"));
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_With_Too_Small_DiskCacheSize()
    {
        // Arrange
        var limits = new ResourceLimits { DiskCacheSize = 104857599L }; // 1 byte less than 100MB

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("DiskCacheSize"));
    }

    [Fact]
    public void ResourceLimits_Should_Fail_Validation_With_Invalid_CacheMaxAgeDays()
    {
        // Arrange
        var limits = new ResourceLimits { CacheMaxAgeDays = 0 };

        // Act
        var isValid = limits.IsValid(out var errors);

        // Assert
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("CacheMaxAgeDays"));
    }

    [Fact]
    public void FilterSettings_Should_Have_Valid_Defaults()
    {
        // Arrange & Act
        var settings = new FilterSettings();

        // Assert
        settings.EnableAutoFiltering.Should().BeTrue();
        settings.DefaultFilterProfileId.Should().BeNull();
        settings.DefaultSortAttribute.Should().Be(SortAttribute.Seeders);
        settings.DefaultSortDirection.Should().Be(SortDirection.Descending);
        settings.MinSeeders.Should().Be(1);
        settings.MinFileSize.Should().Be(104857600L, "Minimum file size should be 100MB");
        settings.MaxFileSize.Should().Be(0, "Maximum file size should be unlimited");
        settings.FilterLowQuality.Should().BeTrue();
        settings.BlockedQualities.Should().Contain(new[] { "CAM", "TS", "HDCAM", "HDTS" });
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
}

