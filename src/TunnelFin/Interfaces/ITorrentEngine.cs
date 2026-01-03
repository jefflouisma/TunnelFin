using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Connections;
using TunnelFin.Models;

namespace TunnelFin.BitTorrent;

/// <summary>
/// Manages BitTorrent torrent lifecycle and streaming operations.
/// Wraps MonoTorrent's ClientEngine with TunnelFin-specific functionality.
/// </summary>
public interface ITorrentEngine
{
    /// <summary>
    /// Adds a torrent from a magnet link and begins downloading metadata.
    /// </summary>
    /// <param name="magnetLink">Magnet URI (magnet:?xt=urn:btih:...)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TorrentMetadata once metadata is downloaded</returns>
    Task<TorrentMetadata> AddTorrentAsync(string magnetLink, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a seekable stream for a specific file within a torrent.
    /// Uses MonoTorrent's StreamProvider API with sequential piece downloading.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent (40-char hex)</param>
    /// <param name="filePath">Relative path of file within torrent</param>
    /// <param name="prebuffer">Whether to prebuffer before returning stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Seekable stream for HTTP range requests</returns>
    Task<Stream> CreateStreamAsync(string infoHash, string filePath, bool prebuffer, CancellationToken cancellationToken);

    /// <summary>
    /// Gets current buffer status for an active stream.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent</param>
    /// <param name="filePath">Relative path of file within torrent</param>
    /// <returns>Buffer status or null if stream not active</returns>
    BufferStatus? GetBufferStatus(string infoHash, string filePath);

    /// <summary>
    /// Removes a torrent and deletes all cached data.
    /// Called when stream ends or times out.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveTorrentAsync(string infoHash, CancellationToken cancellationToken);

    /// <summary>
    /// Gets metadata for a torrent if it exists.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent</param>
    /// <returns>TorrentMetadata or null if not found</returns>
    TorrentMetadata? GetTorrentMetadata(string infoHash);

    /// <summary>
    /// Gets all active torrents.
    /// </summary>
    /// <returns>List of active torrent metadata</returns>
    IReadOnlyList<TorrentMetadata> GetActiveTorrents();

    /// <summary>
    /// Configures the socket connector for peer connections.
    /// Used to route connections through Tribler circuits.
    /// </summary>
    /// <param name="socketConnector">Custom socket connector (e.g., TunnelSocketConnector)</param>
    void SetSocketConnector(ISocketConnector socketConnector);
}

