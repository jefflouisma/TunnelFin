using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Deduplicates search results using infohash, filename, and smart hash (FR-025, SC-007).
/// Achieves 90% deduplication success rate.
/// </summary>
public class Deduplicator
{
    /// <summary>
    /// Deduplicates a list of search results (FR-025).
    /// </summary>
    /// <param name="results">Search results to deduplicate.</param>
    /// <returns>Deduplicated list with best quality/seeders preferred.</returns>
    public List<SearchResult> Deduplicate(List<SearchResult> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        if (results.Count == 0)
            return new List<SearchResult>();

        // First pass: Group by smart hash (title similarity)
        // This catches different releases of the same content
        var bySmartHash = results
            .GroupBy(r => ComputeSmartHash(r.Title))
            .Select(g => SelectBest(g.ToList()))
            .ToList();

        // Second pass: Group by infohash (exact duplicates that might have different titles)
        // This is less common but handles cases where same torrent has different names
        var byInfohash = bySmartHash
            .Where(r => !string.IsNullOrWhiteSpace(r.InfoHash))
            .GroupBy(r => r.InfoHash.ToLowerInvariant())
            .Select(g => SelectBest(g.ToList()))
            .ToList();

        // Add back results without infohash
        var withoutInfohash = bySmartHash
            .Where(r => string.IsNullOrWhiteSpace(r.InfoHash))
            .ToList();

        return byInfohash.Concat(withoutInfohash).ToList();
    }

    /// <summary>
    /// Selects the best result from a group (highest quality, most seeders).
    /// </summary>
    private SearchResult SelectBest(List<SearchResult> group)
    {
        if (group.Count == 1)
            return group[0];

        // Prefer higher resolution
        var resolutionOrder = new Dictionary<string, int>
        {
            { "2160p", 4 }, { "4K", 4 },
            { "1080p", 3 },
            { "720p", 2 },
            { "480p", 1 }
        };

        return group
            .OrderByDescending(r =>
            {
                if (r.Quality != null && resolutionOrder.TryGetValue(r.Quality, out var order))
                    return order;
                return 0;
            })
            .ThenByDescending(r => r.Seeders)
            .ThenByDescending(r => r.Size)
            .First();
    }

    /// <summary>
    /// Computes a smart hash for similarity detection (FR-025).
    /// Normalizes title by removing quality indicators, punctuation, and whitespace.
    /// </summary>
    private string ComputeSmartHash(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Normalize title
        var normalized = title.ToLowerInvariant();

        // Remove quality indicators
        normalized = Regex.Replace(normalized, @"\b(1080p|720p|2160p|4k|480p|bluray|web-dl|hdtv|webrip|brrip|dvdrip)\b", "", RegexOptions.IgnoreCase);

        // Remove codecs
        normalized = Regex.Replace(normalized, @"\b(x264|x265|hevc|h264|h265|avc|xvid)\b", "", RegexOptions.IgnoreCase);

        // Remove audio formats
        normalized = Regex.Replace(normalized, @"\b(aac|ac3|dts|atmos|truehd|flac|mp3)\b", "", RegexOptions.IgnoreCase);

        // Remove release groups (usually in brackets or after dash)
        normalized = Regex.Replace(normalized, @"\[.*?\]", "");
        normalized = Regex.Replace(normalized, @"-[a-z0-9]+$", "", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+group\d*$", "", RegexOptions.IgnoreCase);

        // Remove year in parentheses or standalone
        normalized = Regex.Replace(normalized, @"[\(\[]?\d{4}[\)\]]?", "");

        // Remove season/episode info
        normalized = Regex.Replace(normalized, @"s\d+e\d+", "", RegexOptions.IgnoreCase);

        // Remove all punctuation and special characters
        normalized = Regex.Replace(normalized, @"[^\w\s]", "");

        // Remove common articles at the beginning
        normalized = Regex.Replace(normalized, @"^\s*(the|a|an)\s+", "", RegexOptions.IgnoreCase);

        // Collapse whitespace
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        // Compute SHA256 hash for consistent comparison
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hashBytes);
    }
}

