using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Jellyfin;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Jellyfin;

/// <summary>
/// Tests for ChannelProvider (T071).
/// </summary>
public class ChannelProviderTests
{
    private readonly Mock<ILogger<ChannelProvider>> _mockLogger;
    private readonly Mock<TunnelFinSearchProvider> _mockSearchProvider;
    private readonly ChannelProvider _channelProvider;

    public ChannelProviderTests()
    {
        _mockLogger = new Mock<ILogger<ChannelProvider>>();
        var mockSearchLogger = new Mock<ILogger<TunnelFinSearchProvider>>();
        _mockSearchProvider = new Mock<TunnelFinSearchProvider>(mockSearchLogger.Object, null);
        _channelProvider = new ChannelProvider(_mockLogger.Object, _mockSearchProvider.Object);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Is_Null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChannelProvider(null!, _mockSearchProvider.Object));
    }

    [Fact]
    public void Constructor_Should_Throw_When_SearchProvider_Is_Null()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChannelProvider(_mockLogger.Object, null!));
    }

    [Fact]
    public async Task GetChannelItemsAsync_Should_Throw_When_Category_Is_Empty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _channelProvider.GetChannelItemsAsync(""));
    }

    [Fact]
    public async Task GetChannelItemsAsync_Should_Return_Empty_List_For_Valid_Category()
    {
        // Act
        var result = await _channelProvider.GetChannelItemsAsync("Movies");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // Placeholder implementation
    }

    [Fact]
    public async Task GetChannelItemAsync_Should_Throw_When_ItemId_Is_Empty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _channelProvider.GetChannelItemAsync(""));
    }

    [Fact]
    public async Task GetChannelItemAsync_Should_Return_Null_For_Valid_ItemId()
    {
        // Act
        var result = await _channelProvider.GetChannelItemAsync("test-item-id");

        // Assert
        result.Should().BeNull(); // Placeholder implementation
    }

    [Fact]
    public void GetCategories_Should_Return_Predefined_Categories()
    {
        // Act
        var categories = _channelProvider.GetCategories();

        // Assert
        categories.Should().NotBeNull();
        categories.Should().Contain("Movies");
        categories.Should().Contain("TV Shows");
        categories.Should().Contain("Anime");
        categories.Should().Contain("Popular");
        categories.Should().Contain("Recent");
    }

    [Fact]
    public async Task GetChannelItemsAsync_Should_Respect_Limit()
    {
        // Act
        var result = await _channelProvider.GetChannelItemsAsync("Movies", limit: 10);

        // Assert
        result.Should().NotBeNull();
        // Placeholder implementation returns empty list
        result.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task GetChannelItemsAsync_Should_Respect_Cancellation_Token()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _channelProvider.GetChannelItemsAsync("Movies", cancellationToken: cts.Token));
    }
}

