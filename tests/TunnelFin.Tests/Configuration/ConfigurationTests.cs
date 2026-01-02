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

