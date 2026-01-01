namespace TunnelFin.Models;

/// <summary>
/// Represents an active torrent stream session.
/// Tracks the state and progress of a torrent being streamed through Jellyfin.
/// </summary>
public class TorrentStream
{
    /// <summary>
    /// Unique identifier for this stream session.
    /// </summary>
    public Guid StreamId { get; set; }

    /// <summary>
    /// The torrent info hash (40-character hex string for SHA-1).
    /// </summary>
    public string InfoHash { get; set; } = string.Empty;

    /// <summary>
    /// The magnet URI used to initialize this stream.
    /// </summary>
    public string MagnetUri { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the torrent stream.
    /// </summary>
    public TorrentStreamState State { get; set; }

    /// <summary>
    /// The anonymity circuit ID used for this stream (if anonymous).
    /// Null if streaming without anonymity (non-anonymous fallback).
    /// </summary>
    public Guid? CircuitId { get; set; }

    /// <summary>
    /// Total size of the torrent in bytes.
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Number of bytes downloaded so far.
    /// </summary>
    public long DownloadedBytes { get; set; }

    /// <summary>
    /// Download progress as a percentage (0-100).
    /// </summary>
    public double ProgressPercentage => TotalSize > 0 ? (DownloadedBytes * 100.0 / TotalSize) : 0;

    /// <summary>
    /// Current download speed in bytes per second.
    /// </summary>
    public long DownloadSpeed { get; set; }

    /// <summary>
    /// Number of connected peers.
    /// </summary>
    public int PeerCount { get; set; }

    /// <summary>
    /// Number of connected seeders.
    /// </summary>
    public int SeederCount { get; set; }

    /// <summary>
    /// Timestamp when the stream was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of the last activity (download progress update).
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// The file path being streamed (for multi-file torrents, this is the selected file).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// The index of the file being streamed (for multi-file torrents).
    /// </summary>
    public int? FileIndex { get; set; }

    /// <summary>
    /// Whether this stream is using anonymous routing.
    /// </summary>
    public bool IsAnonymous => CircuitId.HasValue;

    /// <summary>
    /// Error message if the stream failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

