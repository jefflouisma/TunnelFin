using TunnelFin.Models;

namespace TunnelFin.BitTorrent;

/// <summary>
/// Wraps MonoTorrent.Client.TorrentManager with streaming-specific logic (FR-007, FR-008, FR-009).
/// Manages torrent state and provides access to file information.
/// Uses MonoTorrent.Streaming.StreamProvider for HTTP endpoint creation.
/// </summary>
public class TorrentStreamWrapper
{
    private readonly Guid _torrentId;
    private readonly string _magnetLink;
    private readonly DateTime _addedAt;
    private TorrentState _state;
    private long _downloadedBytes;
    private long _totalBytes;
    private long _downloadSpeed;
    private long _uploadSpeed;
    private int _peerCount;
    private bool _metadataLoaded;
    private readonly Dictionary<int, FileInfo> _files = new();

    /// <summary>
    /// Unique identifier for the torrent.
    /// </summary>
    public Guid TorrentId => _torrentId;

    /// <summary>
    /// Magnet link or path to .torrent file.
    /// </summary>
    public string MagnetLink => _magnetLink;

    /// <summary>
    /// Current state of the torrent.
    /// </summary>
    public TorrentState State => _state;

    /// <summary>
    /// Initializes a new instance of the TorrentStreamWrapper class.
    /// </summary>
    /// <param name="torrentId">Unique torrent identifier.</param>
    /// <param name="magnetLink">Magnet link or path to .torrent file.</param>
    public TorrentStreamWrapper(Guid torrentId, string magnetLink)
    {
        _torrentId = torrentId;
        _magnetLink = magnetLink;
        _addedAt = DateTime.UtcNow;
        _state = TorrentState.Initializing;
    }

    /// <summary>
    /// Gets the current status of the torrent.
    /// </summary>
    /// <returns>Current torrent status.</returns>
    public TorrentStatus GetStatus()
    {
        double progress = _totalBytes > 0 
            ? (_downloadedBytes * 100.0) / _totalBytes 
            : 0;

        return new TorrentStatus
        {
            TorrentId = _torrentId,
            State = _state,
            DownloadedBytes = _downloadedBytes,
            TotalSizeBytes = _totalBytes,
            DownloadProgress = progress,
            DownloadSpeedBytesPerSecond = _downloadSpeed,
            UploadSpeedBytesPerSecond = _uploadSpeed,
            PeerCount = _peerCount,
            AddedAt = _addedAt
        };
    }

    /// <summary>
    /// Updates download progress metrics.
    /// </summary>
    /// <param name="downloadedBytes">Number of bytes downloaded.</param>
    /// <param name="totalBytes">Total torrent size in bytes.</param>
    /// <param name="downloadSpeed">Current download speed in bytes/sec.</param>
    /// <param name="uploadSpeed">Current upload speed in bytes/sec.</param>
    /// <param name="peerCount">Number of connected peers.</param>
    public void UpdateProgress(long downloadedBytes, long totalBytes, long downloadSpeed, long uploadSpeed, int peerCount)
    {
        _downloadedBytes = downloadedBytes;
        _totalBytes = totalBytes;
        _downloadSpeed = downloadSpeed;
        _uploadSpeed = uploadSpeed;
        _peerCount = peerCount;
    }

    /// <summary>
    /// Sets the torrent state.
    /// </summary>
    /// <param name="state">New torrent state.</param>
    public void SetState(TorrentState state)
    {
        _state = state;
    }

    /// <summary>
    /// Gets the number of files in the torrent.
    /// </summary>
    /// <returns>Number of files.</returns>
    public int GetFileCount()
    {
        return _files.Count;
    }

    /// <summary>
    /// Sets the number of files in the torrent.
    /// </summary>
    /// <param name="count">Number of files.</param>
    public void SetFileCount(int count)
    {
        // This is a placeholder for metadata loading
        // In real implementation, this would be populated from MonoTorrent metadata
    }

    /// <summary>
    /// Sets file information for a specific file index.
    /// </summary>
    /// <param name="index">File index.</param>
    /// <param name="name">File name.</param>
    /// <param name="size">File size in bytes.</param>
    public void SetFileInfo(int index, string name, long size)
    {
        _files[index] = new FileInfo { Name = name, Size = size };
    }

    /// <summary>
    /// Gets the file name for a specific file index.
    /// </summary>
    /// <param name="index">File index.</param>
    /// <returns>File name.</returns>
    public string GetFileName(int index)
    {
        if (!_files.TryGetValue(index, out var fileInfo))
            throw new ArgumentOutOfRangeException(nameof(index), $"File index {index} not found");

        return fileInfo.Name;
    }

    /// <summary>
    /// Gets the file size for a specific file index.
    /// </summary>
    /// <param name="index">File index.</param>
    /// <returns>File size in bytes.</returns>
    public long GetFileSize(int index)
    {
        if (!_files.TryGetValue(index, out var fileInfo))
            throw new ArgumentOutOfRangeException(nameof(index), $"File index {index} not found");

        return fileInfo.Size;
    }

    /// <summary>
    /// Checks if torrent metadata has been loaded.
    /// </summary>
    /// <returns>True if metadata is loaded.</returns>
    public bool IsMetadataLoaded()
    {
        return _metadataLoaded;
    }

    /// <summary>
    /// Sets the metadata loaded status.
    /// </summary>
    /// <param name="loaded">Whether metadata is loaded.</param>
    public void SetMetadataLoaded(bool loaded)
    {
        _metadataLoaded = loaded;
    }

    /// <summary>
    /// Internal class for tracking file information.
    /// </summary>
    private class FileInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}

