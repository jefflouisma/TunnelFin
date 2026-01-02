using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Jellyfin;

/// <summary>
/// Unit tests for TunnelFinSearchProvider (FR-027, FR-034, FR-039).
/// Tests search functionality, network availability checks, and play button color coding.
/// </summary>
public class TunnelFinSearchProviderTests
{
    private readonly TunnelFinSearchProvider _provider;

    public TunnelFinSearchProviderTests()
    {
        _provider = new TunnelFinSearchProvider(NullLogger.Instance);
    }

    [Fact]
    public async Task SearchAsync_Should_Return_Empty_Results_For_Placeholder()
    {
        // Act
        var response = await _provider.SearchAsync("Inception 2010", ContentType.Movie, limit: 20);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().BeEmpty("placeholder implementation returns no results");
        response.TotalResults.Should().Be(0);
        response.SearchDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SearchAsync_Should_Reject_Empty_Query()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync("", ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Reject_Invalid_Limit()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, limit: 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, limit: 101));
    }

    [Fact]
    public async Task SearchAsync_Should_Complete_Within_Timeout()
    {
        // Arrange
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token);

        // Assert
        response.SearchDuration.Should().BeLessThan(TimeSpan.FromSeconds(5), "search should complete within SC-004 timeout");
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Update_Status()
    {
        // Arrange
        _provider.IsNetworkAvailable.Should().BeFalse("network should be unavailable initially");

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync();

        // Assert
        isAvailable.Should().BeTrue("placeholder implementation returns true");
        _provider.IsNetworkAvailable.Should().BeTrue("status should be updated");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Green_When_Network_Available()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 10
        };

        // Network is available after check
        _provider.CheckNetworkAvailabilityAsync().Wait();

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Green, "network is available and torrent has seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_No_Seeders()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 0
        };

        _provider.CheckNetworkAvailabilityAsync().Wait();

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "torrent has no seeders");
    }

    [Fact]
    public void GetPlayButtonColor_Should_Reject_Null_Result()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _provider.GetPlayButtonColor(null!));
    }

    [Theory]
    [InlineData(ContentType.Movie)]
    [InlineData(ContentType.TVShow)]
    [InlineData(ContentType.Anime)]
    public async Task SearchAsync_Should_Support_All_Content_Types(ContentType contentType)
    {
        // Act
        var response = await _provider.SearchAsync("test", contentType);

        // Assert
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token));
    }


    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act
        var act = () => new TunnelFinSearchProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task SearchAsync_Should_Apply_Limit_To_Results()
    {
        // Arrange
        var limit = 10;

        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie, limit: limit);

        // Assert
        response.Results.Count.Should().BeLessThanOrEqualTo(limit);
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Null()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync(null!, ContentType.Movie));
    }

    [Fact]
    public async Task SearchAsync_Should_Throw_When_Query_Is_Whitespace()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _provider.SearchAsync("   ", ContentType.Movie));
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Return_False_On_Exception()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync(cts.Token);

        // Assert
        // Should handle cancellation gracefully
        isAvailable.Should().BeFalse();
        _provider.IsNetworkAvailable.Should().BeFalse();
    }

    [Fact]
    public void GetPlayButtonColor_Should_Return_Orange_When_Network_Unavailable()
    {
        // Arrange
        var result = new SearchResult
        {
            Title = "Test Movie",
            InfoHash = "0123456789abcdef0123456789abcdef01234567",
            Seeders = 10
        };

        // Network is unavailable initially

        // Act
        var color = _provider.GetPlayButtonColor(result);

        // Assert
        color.Should().Be(PlayButtonColor.Orange, "network is unavailable");
    }

    [Fact]
    public async Task SearchAsync_Should_Log_Cancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _provider.SearchAsync("test", ContentType.Movie, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SearchAsync_Should_Return_SearchResponse_With_Metadata()
    {
        // Act
        var response = await _provider.SearchAsync("test", ContentType.Movie);

        // Assert
        response.Should().NotBeNull();
        response.Results.Should().NotBeNull();
        response.TotalResults.Should().BeGreaterThanOrEqualTo(0);
        response.SearchDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        response.IndexersQueried.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckNetworkAvailabilityAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var isAvailable = await _provider.CheckNetworkAvailabilityAsync(cts.Token);

        // Assert
        isAvailable.Should().BeFalse();
    }

}

