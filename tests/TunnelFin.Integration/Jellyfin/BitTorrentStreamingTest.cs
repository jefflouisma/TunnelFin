using System.Net;
using FluentAssertions;
using MonoTorrent;
using MonoTorrent.Client;
using Xunit;
using Xunit.Abstractions;

namespace TunnelFin.Integration.Jellyfin;

/// <summary>
/// End-to-end integration test that downloads Big Buck Bunny via BitTorrent
/// and verifies the HTTP stream is accessible and contains valid video data.
/// 
/// This test uses real network traffic and downloads from the actual BitTorrent swarm.
/// Big Buck Bunny is a Creative Commons licensed video from the Blender Foundation.
/// </summary>
public class BitTorrentStreamingTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ClientEngine? _engine;
    private string? _downloadPath;

    // Big Buck Bunny - Creative Commons licensed, well-seeded torrent from WebTorrent
    // InfoHash: dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c
    // Note: Seeder discovery may take 30-60 seconds via opentrackr.org
    // Confirmed working with official Tribler network on 2026-01-02
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

    // MP4/MOV files start with "ftyp" at offset 4, preceded by box size
    // Common patterns: 00 00 00 XX 66 74 79 70 (ftyp)
    // We'll verify bytes 4-7 are "ftyp" (0x66 0x74 0x79 0x70)
    private static readonly byte[] FtypSignature = { 0x66, 0x74, 0x79, 0x70 }; // "ftyp"

    public BitTorrentStreamingTest(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _downloadPath = Path.Combine(Path.GetTempPath(), $"TunnelFin_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_downloadPath);

        var settings = new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            AutoSaveLoadDhtCache = false,
            AutoSaveLoadFastResume = false,
            AutoSaveLoadMagnetLinkMetadata = false,
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                { "ipv4", new IPEndPoint(IPAddress.Any, 0) }
            },
            HttpStreamingPrefix = "http://127.0.0.1:18888/"
        }.ToSettings();

        _engine = new ClientEngine(settings);
        _output.WriteLine($"Engine initialized. Download path: {_downloadPath}");
    }

    public async Task DisposeAsync()
    {
        if (_engine != null)
        {
            foreach (var manager in _engine.Torrents)
            {
                await manager.StopAsync();
            }
            await _engine.StopAllAsync();
            _engine.Dispose();
        }

        if (_downloadPath != null && Directory.Exists(_downloadPath))
        {
            try { Directory.Delete(_downloadPath, recursive: true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    [Fact(Timeout = 120_000)] // 2 minute timeout
    public async Task BigBuckBunny_ShouldStreamViaHttp_WithValidVideoData()
    {
        // Arrange
        var magnet = MagnetLink.Parse(BigBuckBunnyMagnet);
        _output.WriteLine($"Parsed magnet link. InfoHash: {magnet.InfoHashes.V1}");

        var manager = await _engine!.AddStreamingAsync(magnet, _downloadPath!);
        _output.WriteLine("Added torrent to engine in streaming mode");

        // Act - Start download and wait for metadata
        await manager.StartAsync();
        _output.WriteLine("Torrent started. Waiting for metadata...");

        using var metadataCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await manager.WaitForMetadataAsync(metadataCts.Token);
        _output.WriteLine($"Metadata received. Files: {manager.Files.Count}");

        // Find the video file (largest file, should be .webm)
        var videoFile = manager.Files
            .OrderByDescending(f => f.Length)
            .First();
        _output.WriteLine($"Selected video file: {videoFile.Path} ({videoFile.Length / 1024 / 1024} MB)");

        // Create HTTP stream
        var streamProvider = manager.StreamProvider ?? throw new InvalidOperationException("StreamProvider is null");
        var httpStream = await streamProvider.CreateHttpStreamAsync(videoFile, prebuffer: true);
        _output.WriteLine($"HTTP stream created at: {httpStream.FullUri}");

        // Verify HTTP stream is accessible
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var response = await httpClient.GetAsync(httpStream.FullUri, HttpCompletionOption.ResponseHeadersRead);
        
        // Assert - HTTP response (206 Partial Content is expected for range-based streaming)
        response.IsSuccessStatusCode.Should().BeTrue(
            $"HTTP stream endpoint should return success status, got {response.StatusCode}");
        response.StatusCode.Should().BeOneOf(
            new[] { HttpStatusCode.OK, HttpStatusCode.PartialContent },
            "HTTP stream should return 200 OK or 206 Partial Content for range requests");
        _output.WriteLine($"HTTP response: {response.StatusCode}");

        // Read first bytes and verify MP4 format (ftyp box at offset 4)
        await using var contentStream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[8];
        var bytesRead = await contentStream.ReadAsync(buffer, 0, 8);

        bytesRead.Should().Be(8, "Should read 8 bytes for MP4 ftyp box verification");
        var ftypBytes = buffer[4..8];
        ftypBytes.Should().BeEquivalentTo(FtypSignature,
            "Bytes 4-7 should be 'ftyp' (0x66 0x74 0x79 0x70) for MP4 files");
        _output.WriteLine($"Verified MP4 ftyp signature: {BitConverter.ToString(buffer)}");

        // Read more data to ensure stream is working
        var largerBuffer = new byte[1024 * 64]; // 64KB
        var totalRead = await contentStream.ReadAsync(largerBuffer, 0, largerBuffer.Length);
        totalRead.Should().BeGreaterThan(0, "Should be able to read video data from stream");
        _output.WriteLine($"Successfully read {totalRead} additional bytes from stream");

        // Cleanup
        await manager.StopAsync();
        _output.WriteLine("Test completed successfully!");
    }
}

