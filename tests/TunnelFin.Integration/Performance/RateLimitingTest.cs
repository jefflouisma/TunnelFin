using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TunnelFin.Configuration;
using TunnelFin.Indexers;
using TunnelFin.Indexers.Torznab;
using TunnelFin.Models;
using Xunit;
using Xunit.Abstractions;

namespace TunnelFin.Integration.Performance;

/// <summary>
/// Performance test for SC-007: Rate limiting prevents more than 1 request/second per indexer under load.
/// </summary>
public class RateLimitingTest
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<TorznabClient>> _mockLogger;

    public RateLimitingTest(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<TorznabClient>>();
    }

    [Fact]
    public async Task SC007_RateLimiting_ShouldEnforce1RequestPerSecond()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9999/api", // Non-existent endpoint (we're testing rate limiting, not actual requests)
            ApiKey = "test-key",
            Enabled = true,
            RateLimitPerSecond = 1.0 // 1 request per second
        };

        const int totalRequests = 10;
        var requestTimestamps = new List<DateTime>();
        var stopwatch = Stopwatch.StartNew();

        _output.WriteLine($"Sending {totalRequests} requests with 1 req/s rate limit...");

        // Act - Send multiple requests rapidly
        var tasks = new List<Task>();
        for (int i = 0; i < totalRequests; i++)
        {
            var requestId = i;
            var task = Task.Run(async () =>
            {
                try
                {
                    var timestamp = DateTime.UtcNow;
                    lock (requestTimestamps)
                    {
                        requestTimestamps.Add(timestamp);
                    }

                    // This will fail (endpoint doesn't exist), but we're measuring timing
                    await client.SearchAsync(config, "test", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Expected to fail - we're testing rate limiting, not actual requests
                    _output.WriteLine($"Request {requestId}: {ex.GetType().Name} (expected)");
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        requestTimestamps.Sort();
        var timeDeltasMs = new List<double>();

        for (int i = 1; i < requestTimestamps.Count; i++)
        {
            var delta = (requestTimestamps[i] - requestTimestamps[i - 1]).TotalMilliseconds;
            timeDeltasMs.Add(delta);
            _output.WriteLine($"Request {i}: {delta:F0}ms after previous request");
        }

        var avgDeltaMs = timeDeltasMs.Average();
        var minDeltaMs = timeDeltasMs.Min();
        var maxDeltaMs = timeDeltasMs.Max();

        _output.WriteLine($"\n=== SC-007 Results ===");
        _output.WriteLine($"Total requests: {totalRequests}");
        _output.WriteLine($"Total duration: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average time between requests: {avgDeltaMs:F0}ms");
        _output.WriteLine($"Min time between requests: {minDeltaMs:F0}ms");
        _output.WriteLine($"Max time between requests: {maxDeltaMs:F0}ms");
        _output.WriteLine($"Expected: ~1000ms between requests (1 req/s)");

        // Success criteria: Average time between requests should be ~1000ms (1 req/s)
        // Allow 10% tolerance for timing variations
        avgDeltaMs.Should().BeGreaterThan(900, "rate limiting should enforce ~1 req/s");
        avgDeltaMs.Should().BeLessThan(1100, "rate limiting should not be too conservative");

        // No two requests should be less than 900ms apart (with 10% tolerance)
        minDeltaMs.Should().BeGreaterThan(900, "no two requests should violate rate limit");
    }

    [Fact]
    public async Task SC007_RateLimiting_ShouldQueueRequests()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new TorznabClient(httpClient, _mockLogger.Object);

        var config = new IndexerConfig
        {
            Name = "Test Indexer",
            Type = IndexerType.Torznab,
            BaseUrl = "http://localhost:9999/api",
            ApiKey = "test-key",
            Enabled = true,
            RateLimitPerSecond = 1.0
        };

        const int burstRequests = 5;
        var completionTimes = new List<long>();
        var stopwatch = Stopwatch.StartNew();

        _output.WriteLine($"Sending {burstRequests} burst requests...");

        // Act - Send burst of requests simultaneously
        var tasks = Enumerable.Range(0, burstRequests).Select(async i =>
        {
            var requestStopwatch = Stopwatch.StartNew();
            try
            {
                await client.SearchAsync(config, "test", CancellationToken.None);
            }
            catch
            {
                // Expected to fail
            }
            requestStopwatch.Stop();
            
            lock (completionTimes)
            {
                completionTimes.Add(requestStopwatch.ElapsedMilliseconds);
            }
            
            _output.WriteLine($"Request {i}: Completed in {requestStopwatch.ElapsedMilliseconds}ms");
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        completionTimes.Sort();
        
        _output.WriteLine($"\n=== SC-007 Queueing Results ===");
        _output.WriteLine($"Burst requests: {burstRequests}");
        _output.WriteLine($"Total duration: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"First request completed: {completionTimes.First()}ms");
        _output.WriteLine($"Last request completed: {completionTimes.Last()}ms");
        _output.WriteLine($"Expected: ~{(burstRequests - 1) * 1000}ms for all requests");

        // Success criteria: Requests should be queued and processed sequentially
        // Total time should be approximately (burstRequests - 1) * 1000ms
        var expectedDurationMs = (burstRequests - 1) * 1000;
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(expectedDurationMs - 500, "requests should be queued");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(expectedDurationMs + 500, "queueing should not add excessive delay");
    }
}

