using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using TunnelFin.Models;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Jellyfin IChannel implementation for TunnelFin.
/// Presents torrent search results as native Jellyfin library items.
/// </summary>
public interface ITunnelFinChannel : IChannel
{
    /// <summary>
    /// Gets channel items (search results) for display in Jellyfin UI.
    /// Called when user navigates to TunnelFin channel or performs search.
    /// </summary>
    /// <param name="query">Channel item query with search term and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Channel items representing torrent search results</returns>
    Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all channel features supported by TunnelFin.
    /// </summary>
    /// <returns>Channel features (search, media playback)</returns>
    ChannelFeatures GetChannelFeatures();

    /// <summary>
    /// Converts a TorrentResult to a ChannelItemInfo for Jellyfin display.
    /// </summary>
    /// <param name="result">Torrent search result</param>
    /// <returns>Channel item info with media sources</returns>
    ChannelItemInfo ToChannelItem(TorrentResult result);

    /// <summary>
    /// Gets media sources for a specific torrent.
    /// Called when user selects a torrent for playback.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent</param>
    /// <param name="filePath">Relative path of file within torrent (for multi-file torrents)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Media source info with HTTP stream URL</returns>
    Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string infoHash, string filePath, CancellationToken cancellationToken);
}

