namespace TunnelFin.Models;

/// <summary>
/// Represents metadata for a torrent discovered from indexers or parsed from magnet links.
/// Lifecycle: Created when search results are returned, persists while torrent is active, deleted when stream ends.
/// Storage: In-memory dictionary keyed by InfoHash.
/// </summary>
public class TorrentMetadata
{
    /// <summary>
    /// SHA-1 hash of torrent info dictionary (40-char hex, lowercase).
    /// </summary>
    public required string InfoHash { get; init; }

    /// <summary>
    /// Human-readable title from indexer or torrent metadata.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Total size in bytes of all files in torrent.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Number of seeders (from indexer, may be stale).
    /// </summary>
    public int? Seeders { get; set; }

    /// <summary>
    /// Number of leechers (from indexer, may be stale).
    /// </summary>
    public int? Leechers { get; set; }

    /// <summary>
    /// Magnet URI for torrent (magnet:?xt=urn:btih:...).
    /// </summary>
    public required string MagnetLink { get; init; }

    /// <summary>
    /// List of files in torrent with paths and sizes.
    /// </summary>
    public required List<TorrentFile> Files { get; init; }

    /// <summary>
    /// Size of each piece in bytes (typically 256KB-4MB).
    /// </summary>
    public required int PieceLength { get; init; }

    /// <summary>
    /// Total number of pieces in torrent.
    /// </summary>
    public required int TotalPieces { get; init; }

    /// <summary>
    /// Timestamp when metadata was discovered.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Name of indexer that provided this result.
    /// </summary>
    public string? IndexerSource { get; set; }

    /// <summary>
    /// Validates the torrent metadata according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InfoHash) || InfoHash.Length != 40 || !IsHexString(InfoHash))
            throw new ArgumentException("InfoHash must be exactly 40 hexadecimal characters (lowercase)", nameof(InfoHash));

        if (Size <= 0)
            throw new ArgumentException("Size must be positive", nameof(Size));

        if (!MagnetLink.StartsWith("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MagnetLink must start with 'magnet:?xt=urn:btih:'", nameof(MagnetLink));

        if (Files == null || Files.Count == 0)
            throw new ArgumentException("Files list must not be empty", nameof(Files));

        if (!IsPowerOfTwo(PieceLength) || PieceLength < 16 * 1024 || PieceLength > 16 * 1024 * 1024)
            throw new ArgumentException("PieceLength must be power of 2 between 16KB and 16MB", nameof(PieceLength));
    }

    private static bool IsHexString(string value)
    {
        return value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }
}

