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
using TunnelFin.Networking;
using TunnelFin.Streaming;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Jellyfin IChannel implementation for TunnelFin.
/// Presents torrent search results as native Jellyfin library items.
/// Implements IRequiresMediaInfoCallback for media source resolution.
/// Implements ISupportsLatestMedia to show recent content in Jellyfin's "Latest" section.
/// </summary>
public class TunnelFinChannel : IChannel, IRequiresMediaInfoCallback, ISupportsLatestMedia
{
    private readonly IIndexerManager _indexerManager;
    private readonly IStreamManager _streamManager;
    private readonly ITorrentEngine _torrentEngine;
    private readonly NetworkAvailabilityService? _networkAvailabilityService;
    private readonly ILogger<TunnelFinChannel>? _logger;
    private bool _isNetworkAvailable;

    public TunnelFinChannel(
        IIndexerManager indexerManager,
        IStreamManager streamManager,
        ITorrentEngine torrentEngine,
        ILogger<TunnelFinChannel>? logger = null,
        NetworkAvailabilityService? networkAvailabilityService = null)
    {
        _indexerManager = indexerManager ?? throw new ArgumentNullException(nameof(indexerManager));
        _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        _torrentEngine = torrentEngine ?? throw new ArgumentNullException(nameof(torrentEngine));
        _logger = logger;
        _networkAvailabilityService = networkAvailabilityService;
        _isNetworkAvailable = false;

        // Subscribe to network availability changes (T116)
        if (_networkAvailabilityService != null)
        {
            _networkAvailabilityService.StatusChanged += OnNetworkAvailabilityChanged;
        }
    }

    /// <summary>
    /// Handles network availability status changes (T116).
    /// Updates UI indicator (green/orange play button) based on circuit availability.
    /// </summary>
    private void OnNetworkAvailabilityChanged(object? sender, bool isAvailable)
    {
        _isNetworkAvailable = isAvailable;
        var status = isAvailable ? "🟢 AVAILABLE" : "🟠 UNAVAILABLE";
        _logger?.LogInformation("Network availability changed: {Status} ({CircuitCount} circuits)",
            status,
            _networkAvailabilityService?.AvailableCircuitCount ?? 0);
    }

    // IChannel properties
    public string Name => "TunnelFin";
    public string Description
    {
        get
        {
            var baseDescription = "Decentralized streaming via BitTorrent with Tribler anonymity";
            if (_networkAvailabilityService != null)
            {
                var status = _isNetworkAvailable ? "🟢 Network Available" : "🟠 Network Unavailable";
                var circuitCount = _networkAvailabilityService.AvailableCircuitCount;
                return $"{baseDescription} | {status} ({circuitCount} circuits)";
            }
            return baseDescription;
        }
    }
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

    /// <summary>
    /// ISupportsLatestMedia implementation - returns recent/popular content for Jellyfin's "Latest" section.
    /// Searches for popular open-source test content that's likely to have seeders.
    /// </summary>
    public async Task<IEnumerable<ChannelItemInfo>> GetLatestMedia(ChannelLatestMediaSearch request, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("GetLatestMedia called for user: {UserId}", request.UserId);

        try
        {
            // Search for popular open-source content that's likely to be available
            var results = await _indexerManager.SearchAsync("big buck bunny", cancellationToken);

            _logger?.LogInformation("GetLatestMedia found {Count} results", results.Count);

            // Return top 10 results
            return results.Take(10).Select(ToChannelItem);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting latest media");
            return Array.Empty<ChannelItemInfo>();
        }
    }

    // Predefined search categories for quick access
    private static readonly (string Id, string Name, string SearchTerm, string Overview)[] SearchCategories = new[]
    {
        ("search_help", "🔍 Search for Content...", "", "Click to learn how to search for any content"),
        ("cat_movies_popular", "🎬 Popular Movies", "1080p BluRay", "Search for popular HD movies"),
        ("cat_movies_4k", "🎬 4K Movies", "2160p UHD", "Search for 4K Ultra HD movies"),
        ("cat_tv_shows", "📺 TV Shows", "S01E01 720p", "Search for TV show episodes"),
        ("cat_documentaries", "🎥 Documentaries", "documentary 1080p", "Search for documentaries"),
        ("cat_anime", "🎌 Anime", "anime 1080p", "Search for anime content"),
        ("cat_big_buck_bunny", "🐰 Big Buck Bunny (Test)", "big buck bunny", "Open source test content - Big Buck Bunny"),
        ("cat_sintel", "🐉 Sintel (Test)", "sintel 1080p", "Open source test content - Sintel"),
    };

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        // FolderId is used for navigation - can be a GUID, magnet link, or encoded search query
        var folderId = query.FolderId;

