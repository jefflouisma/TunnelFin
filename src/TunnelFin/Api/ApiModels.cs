namespace TunnelFin.Api;

/// <summary>
/// Search response containing torrent results.
/// </summary>
public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public List<TorrentSearchResult> Results { get; set; } = new();
}

/// <summary>
/// Individual torrent search result.
/// </summary>
public class TorrentSearchResult
{
    public string InfoHash { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Seeders { get; set; }
    public int Leechers { get; set; }
    public string? Category { get; set; }
    public string MagnetLink { get; set; } = string.Empty;
    public string? IndexerName { get; set; }
}

/// <summary>
/// Stream session response.
/// </summary>
public class StreamResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string InfoHash { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Health check response.
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

