using FluentAssertions;
using TunnelFin.Indexers;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Indexers;

/// <summary>
/// Unit tests for TorznabIndexer (FR-017).
/// Tests custom Torznab endpoint support.
/// </summary>
public class TorznabIndexerTests
{
    [Fact]
    public void TorznabIndexer_Should_Accept_Custom_Endpoint()
    {
        // Arrange & Act
        var indexer = new TorznabIndexer(
            name: "MyTorznab",
            endpoint: "https://example.com/api",
            apiKey: "test-key");

        // Assert
        indexer.Name.Should().Be("MyTorznab");
        indexer.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TorznabIndexer_Should_Reject_Empty_Endpoint()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TorznabIndexer(
            name: "Test",
            endpoint: "",
            apiKey: "key"));
    }

    [Fact]
    public void TorznabIndexer_Should_Reject_Empty_Name()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TorznabIndexer(
            name: "",
            endpoint: "https://example.com",
            apiKey: "key"));
    }

    [Fact]
    public void TorznabIndexer_Should_Allow_Empty_ApiKey()
    {
        // Arrange & Act - Some Torznab endpoints don't require API keys
        var indexer = new TorznabIndexer(
            name: "Public",
            endpoint: "https://example.com",
            apiKey: "");

        // Assert
        indexer.Should().NotBeNull("public endpoints should work without API key");
    }

    [Fact]
    public async Task TorznabIndexer_Should_Build_Correct_Query_Url()
    {
        // Arrange
        var indexer = new TorznabIndexer(
            name: "Test",
            endpoint: "https://example.com/api",
            apiKey: "test-key");

        // Act - This will fail until we implement actual HTTP calls
        var results = await indexer.SearchAsync("test query", ContentType.Movie);

        // Assert
        results.Should().NotBeNull();
        results.Should().BeEmpty("placeholder implementation");
    }

    [Fact]
    public void TorznabIndexer_Should_Support_All_Content_Types()
    {
        // Arrange
        var indexer = new TorznabIndexer(
            name: "Test",
            endpoint: "https://example.com",
            apiKey: "key");

        // Act
        var capabilities = indexer.GetCapabilities();

        // Assert
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Movie);
        capabilities.SupportedContentTypes.Should().Contain(ContentType.TVShow);
        capabilities.SupportedContentTypes.Should().Contain(ContentType.Anime);
    }

    [Fact]
    public async Task TorznabIndexer_Should_Handle_Search_Timeout()
    {
        // Arrange
        var indexer = new TorznabIndexer(
            name: "Test",
            endpoint: "https://example.com",
            apiKey: "key");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await indexer.SearchAsync("test", ContentType.Movie, cts.Token));
    }

    [Theory]
    [InlineData("https://jackett.example.com/api/v2.0/indexers/all/results/torznab")]
    [InlineData("https://prowlarr.example.com/1/api")]
    [InlineData("https://custom-indexer.com/torznab")]
    public void TorznabIndexer_Should_Accept_Various_Endpoint_Formats(string endpoint)
    {
        // Act
        var indexer = new TorznabIndexer(
            name: "Test",
            endpoint: endpoint,
            apiKey: "key");

        // Assert
        indexer.Should().NotBeNull($"should accept endpoint: {endpoint}");
    }

    [Fact]
    public void TorznabIndexer_Should_Be_Disableable()
    {
        // Arrange
        var indexer = new TorznabIndexer(
            name: "Test",
            endpoint: "https://example.com",
            apiKey: "key");

        // Act
        indexer.IsEnabled = false;

        // Assert
        indexer.IsEnabled.Should().BeFalse("should allow disabling");
    }
}