        _logger?.LogInformation("GetChannelItems called with FolderId: {FolderId}", folderId);

        // If no FolderId, return root level with search categories
        if (string.IsNullOrWhiteSpace(folderId))
        {
            _logger?.LogDebug("Empty folder ID, returning search categories");
            return GetRootCategories();
        }

        // Check if FolderId is a search category
        var category = SearchCategories.FirstOrDefault(c => c.Id == folderId);
        if (!string.IsNullOrEmpty(category.Id))
        {
            // Handle search help category specially
            if (category.Id == "search_help")
            {
                _logger?.LogInformation("Showing search help");
                return GetSearchHelpResult();
            }

            _logger?.LogInformation("FolderId is category '{Name}', searching for: {SearchTerm}", category.Name, category.SearchTerm);
            return await SearchAndReturnResultsAsync(category.SearchTerm, cancellationToken);
        }

        // Check if FolderId is a magnet link (multi-file navigation - T090)
        if (folderId.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation("FolderId is a magnet link, checking for multi-file torrent");
            return await GetTorrentFilesAsync(folderId, cancellationToken);
        }

        // Check if FolderId is an encoded search query (from TunnelFinSearchProvider or API)
        if (folderId.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
        {
            var searchTerm = Uri.UnescapeDataString(folderId.Substring(7));
            _logger?.LogInformation("FolderId is encoded search query: {SearchTerm}", searchTerm);
            return await SearchAndReturnResultsAsync(searchTerm, cancellationToken);
        }

        // Otherwise, treat FolderId as a direct search query (for backward compatibility)
        // Note: This path may not work if Jellyfin validates FolderId as GUID
        _logger?.LogInformation("FolderId treated as direct search query: {Query}", folderId);
        return await SearchAndReturnResultsAsync(folderId, cancellationToken);
    }

