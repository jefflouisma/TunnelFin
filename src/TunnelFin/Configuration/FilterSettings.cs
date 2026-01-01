using TunnelFin.Models;

namespace TunnelFin.Configuration;

/// <summary>
/// Configuration settings for content filtering and search result processing.
/// </summary>
public class FilterSettings
{
    /// <summary>
    /// Whether to enable automatic filtering of search results.
    /// </summary>
    public bool EnableAutoFiltering { get; set; } = true;

    /// <summary>
    /// Default filter profile ID to use when no specific profile is selected.
    /// </summary>
    public Guid? DefaultFilterProfileId { get; set; }

    /// <summary>
    /// Default sort attribute for search results.
    /// </summary>
    public SortAttribute DefaultSortAttribute { get; set; } = SortAttribute.Seeders;

    /// <summary>
    /// Default sort direction for search results.
    /// </summary>
    public SortDirection DefaultSortDirection { get; set; } = SortDirection.Descending;

    /// <summary>
    /// Minimum number of seeders required for a result to be shown (global filter).
    /// </summary>
    public int MinSeeders { get; set; } = 1;

    /// <summary>
    /// Minimum file size in bytes (global filter).
    /// </summary>
    public long MinFileSize { get; set; } = 104857600L; // 100MB

    /// <summary>
    /// Maximum file size in bytes (global filter, 0 = unlimited).
    /// </summary>
    public long MaxFileSize { get; set; } = 0;

    /// <summary>
    /// Whether to automatically filter out low-quality releases (CAM, TS, etc.).
    /// </summary>
    public bool FilterLowQuality { get; set; } = true;

    /// <summary>
    /// List of quality tags to block (e.g., ["CAM", "TS", "HDCAM"]).
    /// </summary>
    public List<string> BlockedQualities { get; set; } = new() { "CAM", "TS", "HDCAM", "HDTS" };

    /// <summary>
    /// Whether to automatically filter out results with suspicious seeder counts (potential honeypots).
    /// </summary>
    public bool FilterSuspiciousSeeders { get; set; } = true;

    /// <summary>
    /// Maximum seeder count before flagging as suspicious (0 = disabled).
    /// </summary>
    public int MaxSuspiciousSeeders { get; set; } = 10000;

    /// <summary>
    /// Whether to prefer results with verified uploaders.
    /// </summary>
    public bool PreferVerifiedUploaders { get; set; } = true;

    /// <summary>
    /// Whether to enable metadata enrichment (TMDB/AniList matching).
    /// </summary>
    public bool EnableMetadataEnrichment { get; set; } = true;

    /// <summary>
    /// Whether to filter out results that don't match metadata.
    /// </summary>
    public bool RequireMetadataMatch { get; set; } = false;

    /// <summary>
    /// Minimum relevance score (0-100) for a result to be shown.
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.0;

    /// <summary>
    /// Whether to deduplicate results with the same info hash.
    /// </summary>
    public bool DeduplicateResults { get; set; } = true;

    /// <summary>
    /// Whether to group results by quality.
    /// </summary>
    public bool GroupByQuality { get; set; } = false;

    /// <summary>
    /// Maximum number of results to show per quality group.
    /// </summary>
    public int MaxResultsPerQualityGroup { get; set; } = 5;

    /// <summary>
    /// Validates the filter settings.
    /// </summary>
    /// <param name="errors">List of validation errors.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (MinSeeders < 0)
            errors.Add("MinSeeders cannot be negative");

        if (MinFileSize < 0)
            errors.Add("MinFileSize cannot be negative");

        if (MaxFileSize < 0)
            errors.Add("MaxFileSize cannot be negative");

        if (MaxFileSize > 0 && MaxFileSize < MinFileSize)
            errors.Add("MaxFileSize must be greater than MinFileSize");

        if (MaxSuspiciousSeeders < 0)
            errors.Add("MaxSuspiciousSeeders cannot be negative");

        if (MinRelevanceScore < 0 || MinRelevanceScore > 100)
            errors.Add("MinRelevanceScore must be between 0 and 100");

        if (MaxResultsPerQualityGroup < 1)
            errors.Add("MaxResultsPerQualityGroup must be at least 1");

        return errors.Count == 0;
    }
}

