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
/// HTML scraper for Nyaa.si torrent indexer.
/// Parses panel-based layout with table rows.
/// </summary>
public class ScraperNyaa
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScraperNyaa>? _logger;
    private const string BaseUrl = "https://nyaa.si";
    private const string SearchUrl = "https://nyaa.si/?f=0&c=0_0&q={0}";

    private static readonly string[] UserAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public ScraperNyaa(HttpClient httpClient, ILogger<ScraperNyaa>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <summary>
    /// Searches Nyaa for content.
    /// </summary>
    public async Task<IReadOnlyList<TorrentResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var encodedQuery = Uri.EscapeDataString(query);
        var url = string.Format(SearchUrl, encodedQuery);

        _logger?.LogDebug("Searching Nyaa: {Url}", url);

        try
        {
            var html = await FetchHtmlAsync(url, cancellationToken);
            var results = ParseSearchResults(html);

            _logger?.LogInformation("Nyaa returned {Count} results for query '{Query}'", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search Nyaa for query '{Query}'", query);
            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Fetches HTML with user-agent rotation.
    /// </summary>
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

    /// <summary>
    /// Parses Nyaa search results from HTML table.
    /// </summary>
    private IReadOnlyList<TorrentResult> ParseSearchResults(string html)
    {
        var results = new List<TorrentResult>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select table rows from torrent-list table (skip header)
            var rows = doc.DocumentNode.SelectNodes("//table[@class='torrent-list']//tr[position()>1]");
            if (rows == null)
                return results;

            foreach (var row in rows)
            {
                try
                {
                    // Extract category, title, and links
                    var titleNode = row.SelectSingleNode(".//td[@colspan='2']//a[not(contains(@class, 'comments'))]");
                    if (titleNode == null)
                        continue;

                    var title = titleNode.InnerText.Trim();
                    
                    // Find magnet link
                    var magnetNode = row.SelectSingleNode(".//a[contains(@href, 'magnet:')]");
                    if (magnetNode == null)
                        continue;

                    var magnetLink = magnetNode.GetAttributeValue("href", "");
                    
                    // Extract size (4th column)
                    var sizeText = row.SelectSingleNode(".//td[4]")?.InnerText.Trim() ?? "0";
                    var size = ParseSize(sizeText);

                    // Extract seeders (6th column)
                    var seedersText = row.SelectSingleNode(".//td[6]")?.InnerText.Trim() ?? "0";
                    int.TryParse(seedersText, out var seeders);

                    // Extract leechers (7th column)
                    var leechersText = row.SelectSingleNode(".//td[7]")?.InnerText.Trim() ?? "0";
                    int.TryParse(leechersText, out var leechers);

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
                        IndexerName = "Nyaa"
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse Nyaa search result row");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse Nyaa HTML");
            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Parses human-readable size to bytes.
    /// </summary>
    private static long ParseSize(string sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return 0;

        var match = Regex.Match(sizeText, @"([\d.]+)\s*([KMGT]?i?B)", RegexOptions.IgnoreCase);
        if (!match.Success)
            return 0;

        if (!double.TryParse(match.Groups[1].Value, out var value))
            return 0;

        var unit = match.Groups[2].Value.ToUpperInvariant();
        var multiplier = unit switch
        {
            "B" => 1L,
            "KIB" => 1024L,
            "MIB" => 1024L * 1024,
            "GIB" => 1024L * 1024 * 1024,
            "TIB" => 1024L * 1024 * 1024 * 1024,
            "KB" => 1000L,
            "MB" => 1000L * 1000,
            "GB" => 1000L * 1000 * 1000,
            "TB" => 1000L * 1000 * 1000 * 1000,
            _ => 1L
        };

        return (long)(value * multiplier);
    }

    /// <summary>
    /// Extracts InfoHash from magnet link.
    /// </summary>
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

