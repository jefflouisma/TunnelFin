using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MonoTorrent;
using MonoTorrent.Client;
using TunnelFin.BitTorrent;
using TunnelFin.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace TunnelFin.Integration.Performance;

/// <summary>
/// Performance test for SC-004: System handles 10 concurrent streaming sessions without degradation.
/// </summary>
public class ConcurrentStreamsTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private TorrentEngine? _torrentEngine;
    private string _downloadPath = string.Empty;

    // Big Buck Bunny magnet link (9.2MB MP4)
    private const string BigBuckBunnyMagnet = 
        "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c" +
        "&dn=Big+Buck+Bunny" +
        "&tr=udp%3A%2F%2Ftracker.leechers-paradise.org%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.coppersurfer.tk%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337" +
        "&tr=udp%3A%2F%2Fexplodie.org%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.empire-js.us%3A1337" +
        "&ws=https%3A%2F%2Fwebtorrent.io%2Ftorrents%2Fbig-buck-bunny.torrent";

    public ConcurrentStreamsTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _downloadPath = Path.Combine(Path.GetTempPath(), $"TunnelFin_Perf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_downloadPath);

        // Create TunnelFin TorrentEngine (it creates MonoTorrent ClientEngine internally)
        _torrentEngine = new TorrentEngine(downloadPath: _downloadPath);
        _output.WriteLine($"TorrentEngine initialized. Download path: {_downloadPath}");

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_torrentEngine != null)
        {
            _torrentEngine.Dispose();
        }

        if (Directory.Exists(_downloadPath))
        {
            try
            {
                Directory.Delete(_downloadPath, true);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Failed to delete temp directory: {ex.Message}");
            }
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task SC004_TenConcurrentStreams_ShouldNotDegrade()
    {
        // Arrange
        const int concurrentStreams = 10;
        var stopwatch = Stopwatch.StartNew();
        var tasks = new List<Task<StreamResult>>();

        _output.WriteLine($"Starting {concurrentStreams} concurrent streams...");

        // Act - Start 10 concurrent streams
        for (int i = 0; i < concurrentStreams; i++)
        {
            var streamId = i;
            var task = Task.Run(async () =>
            {
                var streamStopwatch = Stopwatch.StartNew();
                try
                {
                    // Add torrent
                    var metadata = await _torrentEngine!.AddTorrentAsync(BigBuckBunnyMagnet, CancellationToken.None);
                    _output.WriteLine($"Stream {streamId}: Metadata fetched in {streamStopwatch.ElapsedMilliseconds}ms");

                    // Create stream
                    var stream = await _torrentEngine.CreateStreamAsync(
                        metadata.InfoHash,
                        metadata.Files.First().Path,
                        prebuffer: true,
                        CancellationToken.None);

                    streamStopwatch.Stop();
                    _output.WriteLine($"Stream {streamId}: Started in {streamStopwatch.ElapsedMilliseconds}ms");

                    // Read some data to verify stream works
                    var buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    return new StreamResult
                    {
                        StreamId = streamId,
                        Success = true,
                        StartTimeMs = streamStopwatch.ElapsedMilliseconds,
                        BytesRead = bytesRead
                    };
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Stream {streamId}: FAILED - {ex.Message}");
                    return new StreamResult
                    {
                        StreamId = streamId,
                        Success = false,
                        Error = ex.Message
                    };
                }
            });

            tasks.Add(task);
        }

        // Wait for all streams to complete
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);
        var avgStartTime = results.Where(r => r.Success).Average(r => r.StartTimeMs);
        var maxStartTime = results.Where(r => r.Success).Max(r => r.StartTimeMs);

        _output.WriteLine($"\n=== SC-004 Results ===");
        _output.WriteLine($"Total streams: {concurrentStreams}");
        _output.WriteLine($"Successful: {successCount}/{concurrentStreams} ({successCount * 100.0 / concurrentStreams:F1}%)");
        _output.WriteLine($"Failed: {failureCount}");
        _output.WriteLine($"Average start time: {avgStartTime:F0}ms");
        _output.WriteLine($"Max start time: {maxStartTime:F0}ms");
        _output.WriteLine($"Total duration: {stopwatch.ElapsedMilliseconds}ms");

        // Success criteria: All streams should start successfully
        successCount.Should().Be(concurrentStreams, "all streams should start successfully");
        
        // No stream should take longer than 30 seconds (SC-001)
        maxStartTime.Should().BeLessThan(30000, "no stream should take longer than 30 seconds");
    }

    private class StreamResult
    {
        public int StreamId { get; set; }
        public bool Success { get; set; }
        public long StartTimeMs { get; set; }
        public int BytesRead { get; set; }
        public string? Error { get; set; }
    }
}

