namespace TunnelFin.Models;

/// <summary>
/// Represents a single file within a multi-file torrent.
/// Lifecycle: Created when torrent metadata is parsed, deleted when parent torrent is removed.
/// Storage: In-memory as part of TorrentMetadata.
/// </summary>
public class TorrentFile
{
    /// <summary>
    /// Relative path within torrent (e.g., "Season 1/Episode 01.mkv").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Index of first piece containing this file's data.
    /// </summary>
    public required int StartPiece { get; init; }

    /// <summary>
    /// Index of last piece containing this file's data.
    /// </summary>
    public required int EndPiece { get; init; }

    /// <summary>
    /// MIME type (e.g., "video/x-matroska", "video/mp4").
    /// </summary>
    public string? MediaType { get; set; }

    /// <summary>
    /// Validates the torrent file according to specification rules.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException("Path must not be empty", nameof(Path));

        if (Path.Contains(".."))
            throw new ArgumentException("Path must not contain '..' (directory traversal)", nameof(Path));

        if (Size <= 0)
            throw new ArgumentException("Size must be positive", nameof(Size));

        if (StartPiece > EndPiece)
            throw new ArgumentException("StartPiece must be <= EndPiece", nameof(StartPiece));

        if (MediaType != null && !MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("MediaType should be video/* for streamable content", nameof(MediaType));
    }
}

