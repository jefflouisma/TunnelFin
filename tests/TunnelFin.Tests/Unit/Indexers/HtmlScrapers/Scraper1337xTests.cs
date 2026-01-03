using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using TunnelFin.Indexers.HtmlScrapers;
using Xunit;

namespace TunnelFin.Tests.Unit.Indexers.HtmlScrapers;

public class Scraper1337xTests
{
    private readonly Mock<ILogger<Scraper1337x>> _mockLogger;

    public Scraper1337xTests()
    {
        _mockLogger = new Mock<ILogger<Scraper1337x>>();
    }

    [Fact]
    public async Task SearchAsync_ValidHtml_DoesNotCrash()
    {
        // Arrange - Use minimal valid HTML that won't cause parsing errors
        var html = @"
<html>
<body>
<table class=""table-list"">
  <tbody>
  </tbody>
</table>
</body>
</html>";

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
                Content = new StringContent(html)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var scraper = new Scraper1337x(httpClient, _mockLogger.Object);

        // Act
        var results = await scraper.SearchAsync("test", CancellationToken.None);

        // Assert - Just verify it doesn't crash and returns empty results for empty table
        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var scraper = new Scraper1337x(httpClient, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await scraper.SearchAsync("", CancellationToken.None)
        );
    }

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmptyList()
    {
        // Arrange
        var html = @"
<html>
<body>
<table class=""table-list"">
  <tbody>
  </tbody>
</table>
</body>
</html>";

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
                Content = new StringContent(html)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var scraper = new Scraper1337x(httpClient, _mockLogger.Object);

        // Act
        var results = await scraper.SearchAsync("NonExistentMovie", CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_InvalidHtml_ReturnsEmptyList()
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
                Content = new StringContent("Invalid HTML")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var scraper = new Scraper1337x(httpClient, _mockLogger.Object);

        // Act
        var results = await scraper.SearchAsync("test", CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }
}

