using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Connections;
using TunnelFin.Models;

namespace TunnelFin.BitTorrent;

/// <summary>
/// Manages BitTorrent torrent lifecycle and streaming operations.
/// Wraps MonoTorrent's ClientEngine with TunnelFin-specific functionality.
/// Implements ITorrentEngine interface for 003-core-integration.
/// </summary>
public class TorrentEngine : ITorrentEngine
{
    private readonly ClientEngine _engine;
    private readonly string _downloadPath;
    private readonly ConcurrentDictionary<string, TorrentManager> _managers;
    private readonly ConcurrentDictionary<string, TorrentMetadata> _metadata;
    private readonly ILogger<TorrentEngine>? _logger;
    private ISocketConnector? _socketConnector;

    /// <summary>
    /// Initializes a new instance of the TorrentEngine class.
    /// </summary>
    /// <param name="downloadPath">Path where torrent data will be cached (default: temp directory)</param>
    /// <param name="socketConnector">Optional custom socket connector for circuit-routed connections (T107)</param>
    /// <param name="logger">Optional logger for diagnostic output</param>
    public TorrentEngine(
        string? downloadPath = null,
        ISocketConnector? socketConnector = null,
        ILogger<TorrentEngine>? logger = null)
    {
        _downloadPath = downloadPath ?? Path.Combine(Path.GetTempPath(), "TunnelFin", "Cache");
        _managers = new ConcurrentDictionary<string, TorrentManager>();
        _metadata = new ConcurrentDictionary<string, TorrentMetadata>();
        _logger = logger;
        _socketConnector = socketConnector;

        // Create download directory if it doesn't exist
        if (!Directory.Exists(_downloadPath))
        {
            Directory.CreateDirectory(_downloadPath);
        }

        // Configure MonoTorrent engine settings for streaming
        var settings = new EngineSettingsBuilder
        {
            DiskCacheBytes = 50 * 1024 * 1024, // 50MB cache (FR-014)
            MaximumConnections = 200,           // Max total connections
            MaximumHalfOpenConnections = 25,    // Max concurrent connection attempts
            AllowPortForwarding = false,        // Use circuits instead (FR-020)
            UsePartialFiles = true,             // Efficient disk usage
            CacheDirectory = _downloadPath
        }.ToSettings();

        // Configure custom socket connector if provided (T107)
        // This enables circuit-routed peer connections for anonymity (FR-020)
        Factories factories = Factories.Default;
        if (socketConnector != null)
        {
            factories = factories.WithSocketConnectorCreator(() => socketConnector);
            _logger?.LogInformation("TorrentEngine configured with custom socket connector for circuit-routed connections");
        }

        _engine = new ClientEngine(settings, factories);
    }

