using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using TunnelFin.Models;

namespace TunnelFin.Indexers.HtmlScrapers;

/// <summary>
/// HTML scraper for EZTV.re torrent indexer.
/// Parses table-based layout with episode information.
/// </summary>
public class ScraperEZTV
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScraperEZTV>? _logger;
    private const string BaseUrl = "https://eztv.re";
    private const string SearchUrl = "https://eztv.re/search/{0}";

    private static readonly string[] UserAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public ScraperEZTV(HttpClient httpClient, ILogger<ScraperEZTV>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task<IReadOnlyList<TorrentResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var encodedQuery = Uri.EscapeDataString(query);
        var url = string.Format(SearchUrl, encodedQuery);

        _logger?.LogDebug("Searching EZTV: {Url}", url);

        try
        {
            var html = await FetchHtmlAsync(url, cancellationToken);
            var results = ParseSearchResults(html);

            _logger?.LogInformation("EZTV returned {Count} results for query '{Query}'", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search EZTV for query '{Query}'", query);
            return Array.Empty<TorrentResult>();
        }
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        var userAgent = UserAgents[Random.Shared.Next(UserAgents.Length)];
        request.Headers.Add("User-Agent", userAgent);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private IReadOnlyList<TorrentResult> ParseSearchResults(string html)
    {
        var results = new List<TorrentResult>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // EZTV uses table.forum_header_border for results
            var rows = doc.DocumentNode.SelectNodes("//table[@class='forum_header_border']//tr[@class='forum_header_border']");
            if (rows == null)
                return results;

            foreach (var row in rows)
            {
                try
                {
                    // Extract title from second column
                    var titleNode = row.SelectSingleNode(".//td[2]//a[@class='epinfo']");
                    if (titleNode == null)
                        continue;

                    var title = titleNode.InnerText.Trim();

                    // Find magnet link
                    var magnetNode = row.SelectSingleNode(".//a[@class='magnet']");
                    if (magnetNode == null)
                        continue;

                    var magnetLink = magnetNode.GetAttributeValue("href", "");

                    // Extract size from 4th column
                    var sizeText = row.SelectSingleNode(".//td[4]")?.InnerText.Trim() ?? "0";
                    var size = ParseSize(sizeText);

                    // Extract seeders from 6th column
                    var seedersText = row.SelectSingleNode(".//td[6]")?.InnerText.Trim() ?? "0";
                    int.TryParse(seedersText, out var seeders);

                    // EZTV doesn't always show leechers
                    int leechers = 0;

                    // Extract InfoHash
                    var infoHash = ExtractInfoHash(magnetLink);
                    if (string.IsNullOrWhiteSpace(infoHash) || infoHash.Length != 40)
                        continue;

                    results.Add(new TorrentResult
                    {
                        Title = title,
                        InfoHash = infoHash.ToLowerInvariant(),
                        MagnetLink = magnetLink,
                        Size = size,
                        Seeders = seeders,
                        Leechers = leechers,
                        IndexerName = "EZTV",
                        Category = "TV"
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse EZTV search result row");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse EZTV HTML");
            return Array.Empty<TorrentResult>();
        }
    }

    private static long ParseSize(string sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return 0;

        var match = Regex.Match(sizeText, @"([\d.]+)\s*([KMGT]?B)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return 0;

        if (!double.TryParse(match.Groups[1].Value, out var value))
            return 0;

        var unit = match.Groups[2].Value.ToUpperInvariant();
        var multiplier = unit switch
        {
            "B" => 1L,
            "KB" => 1024L,
            "MB" => 1024L * 1024,
            "GB" => 1024L * 1024 * 1024,
            "TB" => 1024L * 1024 * 1024 * 1024,
            _ => 1L
        };

        return (long)(value * multiplier);
    }

    private static string? ExtractInfoHash(string magnetLink)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
            return null;

        var urnIndex = magnetLink.IndexOf("urn:btih:", StringComparison.OrdinalIgnoreCase);
        if (urnIndex < 0)
            return null;

        var hashStart = urnIndex + 9;
        var hashEnd = magnetLink.IndexOf('&', hashStart);
        if (hashEnd < 0)
            hashEnd = magnetLink.Length;

        return magnetLink.Substring(hashStart, hashEnd - hashStart);
    }
}

