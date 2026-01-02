using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;
using TunnelFin.BitTorrent;
using TunnelFin.Indexers;
using TunnelFin.Models;
using TunnelFin.Streaming;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Jellyfin IChannel implementation for TunnelFin.
/// Presents torrent search results as native Jellyfin library items.
/// </summary>
public class TunnelFinChannel : IChannel
{
    private readonly IIndexerManager _indexerManager;
    private readonly IStreamManager _streamManager;
    private readonly ITorrentEngine _torrentEngine;
    private readonly ILogger<TunnelFinChannel>? _logger;

    public TunnelFinChannel(
        IIndexerManager indexerManager,
        IStreamManager streamManager,
        ITorrentEngine torrentEngine,
        ILogger<TunnelFinChannel>? logger = null)
    {
        _indexerManager = indexerManager ?? throw new ArgumentNullException(nameof(indexerManager));
        _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        _torrentEngine = torrentEngine ?? throw new ArgumentNullException(nameof(torrentEngine));
        _logger = logger;
    }

    // IChannel properties
    public string Name => "TunnelFin";
    public string Description => "Decentralized streaming via BitTorrent with Tribler anonymity";
    public string DataVersion => "1.0.0";
    public string HomePageUrl => "https://github.com/jefflouisma/TunnelFin";
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            ContentTypes = new List<ChannelMediaContentType>
            {
                ChannelMediaContentType.Movie,
                ChannelMediaContentType.Episode,
                ChannelMediaContentType.Clip
            },
            MediaTypes = new List<ChannelMediaType>
            {
                ChannelMediaType.Video
            },
            SupportsContentDownloading = false,
            SupportsSortOrderToggle = true,
            DefaultSortFields = new List<ChannelItemSortField>
            {
                ChannelItemSortField.CommunityRating,
                ChannelItemSortField.Name,
                ChannelItemSortField.DateCreated
            }
        };
    }

    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        // Return null - no custom channel image
        return Task.FromResult<DynamicImageResponse>(null!);
    }

    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return Array.Empty<ImageType>();
    }

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        // Use FolderId as search term (Jellyfin channels use FolderId for navigation)
        var searchTerm = query.FolderId;

        _logger?.LogInformation("GetChannelItems called with FolderId: {FolderId}", searchTerm);

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            _logger?.LogDebug("Empty folder ID, returning empty result");
            return new ChannelItemResult
            {
                Items = new List<ChannelItemInfo>(),
                TotalRecordCount = 0
            };
        }

        try
        {
            // Check if FolderId is a magnet link (multi-file navigation - T090)
            if (searchTerm.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("FolderId is a magnet link, checking for multi-file torrent");
                return await GetTorrentFilesAsync(searchTerm, cancellationToken);
            }

            // Otherwise, treat as search query
            var results = await _indexerManager.SearchAsync(searchTerm, cancellationToken);

            _logger?.LogInformation("Found {Count} results for query: {Query}", results.Count, searchTerm);

            // Convert to ChannelItemInfo
            var items = results.Select(ToChannelItem).ToList();

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting channel items for query: {Query}", searchTerm);
            return new ChannelItemResult
            {
                Items = new List<ChannelItemInfo>(),
                TotalRecordCount = 0
            };
        }
    }

    private async Task<ChannelItemResult> GetTorrentFilesAsync(string folderIdOrMagnet, CancellationToken cancellationToken)
    {
        try
        {
            // FolderId format for multi-file navigation: "magnet:?xt=urn:btih:..."
            // We need the magnet link to add the torrent and get metadata
            if (!folderIdOrMagnet.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogWarning("Invalid FolderId format for multi-file torrent: {FolderId}", folderIdOrMagnet);
                return new ChannelItemResult
                {
                    Items = new List<ChannelItemInfo>(),
                    TotalRecordCount = 0
                };
            }

            // Add the torrent to get metadata (if not already added)
            var metadata = await _torrentEngine.AddTorrentAsync(folderIdOrMagnet, cancellationToken);

            if (metadata.Files.Count <= 1)
            {
                // Single file torrent, return empty (user should play directly)
                _logger?.LogDebug("Torrent {InfoHash} has only {Count} file(s), returning empty", metadata.InfoHash, metadata.Files.Count);
                return new ChannelItemResult
                {
                    Items = new List<ChannelItemInfo>(),
                    TotalRecordCount = 0
                };
            }

            _logger?.LogInformation("Torrent {InfoHash} has {Count} files, creating items for video files", metadata.InfoHash, metadata.Files.Count);

            // Multi-file torrent, create items for each video file
            var items = metadata.Files
                .Where(f => IsVideoFile(f.Path))
                .Select(f => new ChannelItemInfo
                {
                    Id = $"{metadata.InfoHash}:{f.Path}",
                    Name = Path.GetFileName(f.Path),
                    Type = ChannelItemType.Media,
                    MediaType = ChannelMediaType.Video,
                    ContentType = ChannelMediaContentType.Movie,
                    Overview = $"Size: {FormatBytes(f.Size)}",
                    DateCreated = metadata.CreatedAt,
                    MediaSources = new List<MediaSourceInfo>()
                })
                .ToList();

            _logger?.LogInformation("Created {Count} video file items for torrent {InfoHash}", items.Count, metadata.InfoHash);

            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting torrent files for FolderId: {FolderId}", folderIdOrMagnet);
            return new ChannelItemResult
            {
                Items = new List<ChannelItemInfo>(),
                TotalRecordCount = 0
            };
        }
    }

    private static bool IsVideoFile(string path)
    {
        var videoExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg" };
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return videoExtensions.Contains(extension);
    }

    private static bool IsHexString(string value)
    {
        return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    public ChannelItemInfo ToChannelItem(TorrentResult result)
    {
        var contentType = DetermineContentType(result.Category);

        // Extract provider IDs for metadata enrichment (T089)
        var providerIds = new Dictionary<string, string>();
        var imdbId = ExtractImdbId(result.Title);
        var tmdbId = ExtractTmdbId(result.Title);

        if (!string.IsNullOrEmpty(imdbId))
            providerIds["Imdb"] = imdbId;

        if (!string.IsNullOrEmpty(tmdbId))
            providerIds["Tmdb"] = tmdbId;

        // Use magnet link as Id for multi-file navigation support (T090)
        // Format: "magnet:?xt=urn:btih:..." allows GetChannelItems to detect multi-file torrents
        return new ChannelItemInfo
        {
            Id = result.MagnetLink,
            Name = result.Title,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            ContentType = contentType,
            Overview = $"Size: {FormatBytes(result.Size)} | Seeders: {result.Seeders ?? 0} | Leechers: {result.Leechers ?? 0}",
            CommunityRating = CalculateCommunityRating(result.Seeders, result.Leechers),
            DateCreated = result.DiscoveredAt,
            ImageUrl = null, // No poster URL in TorrentResult
            MediaSources = new List<MediaSourceInfo>(),
            ProviderIds = providerIds
        };
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string itemId, string? filePath, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("GetChannelItemMediaInfo called for ItemId: {ItemId}, FilePath: {FilePath}", itemId, filePath);

        try
        {
            // ItemId can be either a magnet link or "infoHash:filePath" format
            string infoHash;
            string actualFilePath;

            if (itemId.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                // Extract InfoHash from magnet link
                infoHash = ExtractInfoHashFromMagnet(itemId);
                actualFilePath = filePath ?? string.Empty;

                // Add torrent if not already added (needed to get metadata)
                await _torrentEngine.AddTorrentAsync(itemId, cancellationToken);
            }
            else if (itemId.Contains(':'))
            {
                // Format: "infoHash:filePath" (from multi-file navigation)
                var parts = itemId.Split(':', 2);
                infoHash = parts[0];
                actualFilePath = parts[1];
            }
            else
            {
                // Plain InfoHash
                infoHash = itemId;
                actualFilePath = filePath ?? string.Empty;
            }

            _logger?.LogDebug("Resolved InfoHash: {InfoHash}, FilePath: {FilePath}", infoHash, actualFilePath);

            // Create stream session and get URL
            var session = await _streamManager.CreateSessionAsync(infoHash, actualFilePath, null, cancellationToken);
            var streamUrl = _streamManager.GetStreamUrl(session.SessionId);

            var mediaSource = new MediaSourceInfo
            {
                Id = infoHash,
                Protocol = MediaProtocol.Http,
                Path = streamUrl,
                Type = MediaSourceType.Default,
                SupportsDirectStream = true,
                SupportsDirectPlay = false,
                SupportsTranscoding = true
            };

            return new[] { mediaSource };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting media info for ItemId: {ItemId}", itemId);
            return Array.Empty<MediaSourceInfo>();
        }
    }

    private static string ExtractInfoHashFromMagnet(string magnetLink)
    {
        // Extract InfoHash from magnet link: magnet:?xt=urn:btih:INFOHASH&...
        var match = Regex.Match(magnetLink, @"xt=urn:btih:([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.ToLowerInvariant();

        throw new ArgumentException($"Invalid magnet link format: {magnetLink}");
    }

    public bool IsEnabledFor(string userId)
    {
        return true; // Available for all users
    }

    // Helper methods

    private static ChannelMediaContentType DetermineContentType(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return ChannelMediaContentType.Movie;

        var categoryLower = category.ToLowerInvariant();

        if (categoryLower.Contains("tv") || categoryLower.Contains("episode") || categoryLower.Contains("series"))
            return ChannelMediaContentType.Episode;

        if (categoryLower.Contains("anime"))
            return ChannelMediaContentType.Movie; // Treat anime as movies for now

        return ChannelMediaContentType.Movie;
    }

    private static float? CalculateCommunityRating(int? seeders, int? leechers)
    {
        if (!seeders.HasValue || !leechers.HasValue)
            return null;

        // Calculate rating based on seeder/leecher ratio
        // 10 = excellent (100+ seeders, high ratio)
        // 5 = average (10-100 seeders)
        // 1 = poor (< 10 seeders)

        var total = seeders.Value + leechers.Value;
        if (total == 0)
            return 1.0f;

        var ratio = (float)seeders.Value / total;

        if (seeders.Value >= 100)
            return Math.Min(10.0f, 7.0f + (ratio * 3.0f));
        else if (seeders.Value >= 50)
            return Math.Min(8.0f, 6.0f + (ratio * 2.0f));
        else if (seeders.Value >= 10)
            return Math.Min(6.0f, 4.0f + (ratio * 2.0f));
        else
            return Math.Min(4.0f, 1.0f + (ratio * 3.0f));
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string? ExtractImdbId(string title)
    {
        // Try to extract IMDB ID from title (e.g., "Movie Name (2020) [tt1234567]")
        var match = Regex.Match(title, @"\[?(tt\d{7,8})\]?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractTmdbId(string title)
    {
        // Try to extract TMDB ID from title (e.g., "Movie Name (2020) [tmdb-12345]")
        var match = Regex.Match(title, @"\[?tmdb-(\d+)\]?", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}