    /// <summary>
    /// Adds a torrent from a magnet link and begins downloading metadata.
    /// </summary>
    public async Task<TorrentMetadata> AddTorrentAsync(string magnetLink, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
            throw new ArgumentException("Magnet link cannot be empty", nameof(magnetLink));

        if (!magnetLink.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid magnet link format", nameof(magnetLink));

        // Parse magnet link
        var magnet = MagnetLink.Parse(magnetLink);
        var infoHash = magnet.InfoHashes.V1OrV2.ToHex();

        _logger?.LogInformation("Adding torrent {InfoHash} from magnet link", infoHash);

        // Check if already added
        if (_managers.ContainsKey(infoHash))
        {
            _logger?.LogDebug("Torrent {InfoHash} already exists, returning cached metadata", infoHash);
            return _metadata[infoHash];
        }

        // Download metadata with 90s timeout (FR-007)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(90));

        _logger?.LogDebug("Downloading metadata for {InfoHash} (timeout: 90s)", infoHash);
        var torrentData = await _engine.DownloadMetadataAsync(magnet, cts.Token);
        var torrent = Torrent.Load(torrentData.ToArray());
        _logger?.LogInformation("Downloaded metadata for {InfoHash}: {Name}, {Size} bytes, {FileCount} files",
            infoHash, torrent.Name, torrent.Size, torrent.Files.Count);

        // Create TorrentManager for streaming
        var torrentSettings = new TorrentSettingsBuilder
        {
            MaximumConnections = 50,  // Per-torrent limit
            MaximumDownloadRate = 0,  // Unlimited
            MaximumUploadRate = 0     // Unlimited
        }.ToSettings();

        var manager = await _engine.AddStreamingAsync(torrent, _downloadPath, torrentSettings);
        
        // Apply custom socket connector if configured
        if (_socketConnector != null)
        {
            // Note: MonoTorrent 3.0.2 socket connector integration would go here
            // This requires accessing internal MonoTorrent APIs or using reflection
        }

        // Start the torrent
        await manager.StartAsync();

        // Store manager
        _managers[infoHash] = manager;

        // Create metadata
        var metadata = ConvertToMetadata(torrent, magnetLink);
        _metadata[infoHash] = metadata;

        _logger?.LogInformation("Successfully added torrent {InfoHash}, started downloading", infoHash);

        return metadata;
    }

    /// <summary>
    /// Creates a seekable stream for a specific file within a torrent.
    /// </summary>
    public async Task<Stream> CreateStreamAsync(string infoHash, string filePath, bool prebuffer, CancellationToken cancellationToken)
    {
        if (!_managers.TryGetValue(infoHash, out var manager))
            throw new KeyNotFoundException($"Torrent {infoHash} not found");

        if (manager.Torrent == null)
            throw new InvalidOperationException("Torrent metadata not available");

        // Find the file (use manager.Files which contains ITorrentManagerFile instances)
        var file = manager.Files.FirstOrDefault(f => f.Path == filePath);
        if (file == null)
            throw new FileNotFoundException($"File {filePath} not found in torrent");

        _logger?.LogInformation("Creating stream for {InfoHash}/{FilePath} (prebuffer: {Prebuffer})",
            infoHash, filePath, prebuffer);

        // Create stream using MonoTorrent's StreamProvider
        var stream = await manager.StreamProvider.CreateStreamAsync(file, prebuffer, cancellationToken);

        _logger?.LogDebug("Stream created for {InfoHash}/{FilePath}, length: {Length} bytes",
            infoHash, filePath, stream.Length);

        return stream;
    }

    /// <summary>
    /// Gets current buffer status for an active stream.
    /// </summary>
    public BufferStatus? GetBufferStatus(string infoHash, string filePath)
    {
        if (!_managers.TryGetValue(infoHash, out var manager))
            return null;

        if (manager.Torrent == null)
            return null;

        var file = manager.Files.FirstOrDefault(f => f.Path == filePath);
        if (file == null)
            return null;

        // Calculate buffered ranges from BitField
        var bufferedRanges = CalculateBufferedRanges(manager, file);

        return new BufferStatus
        {
            BufferedRanges = bufferedRanges,
            PrebufferComplete = file.BitField.PercentComplete > 1.0, // At least 1% buffered
            CurrentBufferedBytes = (long)(file.Length * file.BitField.PercentComplete / 100.0),
            DownloadRate = manager.Monitor.DownloadRate,  // Use DownloadRate instead of deprecated DownloadSpeed
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Removes a torrent and deletes all cached data (ephemeral requirement FR-007a).
    /// </summary>
    public async Task RemoveTorrentAsync(string infoHash, CancellationToken cancellationToken)
    {
        if (!_managers.TryGetValue(infoHash, out var manager))
            return;

        _logger?.LogInformation("Removing torrent {InfoHash}", infoHash);

        // Stop the torrent with timeout for tracker announce
        if (manager.State != MonoTorrent.Client.TorrentState.Stopped &&
            manager.State != MonoTorrent.Client.TorrentState.Stopping)
        {
            _logger?.LogDebug("Stopping torrent {InfoHash}", infoHash);
            await manager.StopAsync(TimeSpan.FromSeconds(10));
        }

        // Remove from engine (this also disposes the manager and deletes cache data)
        await _engine.RemoveAsync(manager, MonoTorrent.Client.RemoveMode.CacheDataOnly | MonoTorrent.Client.RemoveMode.DownloadedDataOnly);

        // Remove from tracking
        _managers.TryRemove(infoHash, out _);
        _metadata.TryRemove(infoHash, out _);

        _logger?.LogInformation("Removed torrent {InfoHash} and deleted cached data", infoHash);
    }

    /// <summary>
    /// Gets metadata for a specific torrent.
    /// </summary>
    public TorrentMetadata? GetTorrentMetadata(string infoHash)
    {
        return _metadata.TryGetValue(infoHash, out var metadata) ? metadata : null;
    }

    /// <summary>
    /// Gets all active torrents.
    /// </summary>
    public IReadOnlyList<TorrentMetadata> GetActiveTorrents()
    {
        return _metadata.Values.ToList();
    }

    /// <summary>
    /// Sets the socket connector for circuit-routed connections (FR-020).
    /// NOTE: This method is deprecated. Pass socketConnector to constructor instead.
    /// MonoTorrent's ClientEngine is configured at creation time and cannot be reconfigured.
    /// </summary>
    [Obsolete("Pass socketConnector to constructor instead. This method has no effect.")]
    public void SetSocketConnector(ISocketConnector socketConnector)
    {
        _logger?.LogWarning(
            "SetSocketConnector called but has no effect. " +
            "Socket connector must be passed to TorrentEngine constructor. " +
            "MonoTorrent's ClientEngine is configured at creation time.");
        _socketConnector = socketConnector;
    }

    /// <summary>
    /// Disposes the engine and all resources.
    /// </summary>
    public void Dispose()
    {
        // Stop all torrents
        _engine.StopAllAsync(TimeSpan.FromSeconds(10)).Wait();

        // Remove all managers (this also disposes them)
        foreach (var manager in _managers.Values.ToList())
        {
            _engine.RemoveAsync(manager, MonoTorrent.Client.RemoveMode.CacheDataOnly | MonoTorrent.Client.RemoveMode.DownloadedDataOnly).Wait();
        }

        // Dispose engine (this also stops DHT)
        _engine.Dispose();

        _managers.Clear();
        _metadata.Clear();
    }

    // Private helper methods

    private TorrentMetadata ConvertToMetadata(Torrent torrent, string magnetLink)
    {
        // Calculate piece indices for each file
        var files = new List<Models.TorrentFile>();
        foreach (var file in torrent.Files)
        {
            files.Add(new Models.TorrentFile
            {
                Path = file.Path,
                Size = file.Length,
                StartPiece = file.StartPieceIndex,
                EndPiece = file.EndPieceIndex
            });
        }

        return new TorrentMetadata
        {
            InfoHash = torrent.InfoHashes.V1OrV2.ToHex(),
            Title = torrent.Name ?? "Unknown",
            Size = torrent.Size,
            PieceLength = torrent.PieceLength,
            TotalPieces = torrent.PieceCount,
            Files = files,
            MagnetLink = magnetLink
        };
    }

    private List<(long Start, long End)> CalculateBufferedRanges(TorrentManager manager, ITorrentManagerFile file)
    {
        var ranges = new List<(long Start, long End)>();

        // Calculate file's piece range within the torrent
        long fileStart = 0;
        foreach (var f in manager.Torrent!.Files)
        {
            if (f == file)
                break;
            fileStart += f.Length;
        }

        long fileEnd = fileStart + file.Length;
        int pieceSize = manager.Torrent.PieceLength;

        int startPiece = (int)(fileStart / pieceSize);
        int endPiece = (int)((fileEnd - 1) / pieceSize);

        // Scan BitField for contiguous ranges
        long? rangeStart = null;
        for (int i = startPiece; i <= endPiece; i++)
        {
            if (manager.Bitfield[i])
            {
                if (rangeStart == null)
                {
                    rangeStart = Math.Max(0, (long)i * pieceSize - fileStart);
                }
            }
            else
            {
                if (rangeStart != null)
                {
                    long rangeEnd = Math.Min(file.Length, (long)i * pieceSize - fileStart);
                    ranges.Add((rangeStart.Value, rangeEnd));
                    rangeStart = null;
                }
            }
        }

        // Close final range if needed
        if (rangeStart != null)
        {
            ranges.Add((rangeStart.Value, file.Length));
        }

        return ranges;
    }

    private void DeleteTorrentFiles(TorrentManager manager)
    {
        try
        {
            if (manager.Torrent == null)
                return;

            // For single-file torrents, delete the file
            if (manager.Torrent.Files.Count == 1)
            {
                string filePath = Path.Combine(_downloadPath, manager.Torrent.Files[0].Path);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            else
            {
                // For multi-file torrents, delete the directory
                string torrentDir = Path.Combine(_downloadPath, manager.Torrent.Name);
                if (Directory.Exists(torrentDir))
                {
                    Directory.Delete(torrentDir, recursive: true);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

