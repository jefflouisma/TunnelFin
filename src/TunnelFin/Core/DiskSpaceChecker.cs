using Microsoft.Extensions.Logging;

namespace TunnelFin.Core;

/// <summary>
/// Checks available disk space before starting downloads (T121).
/// Warns if less than 2x torrent size is available.
/// </summary>
public class DiskSpaceChecker
{
    private readonly ILogger? _logger;

    public DiskSpaceChecker(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if sufficient disk space is available for a torrent download.
    /// </summary>
    /// <param name="downloadPath">Path where torrent will be downloaded.</param>
    /// <param name="torrentSize">Size of the torrent in bytes.</param>
    /// <param name="multiplier">Safety multiplier (default: 2x torrent size).</param>
    /// <returns>True if sufficient space available, false otherwise.</returns>
    public bool HasSufficientSpace(string downloadPath, long torrentSize, double multiplier = 2.0)
    {
        if (string.IsNullOrWhiteSpace(downloadPath))
            throw new ArgumentException("Download path cannot be empty", nameof(downloadPath));

        if (torrentSize < 0)
            throw new ArgumentException("Torrent size cannot be negative", nameof(torrentSize));

        if (multiplier < 1.0)
            throw new ArgumentException("Multiplier must be at least 1.0", nameof(multiplier));

        try
        {
            // Get drive info for the download path
            var driveInfo = new DriveInfo(Path.GetPathRoot(downloadPath) ?? downloadPath);

            if (!driveInfo.IsReady)
            {
                _logger?.LogWarning("Drive not ready for path: {Path}", downloadPath);
                return false;
            }

            var availableSpace = driveInfo.AvailableFreeSpace;
            var requiredSpace = (long)(torrentSize * multiplier);

            _logger?.LogDebug(
                "Disk space check: Available={Available} bytes, Required={Required} bytes ({Multiplier}x {TorrentSize})",
                availableSpace, requiredSpace, multiplier, torrentSize);

            if (availableSpace < requiredSpace)
            {
                _logger?.LogWarning(
                    "Insufficient disk space: Available={Available} bytes, Required={Required} bytes (torrent: {TorrentSize} bytes)",
                    availableSpace, requiredSpace, torrentSize);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking disk space for path: {Path}", downloadPath);
            // Return true to allow download attempt (fail-open for disk space checks)
            return true;
        }
    }

    /// <summary>
    /// Gets available disk space in bytes for a given path.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <returns>Available space in bytes, or -1 if unable to determine.</returns>
    public long GetAvailableSpace(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);
            return driveInfo.IsReady ? driveInfo.AvailableFreeSpace : -1;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting available space for path: {Path}", path);
            return -1;
        }
    }

    /// <summary>
    /// Formats bytes into human-readable string (e.g., "1.5 GB").
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Gets a user-friendly warning message for insufficient disk space.
    /// </summary>
    /// <param name="availableSpace">Available space in bytes.</param>
    /// <param name="requiredSpace">Required space in bytes.</param>
    /// <returns>Warning message.</returns>
    public static string GetInsufficientSpaceMessage(long availableSpace, long requiredSpace)
    {
        return $"Insufficient disk space. Available: {FormatBytes(availableSpace)}, Required: {FormatBytes(requiredSpace)}";
    }
}

