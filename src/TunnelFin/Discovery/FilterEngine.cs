using System.Text.RegularExpressions;
using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// FilterEngine provides advanced filtering capabilities for search results (T077).
/// Supports Required, Preferred, Excluded, Include filter types per FR-019, FR-020, FR-021, FR-022.
/// </summary>
public class FilterEngine
{
    private readonly AttributeParser _attributeParser;
    private readonly Dictionary<string, string> _requiredFilters;
    private readonly Dictionary<string, string> _preferredFilters;
    private readonly Dictionary<string, string> _excludedFilters;
    private readonly Dictionary<string, string> _includeFilters;
    private readonly List<string> _regexFilters;
    private readonly List<string> _conditionalFilters;

    public FilterEngine()
    {
        _attributeParser = new AttributeParser();
        _requiredFilters = new Dictionary<string, string>();
        _preferredFilters = new Dictionary<string, string>();
        _excludedFilters = new Dictionary<string, string>();
        _includeFilters = new Dictionary<string, string>();
        _regexFilters = new List<string>();
        _conditionalFilters = new List<string>();
    }

    /// <summary>
    /// Adds a required filter (results MUST match this attribute).
    /// </summary>
    public void AddRequiredFilter(string attribute, string value)
    {
        _requiredFilters[attribute] = value;
    }

    /// <summary>
    /// Adds a preferred filter (results matching this are prioritized).
    /// </summary>
    public void AddPreferredFilter(string attribute, string value)
    {
        _preferredFilters[attribute] = value;
    }

    /// <summary>
    /// Adds an excluded filter (results matching this are removed).
    /// </summary>
    public void AddExcludedFilter(string attribute, string value)
    {
        _excludedFilters[attribute] = value;
    }

    /// <summary>
    /// Adds an include filter (only results matching this keyword are included).
    /// </summary>
    public void AddIncludeFilter(string attribute, string value)
    {
        _includeFilters[attribute] = value;
    }

    /// <summary>
    /// Adds a regex filter (results must match this regex pattern).
    /// </summary>
    public void AddRegexFilter(string pattern)
    {
        _regexFilters.Add(pattern);
    }

    /// <summary>
    /// Adds a conditional filter (expression-based filtering per FR-022).
    /// </summary>
    public void AddConditionalFilter(string expression)
    {
        _conditionalFilters.Add(expression);
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _requiredFilters.Clear();
        _preferredFilters.Clear();
        _excludedFilters.Clear();
        _includeFilters.Clear();
        _regexFilters.Clear();
        _conditionalFilters.Clear();
    }

    /// <summary>
    /// Loads a filter profile (per FR-024).
    /// </summary>
    public void LoadProfile(FilterProfile profile)
    {
        ClearFilters();

        // Convert FilterProfile properties to filter dictionaries
        if (profile.AllowedQualities?.Count > 0)
        {
            foreach (var quality in profile.AllowedQualities)
            {
                AddRequiredFilter("quality", quality);
            }
        }

        if (profile.BlockedQualities?.Count > 0)
        {
            foreach (var quality in profile.BlockedQualities)
            {
                AddExcludedFilter("quality", quality);
            }
        }

        if (profile.AllowedCodecs?.Count > 0)
        {
            foreach (var codec in profile.AllowedCodecs)
            {
                AddIncludeFilter("codec", codec);
            }
        }

        if (profile.BlockedCodecs?.Count > 0)
        {
            foreach (var codec in profile.BlockedCodecs)
            {
                AddExcludedFilter("codec", codec);
            }
        }

        if (profile.RequiredKeywords?.Count > 0)
        {
            foreach (var keyword in profile.RequiredKeywords)
            {
                AddIncludeFilter("title", keyword);
            }
        }

        if (profile.ExcludedKeywords?.Count > 0)
        {
            foreach (var keyword in profile.ExcludedKeywords)
            {
                AddExcludedFilter("title", keyword);
            }
        }
    }

