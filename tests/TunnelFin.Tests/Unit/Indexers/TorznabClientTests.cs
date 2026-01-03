using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TunnelFin.Configuration;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Models;
using Xunit;

namespace TunnelFin.Tests.Unit.Indexers;

public class TorznabClientTests
{
    private readonly Mock<ILogger<TorznabClient>> _mockLogger;

    public TorznabClientTests()
    {
        _mockLogger = new Mock<ILogger<TorznabClient>>();
    }

    [Fact]
    public async Task SearchAsync_ValidXml_ParsesCorrectly()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel>
    <item>
      <title>Big Buck Bunny 1080p</title>
      <link>magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&amp;dn=Big+Buck+Bunny</link>
      <torznab:attr name=""size"" value=""734003200""/>
      <torznab:attr name=""seeders"" value=""42""/>
      <torznab:attr name=""peers"" value=""15""/>
    </item>
  </channel>
</rss>";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xml)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9117/api/v2.0/indexers/test",
            ApiKey = "test-api-key"
        };

        // Act
        var results = await client.SearchAsync(config, "Big Buck Bunny", CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        var result = results.First();
        result.Title.Should().Be("Big Buck Bunny 1080p");
        result.InfoHash.Should().Be("dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c");
        result.Size.Should().Be(734003200);
        result.Seeders.Should().Be(42);
        result.Leechers.Should().Be(15);
        result.IndexerName.Should().Be("Test Indexer");
    }

    [Fact]
    public async Task SearchAsync_RateLimiting_EnforcesOneRequestPerSecond()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel></channel>
</rss>")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9117/api/v2.0/indexers/test",
            ApiKey = "test-api-key"
        };

        // Act
        var startTime = DateTime.UtcNow;
        await client.SearchAsync(config, "test1", CancellationToken.None);
        await client.SearchAsync(config, "test2", CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert - second request should be delayed by ~1 second
        elapsed.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(900); // Allow 100ms tolerance
    }

    [Fact]
    public async Task SearchAsync_ExponentialBackoff_RetriesOn429()
    {
        // Arrange
        var attemptCount = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    return new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.TooManyRequests
                    };
                }
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel></channel>
</rss>")
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9117/api/v2.0/indexers/test",
            ApiKey = "test-api-key"
        };

        // Act
        var startTime = DateTime.UtcNow;
        var results = await client.SearchAsync(config, "test", CancellationToken.None);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        attemptCount.Should().Be(3); // Should retry twice before succeeding
        elapsed.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(2900); // 1s + 2s delays (allow 100ms tolerance)
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9117/api/v2.0/indexers/test",
            ApiKey = "test-api-key"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await client.SearchAsync(config, "", CancellationToken.None)
        );
    }

    [Fact]
    public async Task SearchAsync_InvalidXml_ReturnsEmptyList()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Invalid XML content")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9117/api/v2.0/indexers/test",
            ApiKey = "test-api-key"
        };

        // Act
        var results = await client.SearchAsync(config, "test", CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ProwlarrStyleUrl_BuildsCorrectUrl()
    {
        // Arrange - Prowlarr uses /{indexerId}/api endpoint
        string? capturedUrl = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUrl = req.RequestUri?.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel></channel>
</rss>")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        // Prowlarr-style URL ends with /api
        var config = new IndexerConfig
        {
            Name = "Prowlarr - TPB",
            Type = IndexerType.Torznab,
            BaseUrl = "http://192.168.1.10:30696/7/api",
            ApiKey = "bcd0a9996707444c8ed39a88990fb7aa"
        };

        // Act
        await client.SearchAsync(config, "test query", CancellationToken.None);

        // Assert - should NOT double the /api path
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("/7/api?t=search");
        capturedUrl.Should().NotContain("/api/api");
        capturedUrl.Should().Contain("q=test%20query");
        capturedUrl.Should().Contain("apikey=bcd0a9996707444c8ed39a88990fb7aa");
    }

    [Fact]
    public async Task SearchAsync_ProwlarrResponse_ParsesInfoHashFromAttribute()
    {
        // Arrange - Prowlarr returns infohash as torznab:attr, magnet in <guid>
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<rss version=""2.0"" xmlns:torznab=""http://torznab.com/schemas/2015/feed"">
  <channel>
    <item>
      <title>Big.Buck.Bunny.BDRip.XviD-MEDiC</title>
      <guid>magnet:?xt=urn:btih:C39FE3EEFBDB62DA9C27EB6398FF4A7D2E26E7AB&amp;dn=Big.Buck.Bunny</guid>
      <link>http://192.168.1.10:30696/7/download?apikey=xxx&amp;link=encoded</link>
      <torznab:attr name=""size"" value=""183567936""/>
      <torznab:attr name=""seeders"" value=""5""/>
      <torznab:attr name=""peers"" value=""3""/>
      <torznab:attr name=""infohash"" value=""C39FE3EEFBDB62DA9C27EB6398FF4A7D2E26E7AB""/>
    </item>
  </channel>
</rss>";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(xml)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Prowlarr - TPB",
            Type = IndexerType.Torznab,
            BaseUrl = "http://192.168.1.10:30696/7/api",
            ApiKey = "test-key"
        };

        // Act
        var results = await client.SearchAsync(config, "Big Buck Bunny", CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        var result = results.First();
        result.Title.Should().Be("Big.Buck.Bunny.BDRip.XviD-MEDiC");
        result.InfoHash.Should().Be("c39fe3eefbdb62da9c27eb6398ff4a7d2e26e7ab"); // lowercase
        result.Size.Should().Be(183567936);
        result.Seeders.Should().Be(5);
        result.Leechers.Should().Be(3);
        // Magnet should come from <guid>, not <link>
        result.MagnetLink.Should().StartWith("magnet:?xt=urn:btih:");
        result.MagnetLink.Should().Contain("C39FE3EEFBDB62DA9C27EB6398FF4A7D2E26E7AB");
    }
}
