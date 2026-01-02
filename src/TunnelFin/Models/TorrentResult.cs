namespace TunnelFin.Models;

/// <summary>
/// Represents a search result from an indexer (Torznab or HTML scraper).
/// Lifecycle: Created when search results are returned, converted to TorrentMetadata when selected.
/// Storage: In-memory, ephemeral (discarded after user selection).
/// </summary>
public class TorrentResult
{
    /// <summary>
    /// Human-readable title from indexer.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// SHA-1 hash of torrent info dictionary (40-char hex, lowercase).
    /// </summary>
    public required string InfoHash { get; init; }

    /// <summary>
    /// Magnet URI for torrent (magnet:?xt=urn:btih:...).
    /// </summary>
    public required string MagnetLink { get; init; }

    /// <summary>
    /// Total size in bytes (from indexer).
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Number of seeders (from indexer, may be stale).
    /// </summary>
    public int? Seeders { get; init; }

    /// <summary>
    /// Number of leechers (from indexer, may be stale).
    /// </summary>
    public int? Leechers { get; init; }

    /// <summary>
    /// Name of indexer that provided this result.
    /// </summary>
    public required string IndexerName { get; init; }

    /// <summary>
    /// Timestamp when result was discovered.
    /// </summary>
    public DateTime DiscoveredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional category from indexer (e.g., "Movies", "TV").
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Validates the torrent result according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title))
            throw new ArgumentException("Title must not be empty", nameof(Title));

        if (string.IsNullOrWhiteSpace(InfoHash) || InfoHash.Length != 40 || !IsHexString(InfoHash))
            throw new ArgumentException("InfoHash must be exactly 40 hexadecimal characters (lowercase)", nameof(InfoHash));

        if (!MagnetLink.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MagnetLink must start with 'magnet:?xt=urn:btih:'", nameof(MagnetLink));

        if (Size <= 0)
            throw new ArgumentException("Size must be positive", nameof(Size));

        if (string.IsNullOrWhiteSpace(IndexerName))
            throw new ArgumentException("IndexerName must not be empty", nameof(IndexerName));
    }

    private static bool IsHexString(string value)
    {
        return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }
}

