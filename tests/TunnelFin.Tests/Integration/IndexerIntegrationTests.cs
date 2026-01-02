using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Configuration;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Integration tests for Torznab indexers.
/// These tests require a running Jackett instance (Docker-based).
/// Skip these tests if Jackett is not available.
/// </summary>
public class IndexerIntegrationTests
{
    private readonly Mock<ILogger<TorznabClient>> _mockLogger;
    private const string JackettBaseUrl = "http://localhost:9117/api/v2.0/indexers/all";
    private const string JackettApiKey = "test-api-key"; // Replace with actual API key for real tests

    public IndexerIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<TorznabClient>>();
    }

    [Fact(Skip = "Requires running Jackett instance")]
    public async Task TorznabClient_LiveJackettQuery_ReturnsResults()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Jackett All",
            Type = IndexerType.Torznab,
            BaseUrl = JackettBaseUrl,
            ApiKey = JackettApiKey,
            Enabled = true
        };

        // Act
        var results = await client.SearchAsync(config, "Big Buck Bunny", CancellationToken.None);

        // Assert
        results.Should().NotBeEmpty();
        results.All(r => !string.IsNullOrWhiteSpace(r.Title)).Should().BeTrue();
        results.All(r => !string.IsNullOrWhiteSpace(r.InfoHash)).Should().BeTrue();
        results.All(r => r.InfoHash.Length == 40).Should().BeTrue();
        results.All(r => r.Size > 0).Should().BeTrue();
        results.All(r => !string.IsNullOrWhiteSpace(r.MagnetLink)).Should().BeTrue();
    }

    [Fact(Skip = "Requires running Jackett instance")]
    public async Task TorznabClient_LiveJackettQuery_ParsesMetadataCorrectly()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Jackett All",
            Type = IndexerType.Torznab,
            BaseUrl = JackettBaseUrl,
            ApiKey = JackettApiKey,
            Enabled = true
        };

        // Act
        var results = await client.SearchAsync(config, "ubuntu", CancellationToken.None);

        // Assert
        results.Should().NotBeEmpty();

        // Verify at least one result has seeders
        results.Any(r => r.Seeders.HasValue && r.Seeders.Value > 0).Should().BeTrue();

        // Verify all results have valid InfoHash format (40-char hex)
        results.All(r => System.Text.RegularExpressions.Regex.IsMatch(r.InfoHash, "^[a-f0-9]{40}$")).Should().BeTrue();
    }

    [Fact(Skip = "Requires running Jackett instance")]
    public async Task TorznabClient_LiveJackettQuery_HandlesRateLimiting()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Jackett All",
            Type = IndexerType.Torznab,
            BaseUrl = JackettBaseUrl,
            ApiKey = JackettApiKey,
            Enabled = true
        };

        // Act
        var startTime = DateTime.UtcNow;
        await client.SearchAsync(config, "test1", CancellationToken.None);
        await client.SearchAsync(config, "test2", CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - should enforce 1 req/sec rate limit
        elapsed.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(900);
    }

    [Fact]
    public async Task TorznabClient_InvalidApiKey_ReturnsEmptyResults()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Jackett Invalid",
            Type = IndexerType.Torznab,
            BaseUrl = JackettBaseUrl,
            ApiKey = "invalid-api-key-12345",
            Enabled = true
        };

        // Act
        var results = await client.SearchAsync(config, "test", CancellationToken.None);

        // Assert
        results.Should().BeEmpty(); // Should handle 401/403 gracefully
    }

    [Fact]
    public async Task TorznabClient_UnreachableEndpoint_ReturnsEmptyResults()
    {
        // Arrange
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Unreachable",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:99999/api",
            ApiKey = "test",
            Enabled = true
        };

        // Act
        var results = await client.SearchAsync(config, "test", CancellationToken.None);

        // Assert
        results.Should().BeEmpty(); // Should handle connection errors gracefully
    }
}