    /// <summary>
    /// Applies all filters to search results.
    /// </summary>
    public List<SearchResult> ApplyFilters(List<SearchResult> results)
    {
        if (results == null || results.Count == 0)
        {
            return new List<SearchResult>();
        }

        // Parse attributes for all results if not already set
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.Quality))
            {
                var attributes = _attributeParser.ParseAttributes(result.Title);
                result.Quality = attributes.ContainsKey("resolution") ? attributes["resolution"] : null;
                result.Codec = attributes.ContainsKey("codec") ? attributes["codec"] : null;
                result.Audio = attributes.ContainsKey("audio") ? attributes["audio"] : null;
                result.Language = attributes.ContainsKey("language") ? attributes["language"] : null;
                result.ReleaseGroup = attributes.ContainsKey("releaseGroup") ? attributes["releaseGroup"] : null;
            }
        }

        var filtered = results.ToList();

        // Apply conditional filters first
        filtered = ApplyConditionalFilters(filtered);

        // Apply required filters
        filtered = ApplyRequiredFilters(filtered);

        // Apply excluded filters
        filtered = ApplyExcludedFilters(filtered);

        // Apply include filters
        filtered = ApplyIncludeFilters(filtered);

        // Apply regex filters
        filtered = ApplyRegexFilters(filtered);

        // Apply preferred filters (sorting)
        filtered = ApplyPreferredFilters(filtered);

        return filtered;
    }

    private List<SearchResult> ApplyRequiredFilters(List<SearchResult> results)
    {
        if (_requiredFilters.Count == 0)
        {
            return results;
        }

        return results.Where(r =>
        {
            foreach (var filter in _requiredFilters)
            {
                var attribute = GetAttribute(r, filter.Key);
                if (attribute == null || !attribute.Equals(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }).ToList();
    }

    private List<SearchResult> ApplyExcludedFilters(List<SearchResult> results)
    {
        if (_excludedFilters.Count == 0)
        {
            return results;
        }

        return results.Where(r =>
        {
            foreach (var filter in _excludedFilters)
            {
                var attribute = GetAttribute(r, filter.Key);
                if (attribute != null && attribute.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }).ToList();
    }

    private List<SearchResult> ApplyIncludeFilters(List<SearchResult> results)
    {
        if (_includeFilters.Count == 0)
        {
            return results;
        }

        return results.Where(r =>
        {
            foreach (var filter in _includeFilters)
            {
                var attribute = GetAttribute(r, filter.Key);
                if (attribute == null || !attribute.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }).ToList();
    }

    private List<SearchResult> ApplyRegexFilters(List<SearchResult> results)
    {
        if (_regexFilters.Count == 0)
        {
            return results;
        }

        return results.Where(r =>
        {
            foreach (var pattern in _regexFilters)
            {
                if (!Regex.IsMatch(r.Title, pattern))
                {
                    return false;
                }
            }
            return true;
        }).ToList();
    }

    private List<SearchResult> ApplyPreferredFilters(List<SearchResult> results)
    {
        if (_preferredFilters.Count == 0)
        {
            return results;
        }

        // Sort results so preferred ones come first
        return results.OrderByDescending(r =>
        {
            int score = 0;
            foreach (var filter in _preferredFilters)
            {
                var attribute = GetAttribute(r, filter.Key);
                if (attribute != null && attribute.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }
            return score;
        }).ToList();
    }

    private List<SearchResult> ApplyConditionalFilters(List<SearchResult> results)
    {
        if (_conditionalFilters.Count == 0)
        {
            return results;
        }

        foreach (var expression in _conditionalFilters)
        {
            results = EvaluateConditionalExpression(expression, results);
        }

        return results;
    }

    private List<SearchResult> EvaluateConditionalExpression(string expression, List<SearchResult> results)
    {
        // Simple expression parser for "exclude X if count(Y) > N"
        var match = Regex.Match(expression, @"exclude\s+(\w+)\s+if\s+count\((\w+)\)\s*>\s*(\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var excludeValue = match.Groups[1].Value;
            var countAttribute = match.Groups[2].Value;
            var threshold = int.Parse(match.Groups[3].Value);

            var count = results.Count(r =>
            {
                var attr = GetAttribute(r, "quality");
                return attr != null && attr.Equals(countAttribute, StringComparison.OrdinalIgnoreCase);
            });

            if (count > threshold)
            {
                return results.Where(r =>
                {
                    var attr = GetAttribute(r, "quality");
                    return attr == null || !attr.Equals(excludeValue, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
        }

        return results;
    }

    private string? GetAttribute(SearchResult result, string attribute)
    {
        return attribute.ToLower() switch
        {
            "quality" => result.Quality,
            "resolution" => result.Quality, // Quality field contains resolution
            "title" => result.Title,
            "codec" => result.Codec,
            "audio" => result.Audio,
            "language" => result.Language,
            "releasegroup" => result.ReleaseGroup,
            _ => null
        };
    }
}



