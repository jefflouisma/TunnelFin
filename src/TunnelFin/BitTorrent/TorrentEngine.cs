using System.Collections.Concurrent;
using TunnelFin.Models;

namespace TunnelFin.BitTorrent;

/// <summary>
/// Manages torrent downloads using MonoTorrent library (FR-007, FR-008, FR-012, FR-013, FR-014, FR-015).
/// Handles torrent initialization, piece prioritization, and resource limits.
/// </summary>
public class TorrentEngine
{
    private readonly ConcurrentDictionary<Guid, TorrentInfo> _torrents = new();
    private readonly int _maxConcurrentStreams;
    private readonly long _maxCacheSizeBytes;

    /// <summary>
    /// Maximum number of concurrent streams allowed (FR-013).
    /// </summary>
    public int MaxConcurrentStreams => _maxConcurrentStreams;

    /// <summary>
    /// Maximum cache size in bytes (FR-014).
    /// </summary>
    public long MaxCacheSizeBytes => _maxCacheSizeBytes;

    /// <summary>
    /// Initializes a new instance of the TorrentEngine class.
    /// </summary>
    /// <param name="maxConcurrentStreams">Maximum concurrent streams (default: 3).</param>
    /// <param name="maxCacheSizeBytes">Maximum cache size in bytes (default: 10GB).</param>
    public TorrentEngine(int maxConcurrentStreams = 3, long maxCacheSizeBytes = 10_000_000_000)
    {
        if (maxConcurrentStreams < 1)
            throw new ArgumentException("Max concurrent streams must be at least 1", nameof(maxConcurrentStreams));
        if (maxCacheSizeBytes < 0)
            throw new ArgumentException("Max cache size must be non-negative", nameof(maxCacheSizeBytes));

        _maxConcurrentStreams = maxConcurrentStreams;
        _maxCacheSizeBytes = maxCacheSizeBytes;
    }

    /// <summary>
    /// Adds a torrent from a magnet link or .torrent file (FR-007).
    /// </summary>
    /// <param name="magnetLinkOrPath">Magnet link or path to .torrent file.</param>
    /// <returns>Unique identifier for the torrent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when resource limits are exceeded (FR-015).</exception>
    public async Task<Guid> AddTorrentAsync(string magnetLinkOrPath)
    {
        if (string.IsNullOrWhiteSpace(magnetLinkOrPath))
            throw new ArgumentException("Magnet link or path cannot be empty", nameof(magnetLinkOrPath));

        // Check concurrent stream limit (FR-013, FR-015)
        if (_torrents.Count >= _maxConcurrentStreams)
        {
            throw new InvalidOperationException(
                $"Cannot add torrent: maximum concurrent streams ({_maxConcurrentStreams}) reached");
        }

        // TODO: Check cache size limit (FR-014, FR-015)
        // This would require estimating torrent size from metadata

        var torrentId = Guid.NewGuid();
        var info = new TorrentInfo
        {
            TorrentId = torrentId,
            MagnetLinkOrPath = magnetLinkOrPath,
            AddedAt = DateTime.UtcNow,
            State = TorrentState.Initializing
        };

        _torrents[torrentId] = info;

        // TODO: Initialize MonoTorrent manager
        // var manager = await _engine.AddStreamingAsync(torrent, downloadPath);
        // info.Manager = manager;

        return torrentId;
    }

    /// <summary>
    /// Gets the current status of a torrent (FR-011).
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <returns>Current torrent status.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when torrent ID is not found.</exception>
    public TorrentStatus GetTorrentStatus(Guid torrentId)
    {
        if (!_torrents.TryGetValue(torrentId, out var info))
        {
            throw new KeyNotFoundException($"Torrent {torrentId} not found");
        }

        return new TorrentStatus
        {
            TorrentId = torrentId,
            DownloadProgress = info.DownloadProgress,
            DownloadSpeedBytesPerSecond = info.DownloadSpeedBytesPerSecond,
            UploadSpeedBytesPerSecond = info.UploadSpeedBytesPerSecond,
            PeerCount = info.PeerCount,
            TotalSizeBytes = info.TotalSizeBytes,
            DownloadedBytes = info.DownloadedBytes,
            State = info.State,
            AddedAt = info.AddedAt
        };
    }

    /// <summary>
    /// Removes a torrent and cleans up resources.
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    public async Task RemoveTorrentAsync(Guid torrentId)
    {
        if (_torrents.TryRemove(torrentId, out var info))
        {
            // TODO: Stop MonoTorrent manager
            // await info.Manager.StopAsync();
            // await _engine.RemoveAsync(info.Manager);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Gets the number of active torrents.
    /// </summary>
    /// <returns>Number of active torrents.</returns>
    public int GetActiveTorrentCount()
    {
        return _torrents.Count;
    }

    /// <summary>
    /// Gets the current cache size in bytes (FR-014).
    /// </summary>
    /// <returns>Total cache size in bytes.</returns>
    public long GetCacheSizeBytes()
    {
        // TODO: Calculate actual disk usage
        // For now, return sum of downloaded bytes
        return _torrents.Values.Sum(t => t.DownloadedBytes);
    }

    /// <summary>
    /// Internal class for tracking torrent information.
    /// </summary>
    private class TorrentInfo
    {
        public Guid TorrentId { get; set; }
        public string MagnetLinkOrPath { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public TorrentState State { get; set; }
        public double DownloadProgress { get; set; }
        public long DownloadSpeedBytesPerSecond { get; set; }
        public long UploadSpeedBytesPerSecond { get; set; }
        public int PeerCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public long DownloadedBytes { get; set; }
        // TODO: Add MonoTorrent.Client.TorrentManager reference
        // public TorrentManager? Manager { get; set; }
    }
}