    /// <summary>
    /// Returns root-level search categories for the channel UI.
    /// </summary>
    private ChannelItemResult GetRootCategories()
    {
        var items = SearchCategories.Select(cat => new ChannelItemInfo
        {
            Id = cat.Id,
            Name = cat.Name,
            Overview = cat.Overview,
            Type = ChannelItemType.Folder,
            FolderType = ChannelFolderType.Container
        }).ToList();

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    /// <summary>
    /// Returns help information for custom searching.
    /// </summary>
    private ChannelItemResult GetSearchHelpResult()
    {
        var networkStatus = _isNetworkAvailable ? "🟢 Anonymous Network Available" : "🟠 Direct Connection Only";

        var items = new List<ChannelItemInfo>
        {
            new ChannelItemInfo
            {
                Id = "help_how_to_search",
                Name = "📖 How to Search",
                Overview = $@"To search for any content, use the URL parameter format:

?folderId=search:your+search+term

Examples:
• search:inception 2010
• search:breaking bad s01e01
• search:big buck bunny 1080p

Or use the TunnelFin Search page in Dashboard → Plugins → TunnelFin → Search

Current Network Status: {networkStatus}",
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container
            },
            new ChannelItemInfo
            {
                Id = "help_network_status",
                Name = networkStatus,
                Overview = _isNetworkAvailable
                    ? $"🟢 Your connection is routed through {_networkAvailabilityService?.AvailableCircuitCount ?? 0} anonymous circuits. Your IP address is hidden from peers."
                    : "🟠 No anonymous circuits available. Streaming will expose your IP address to peers. Consider waiting for circuits to establish.",
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container
            },
            new ChannelItemInfo
            {
                Id = "search:big buck bunny",
                Name = "🔍 Try: Big Buck Bunny",
                Overview = "Click to search for 'Big Buck Bunny' - a free open source test video",
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container
            },
            new ChannelItemInfo
            {
                Id = "search:sintel",
                Name = "🔍 Try: Sintel",
                Overview = "Click to search for 'Sintel' - another free open source test video",
                Type = ChannelItemType.Folder,
                FolderType = ChannelFolderType.Container
            }
        };

        return new ChannelItemResult
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    /// <summary>
    /// Performs a search and returns results as channel items.
    /// </summary>
    private async Task<ChannelItemResult> SearchAndReturnResultsAsync(string searchTerm, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _indexerManager.SearchAsync(searchTerm, cancellationToken);
            _logger?.LogInformation("Found {Count} results for query: {Query}", results.Count, searchTerm);

            var items = results.Select(ToChannelItem).ToList();
            return new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error searching for: {Query}", searchTerm);
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

        // Extract provider IDs for metadata enrichment (T089 + Phase 4)
        var providerIds = new Dictionary<string, string>();

        // Prefer TMDB-enriched IDs, fallback to title extraction
        var imdbId = result.ImdbId ?? ExtractImdbId(result.Title);
        var tmdbId = result.TmdbId?.ToString() ?? ExtractTmdbId(result.Title);

        if (!string.IsNullOrEmpty(imdbId))
            providerIds["Imdb"] = imdbId;

        if (!string.IsNullOrEmpty(tmdbId))
            providerIds["Tmdb"] = tmdbId;

        // Build overview with network status and torrent info (Search UX Phase 1)
        var networkStatus = _isNetworkAvailable ? "🟢 Anonymous" : "🟠 Direct";
        var overviewParts = new List<string>
        {
            networkStatus,
            $"Size: {FormatBytes(result.Size)}",
            $"Seeders: {result.Seeders ?? 0}",
            $"Leechers: {result.Leechers ?? 0}"
        };

        // Add source indexer
        if (!string.IsNullOrEmpty(result.IndexerName))
            overviewParts.Add($"Source: {result.IndexerName}");

        // Add TMDB rating and year (Phase 4)
        if (result.TmdbRating.HasValue)
            overviewParts.Add($"⭐ {result.TmdbRating:F1}");

        if (result.Year.HasValue)
            overviewParts.Add($"📅 {result.Year}");

        var overview = string.Join(" | ", overviewParts);

        // Add TMDB overview if available (Phase 4)
        if (!string.IsNullOrEmpty(result.TmdbOverview))
        {
            var truncatedOverview = result.TmdbOverview.Length > 200
                ? result.TmdbOverview[..200] + "..."
                : result.TmdbOverview;
            overview += $"\n{truncatedOverview}";
        }

        // Add zero seeders warning (T122)
        if (result.Seeders == 0)
        {
            overview += "\n⚠️ WARNING: No seeders available - download may not start";
        }

        // Use magnet link as Id for multi-file navigation support (T090)
        // Format: "magnet:?xt=urn:btih:..." allows GetChannelItems to detect multi-file torrents
        return new ChannelItemInfo
        {
            Id = result.MagnetLink,
            Name = result.Title,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Video,
            ContentType = contentType,
            Overview = overview,
            CommunityRating = result.TmdbRating ?? CalculateCommunityRating(result.Seeders, result.Leechers),
            DateCreated = result.DiscoveredAt,
            ImageUrl = result.PosterUrl, // Use TMDB poster if available (Phase 4)
            MediaSources = new List<MediaSourceInfo>(),
            ProviderIds = providerIds
        };
    }

    /// <summary>
    /// IRequiresMediaInfoCallback implementation - provides media sources for channel items.
    /// </summary>
    public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("GetChannelItemMediaInfo called for Id: {Id}", id);

        try
        {
            // Id can be either a magnet link or "infoHash:filePath" format
            string infoHash;
            string filePath;

            if (id.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                // Extract InfoHash from magnet link
                infoHash = ExtractInfoHashFromMagnet(id);
                filePath = string.Empty;

                // Add torrent if not already added (needed to get metadata)
                await _torrentEngine.AddTorrentAsync(id, cancellationToken);
            }
            else if (id.Contains(':') && !id.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
            {
                // Format: "infoHash:filePath" (from multi-file navigation)
                var parts = id.Split(':', 2);
                infoHash = parts[0];
                filePath = parts[1];
            }
            else
            {
                // Plain InfoHash
                infoHash = id;
                filePath = string.Empty;
            }

            _logger?.LogDebug("Resolved InfoHash: {InfoHash}, FilePath: {FilePath}", infoHash, filePath);

            // Create stream session and get URL
            var session = await _streamManager.CreateSessionAsync(infoHash, filePath, null, cancellationToken);
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
            _logger?.LogError(ex, "Error getting media info for Id: {Id}", id);
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

