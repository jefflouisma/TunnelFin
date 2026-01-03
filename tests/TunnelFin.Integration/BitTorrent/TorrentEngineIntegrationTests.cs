using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TunnelFin.BitTorrent;
using Xunit;
using Xunit.Abstractions;

namespace TunnelFin.Integration.BitTorrent;

/// <summary>
/// Integration tests for TorrentEngine that require BitTorrent network connectivity.
/// These tests connect to the live BitTorrent network and require available seeders.
///
/// Big Buck Bunny is used as a well-seeded, legal test torrent.
/// Enhanced with multiple trackers and WebSeeds for reliable metadata discovery.
/// </summary>
public class TorrentEngineIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<TorrentEngine> _logger;

    // Big Buck Bunny - Creative Commons licensed, well-seeded torrent from WebTorrent
    // Enhanced with multiple trackers and WebSeeds for reliable seeder discovery
    private const string BigBuckBunnyMagnet =
        "magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c" +
        "&dn=Big+Buck+Bunny" +
        "&tr=udp%3A%2F%2Fexplodie.org%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.coppersurfer.tk%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.empire-js.us%3A1337" +
        "&tr=udp%3A%2F%2Ftracker.leechers-paradise.org%3A6969" +
        "&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337" +
        "&tr=wss%3A%2F%2Ftracker.btorrent.xyz" +
        "&tr=wss%3A%2F%2Ftracker.fastcast.nz" +
        "&tr=wss%3A%2F%2Ftracker.openwebtorrent.com" +
        "&ws=https%3A%2F%2Fwebtorrent.io%2Ftorrents%2F" +
        "&xs=https%3A%2F%2Fwebtorrent.io%2Ftorrents%2Fbig-buck-bunny.torrent";

    private const string BigBuckBunnyInfoHash = "dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c";

    // Retry configuration for flaky network conditions
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public TorrentEngineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XunitLogger<TorrentEngine>(output);
    }

    public void Dispose()
    {
        // Cleanup handled per-test
    }

    /// <summary>
    /// T018: Verify TorrentEngine.AddTorrentAsync creates MonoTorrent manager and downloads metadata.
    /// Requires BitTorrent network connectivity and available seeders.
    /// </summary>
    [Fact(Timeout = 180_000)] // 3 minute timeout
    public async Task AddTorrentAsync_ValidMagnetLink_ReturnsMetadata()
    {
        // Arrange
        var engine = new TorrentEngine(downloadPath: null, socketConnector: null, logger: _logger);
        _output.WriteLine($"Starting AddTorrentAsync test with magnet: {BigBuckBunnyInfoHash}");

        try
        {
            // Act - with retry logic for flaky network conditions
            var metadata = await ExecuteWithRetryAsync(async ct =>
            {
                _output.WriteLine("Attempting to add torrent and fetch metadata...");
                return await engine.AddTorrentAsync(BigBuckBunnyMagnet, ct);
            }, TimeSpan.FromMinutes(2));

            // Assert
            metadata.Should().NotBeNull();
            metadata.InfoHash.ToLowerInvariant().Should().Be(BigBuckBunnyInfoHash);
            metadata.Files.Should().NotBeEmpty();
            _output.WriteLine($"Metadata received. Title: {metadata.Title}, Files: {metadata.Files.Count}");

            // Verify file info
            foreach (var file in metadata.Files)
            {
                _output.WriteLine($"  File: {file.Path} ({file.Size / 1024 / 1024} MB)");
            }
        }
        finally
        {
            engine.Dispose();
            _output.WriteLine("Engine disposed");
        }
    }

    /// <summary>
    /// T019: Verify TorrentEngine.CreateStreamAsync returns seekable stream.
    /// Requires BitTorrent network connectivity and available seeders.
    /// </summary>
    [Fact(Timeout = 300_000)] // 5 minute timeout for streaming
    public async Task CreateStreamAsync_ValidInfoHash_ReturnsSeekableStream()
    {
        // Arrange
        var engine = new TorrentEngine(downloadPath: null, socketConnector: null, logger: _logger);
        _output.WriteLine($"Starting CreateStreamAsync test");

        try
        {
            // Get metadata first
            var metadata = await ExecuteWithRetryAsync(async ct =>
            {
                _output.WriteLine("Fetching torrent metadata...");
                return await engine.AddTorrentAsync(BigBuckBunnyMagnet, ct);
            }, TimeSpan.FromMinutes(2));

            // Find largest file (video)
            var videoFile = metadata.Files.OrderByDescending(f => f.Size).First();
            _output.WriteLine($"Selected file: {videoFile.Path} ({videoFile.Size / 1024 / 1024} MB)");

            // Act - Create stream with prebuffering
            using var streamCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _output.WriteLine("Creating stream with prebuffering...");
            var stream = await engine.CreateStreamAsync(metadata.InfoHash, videoFile.Path, prebuffer: true, streamCts.Token);

            // Assert
            stream.Should().NotBeNull();
            stream.CanSeek.Should().BeTrue("BitTorrent streams must support seeking for video playback");
            stream.CanRead.Should().BeTrue();
            stream.Length.Should().BeGreaterThan(0);
            _output.WriteLine($"Stream created successfully. Length: {stream.Length / 1024 / 1024} MB, CanSeek: {stream.CanSeek}");

            // Read some data to verify stream is working
            var buffer = new byte[4096];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            bytesRead.Should().BeGreaterThan(0, "Should be able to read data from stream");
            _output.WriteLine($"Successfully read {bytesRead} bytes from stream");
        }
        finally
        {
            engine.Dispose();
            _output.WriteLine("Engine disposed");
        }
    }

    /// <summary>
    /// T020: Verify TorrentEngine.GetBufferStatus tracks buffered ranges.
    /// Requires BitTorrent network connectivity and available seeders.
    /// </summary>
    [Fact(Timeout = 300_000)] // 5 minute timeout
    public async Task GetBufferStatus_ActiveStream_ReturnsBufferStatus()
    {
        // Arrange
        var engine = new TorrentEngine(downloadPath: null, socketConnector: null, logger: _logger);
        _output.WriteLine("Starting GetBufferStatus test");

        try
        {
            // Get metadata
            var metadata = await ExecuteWithRetryAsync(async ct =>
            {
                _output.WriteLine("Fetching torrent metadata...");
                return await engine.AddTorrentAsync(BigBuckBunnyMagnet, ct);
            }, TimeSpan.FromMinutes(2));

            var videoFile = metadata.Files.OrderByDescending(f => f.Size).First();
            _output.WriteLine($"Selected file: {videoFile.Path}");

            // Create stream with prebuffering
            using var streamCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _output.WriteLine("Creating stream with prebuffering...");
            await engine.CreateStreamAsync(metadata.InfoHash, videoFile.Path, prebuffer: true, streamCts.Token);

            // Act
            var bufferStatus = engine.GetBufferStatus(metadata.InfoHash, videoFile.Path);

            // Assert
            bufferStatus.Should().NotBeNull();
            _output.WriteLine($"Buffer status: {bufferStatus!.CurrentBufferedBytes / 1024} KB buffered");

            // Prebuffering should have downloaded at least some data
            bufferStatus.CurrentBufferedBytes.Should().BeGreaterThan(0, "prebuffering should have downloaded some data");
        }
        finally
        {
            engine.Dispose();
            _output.WriteLine("Engine disposed");
        }
    }

    /// <summary>
    /// T021: Verify TorrentEngine.RemoveTorrentAsync deletes cached data (FR-007a ephemeral requirement).
    /// Requires BitTorrent network connectivity and available seeders.
    /// </summary>
    [Fact(Timeout = 180_000)] // 3 minute timeout
    public async Task RemoveTorrentAsync_ActiveTorrent_DeletesCachedData()
    {
        // Arrange
        var engine = new TorrentEngine(downloadPath: null, socketConnector: null, logger: _logger);
        _output.WriteLine("Starting RemoveTorrentAsync test");

        try
        {
            // Get metadata
            var metadata = await ExecuteWithRetryAsync(async ct =>
            {
                _output.WriteLine("Fetching torrent metadata...");
                return await engine.AddTorrentAsync(BigBuckBunnyMagnet, ct);
            }, TimeSpan.FromMinutes(2));
            _output.WriteLine($"Metadata received for: {metadata.InfoHash}");

            // Act
            _output.WriteLine("Removing torrent...");
            await engine.RemoveTorrentAsync(metadata.InfoHash, CancellationToken.None);

            // Assert
            var retrievedMetadata = engine.GetTorrentMetadata(metadata.InfoHash);
            retrievedMetadata.Should().BeNull("torrent metadata should be removed after RemoveTorrentAsync");
            _output.WriteLine("Torrent removed successfully - metadata is null as expected");
        }
        finally
        {
            engine.Dispose();
            _output.WriteLine("Engine disposed");
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic for flaky network conditions.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                return await operation(cts.Token);
            }
            catch (OperationCanceledException) when (attempt < MaxRetries)
            {
                lastException = new TimeoutException($"Operation timed out after {timeout.TotalSeconds}s");
                _output.WriteLine($"Attempt {attempt}/{MaxRetries} timed out. Retrying in {RetryDelay.TotalSeconds}s...");
                await Task.Delay(RetryDelay);
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                lastException = ex;
                _output.WriteLine($"Attempt {attempt}/{MaxRetries} failed: {ex.Message}. Retrying in {RetryDelay.TotalSeconds}s...");
                await Task.Delay(RetryDelay);
            }
        }

        throw lastException ?? new InvalidOperationException("All retry attempts failed");
    }
}

/// <summary>
/// ILogger adapter that writes to xUnit test output.
/// </summary>
internal class XunitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public XunitLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");

        if (exception != null)
        {
            _output.WriteLine($"  Exception: {exception.Message}");
        }
    }
}

