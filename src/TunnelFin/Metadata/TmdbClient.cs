using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TunnelFin.Models;

namespace TunnelFin.Metadata;

/// <summary>
/// TMDB client for enriching torrent results with metadata (Phase 4: Rich Metadata Integration).
/// Uses TMDB API v3 for movie/TV metadata lookup.
/// </summary>
public interface ITmdbClient
{
    /// <summary>
    /// Enriches torrent results with TMDB metadata (poster, overview, rating).
    /// </summary>
    Task<IReadOnlyList<TorrentResult>> EnrichResultsAsync(
        IReadOnlyList<TorrentResult> results,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// TMDB API client implementation.
/// </summary>
public class TmdbClient : ITmdbClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TmdbClient>? _logger;
    private readonly string? _apiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500";

    // Regex patterns for title parsing
    private static readonly Regex YearPattern = new(@"[\.\s\(]*(19|20)\d{2}[\.\s\)]*", RegexOptions.Compiled);
    private static readonly Regex TvPattern = new(@"[Ss](\d{1,2})[Ee](\d{1,2})", RegexOptions.Compiled);
    private static readonly Regex QualityPattern = new(@"(720p|1080p|2160p|4K|HDRip|BluRay|WEB-?DL|HDTV)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public TmdbClient(HttpClient httpClient, ILogger<TmdbClient>? logger = null, string? apiKey = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("TMDB_API_KEY");
    }

    public async Task<IReadOnlyList<TorrentResult>> EnrichResultsAsync(
        IReadOnlyList<TorrentResult> results,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger?.LogDebug("TMDB API key not configured, skipping metadata enrichment");
            return results;
        }

        var enrichedResults = new List<TorrentResult>();

        foreach (var result in results)
        {
            try
            {
                var enriched = await EnrichSingleResultAsync(result, cancellationToken);
                enrichedResults.Add(enriched);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to enrich result: {Title}", result.Title);
                enrichedResults.Add(result);
            }
        }

        return enrichedResults;
    }

    private async Task<TorrentResult> EnrichSingleResultAsync(TorrentResult result, CancellationToken ct)
    {
        var (cleanTitle, year) = ParseTitle(result.Title);
        var isTV = TvPattern.IsMatch(result.Title);

        var searchUrl = isTV
            ? $"{BaseUrl}/search/tv?api_key={_apiKey}&query={Uri.EscapeDataString(cleanTitle)}"
            : $"{BaseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(cleanTitle)}";

        if (year.HasValue && !isTV)
            searchUrl += $"&year={year}";

        var response = await _httpClient.GetAsync(searchUrl, ct);
        if (!response.IsSuccessStatusCode)
            return result;

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var resultsArray = doc.RootElement.GetProperty("results");

        if (resultsArray.GetArrayLength() == 0)
            return result;

        var first = resultsArray[0];

        // Extract metadata
        result.TmdbId = first.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : null;
        result.Year = year ?? ExtractYear(first, isTV);
        result.TmdbRating = first.TryGetProperty("vote_average", out var voteProp) ? (float?)voteProp.GetSingle() : null;
        result.TmdbOverview = first.TryGetProperty("overview", out var overviewProp) ? overviewProp.GetString() : null;

        if (first.TryGetProperty("poster_path", out var posterProp) && posterProp.ValueKind == JsonValueKind.String)
        {
            var posterPath = posterProp.GetString();
            if (!string.IsNullOrEmpty(posterPath))
                result.PosterUrl = ImageBaseUrl + posterPath;
        }

        // Fetch external IDs for IMDB
        if (result.TmdbId.HasValue)
        {
            var externalUrl = isTV
                ? $"{BaseUrl}/tv/{result.TmdbId}/external_ids?api_key={_apiKey}"
                : $"{BaseUrl}/movie/{result.TmdbId}/external_ids?api_key={_apiKey}";

            var extResponse = await _httpClient.GetAsync(externalUrl, ct);
            if (extResponse.IsSuccessStatusCode)
            {
                var extJson = await extResponse.Content.ReadAsStringAsync(ct);
                using var extDoc = JsonDocument.Parse(extJson);
                if (extDoc.RootElement.TryGetProperty("imdb_id", out var imdbProp))
                    result.ImdbId = imdbProp.GetString();
            }
        }

        return result;
    }

    private static (string cleanTitle, int? year) ParseTitle(string title)
    {
        var yearMatch = YearPattern.Match(title);
        int? year = null;
        if (yearMatch.Success && int.TryParse(yearMatch.Value.Trim('.', ' ', '(', ')'), out var y))
            year = y;

        // Remove quality tags and year
        var clean = QualityPattern.Replace(title, "");
        clean = YearPattern.Replace(clean, " ");
        clean = TvPattern.Replace(clean, "");
        clean = clean.Replace('.', ' ').Replace('_', ' ');
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        return (clean, year);
    }

    private static int? ExtractYear(JsonElement element, bool isTV)
    {
        var dateProp = isTV ? "first_air_date" : "release_date";
        if (element.TryGetProperty(dateProp, out var datePropVal) && datePropVal.ValueKind == JsonValueKind.String)
        {
            var dateStr = datePropVal.GetString();
            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4 && int.TryParse(dateStr[..4], out var year))
                return year;
        }
        return null;
    }
}

