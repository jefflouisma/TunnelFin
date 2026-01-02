using Xunit;
using FluentAssertions;
using System.Net.Http;

namespace TunnelFin.Integration.Jellyfin;

/// <summary>
/// Integration tests for Jellyfin connectivity.
/// Tests against the Kubernetes deployment at http://192.168.64.6:8096
/// </summary>
public class SampleIntegrationTest
{
    private const string JellyfinBaseUrl = "http://192.168.64.6:8096";
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    [Fact]
    public void SampleIntegrationTest_ShouldPass()
    {
        // Arrange
        var expected = "Integration Test";

        // Act
        var actual = "Integration Test";

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task Jellyfin_ShouldBeAccessible()
    {
        // Arrange
        var systemInfoUrl = $"{JellyfinBaseUrl}/System/Info/Public";

        // Act
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.GetAsync(systemInfoUrl);
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Failed to connect to Jellyfin at {JellyfinBaseUrl}. " +
                              $"Ensure Jellyfin is running: kubectl -n jellyfin get pods", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new Exception($"Timeout connecting to Jellyfin at {JellyfinBaseUrl}. " +
                              $"Ensure Jellyfin is accessible: curl {systemInfoUrl}", ex);
        }

        // Assert
        response.Should().NotBeNull();
        response!.IsSuccessStatusCode.Should().BeTrue(
            $"Jellyfin should be accessible at {JellyfinBaseUrl}. Status: {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty("Jellyfin should return system info");
        content.Should().Contain("ServerName", "response should contain Jellyfin system info");
    }
}

