using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// SortEngine provides multi-criteria sorting for search results (T081).
/// Supports sorting by quality, seeders, size, upload date per FR-023.
/// Must complete in <1s for 100+ results per SC-005.
/// </summary>
public class SortEngine
{
    private SortAttribute[] _criteria = new[] { SortAttribute.Seeders };
    private SortDirection[] _directions = new[] { SortDirection.Descending };

    /// <summary>
    /// Sets a single sort criterion.
    /// </summary>
    public void SetSortCriteria(SortAttribute criteria, SortDirection direction)
    {
        _criteria = new[] { criteria };
        _directions = new[] { direction };
    }

    /// <summary>
    /// Sets multiple sort criteria (applied in order).
    /// </summary>
    public void SetSortCriteria(SortAttribute[] criteria, SortDirection[] directions)
    {
        if (criteria.Length != directions.Length)
        {
            throw new ArgumentException("Criteria and directions arrays must have the same length");
        }

        _criteria = criteria;
        _directions = directions;
    }

    /// <summary>
    /// Sorts search results based on configured criteria.
    /// </summary>
    public List<SearchResult> Sort(List<SearchResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return new List<SearchResult>();
        }

        IOrderedEnumerable<SearchResult>? ordered = null;

        for (int i = 0; i < _criteria.Length; i++)
        {
            var criterion = _criteria[i];
            var direction = _directions[i];

            if (ordered == null)
            {
                ordered = direction == SortDirection.Ascending
                    ? results.OrderBy(r => GetSortValue(r, criterion))
                    : results.OrderByDescending(r => GetSortValue(r, criterion));
            }
            else
            {
                ordered = direction == SortDirection.Ascending
                    ? ordered.ThenBy(r => GetSortValue(r, criterion))
                    : ordered.ThenByDescending(r => GetSortValue(r, criterion));
            }
        }

        return ordered?.ToList() ?? results;
    }

    private object GetSortValue(SearchResult result, SortAttribute criteria)
    {
        return criteria switch
        {
            SortAttribute.Seeders => result.Seeders,
            SortAttribute.Quality => GetQualityScore(result.Quality),
            SortAttribute.Size => result.Size,
            SortAttribute.UploadDate => result.UploadedAt ?? DateTime.MinValue,
            SortAttribute.Relevance => result.RelevanceScore,
            SortAttribute.Title => result.Title,
            _ => 0
        };
    }

    private int GetQualityScore(string? quality)
    {
        if (quality == null)
        {
            return 0;
        }

        // Higher score = better quality
        return quality.ToLower() switch
        {
            "2160p" => 4,
            "1080p" => 3,
            "720p" => 2,
            "480p" => 1,
            _ => 0
        };
    }
}

