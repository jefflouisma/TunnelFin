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
/// HTML scraper for 1337x.to torrent indexer.
/// Parses table-based layout with class selectors.
/// </summary>
public class Scraper1337x
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Scraper1337x>? _logger;
    private const string BaseUrl = "https://1337x.to";
    private const string SearchUrl = "https://1337x.to/search/{0}/1/";

    private static readonly string[] UserAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public Scraper1337x(HttpClient httpClient, ILogger<Scraper1337x>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <summary>
    /// Searches 1337x for content.
    /// </summary>
    public async Task<IReadOnlyList<TorrentResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        var encodedQuery = Uri.EscapeDataString(query);
        var url = string.Format(SearchUrl, encodedQuery);

        _logger?.LogDebug("Searching 1337x: {Url}", url);

        try
        {
            // Fetch HTML with random user-agent
            var html = await FetchHtmlAsync(url, cancellationToken);

            // Parse search results
            var results = await ParseSearchResultsAsync(html, cancellationToken);

            _logger?.LogInformation("1337x returned {Count} results for query '{Query}'", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search 1337x for query '{Query}'", query);
            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Fetches HTML with user-agent rotation.
    /// </summary>
    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // Rotate user-agent
        var userAgent = UserAgents[Random.Shared.Next(UserAgents.Length)];
        request.Headers.Add("User-Agent", userAgent);
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.5");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Parses 1337x search results from HTML table.
    /// </summary>
    private async Task<IReadOnlyList<TorrentResult>> ParseSearchResultsAsync(string html, CancellationToken cancellationToken)
    {
        var results = new List<TorrentResult>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select table rows (skip header row)
            var rows = doc.DocumentNode.SelectNodes("//table[@class='table-list']//tr[position()>1]");
            if (rows == null)
                return results;

            foreach (var row in rows)
            {
                try
                {
                    // Extract title and detail link
                    var titleNode = row.SelectSingleNode(".//td[@class='coll-1 name']//a[2]");
                    if (titleNode == null)
                        continue;

                    var title = titleNode.InnerText.Trim();
                    var detailLink = titleNode.GetAttributeValue("href", "");

                    // Extract seeders and leechers
                    var seedersText = row.SelectSingleNode(".//td[@class='coll-2']")?.InnerText.Trim() ?? "0";
                    var leechersText = row.SelectSingleNode(".//td[@class='coll-3']")?.InnerText.Trim() ?? "0";
                    var sizeText = row.SelectSingleNode(".//td[@class='coll-4']")?.InnerText.Trim() ?? "0";

                    int.TryParse(seedersText, out var seeders);
                    int.TryParse(leechersText, out var leechers);
                    var size = ParseSize(sizeText);

                    // Fetch magnet link from detail page
                    if (!string.IsNullOrWhiteSpace(detailLink))
                    {
                        var fullDetailUrl = detailLink.StartsWith("http") ? detailLink : BaseUrl + detailLink;
                        var magnetLink = await FetchMagnetLinkAsync(fullDetailUrl, cancellationToken);

                        if (!string.IsNullOrWhiteSpace(magnetLink))
                        {
                            // Extract InfoHash from magnet link
                            var infoHash = ExtractInfoHash(magnetLink);
                            if (!string.IsNullOrWhiteSpace(infoHash) && infoHash.Length == 40)
                            {
                                results.Add(new TorrentResult
                                {
                                    Title = title,
                                    InfoHash = infoHash.ToLowerInvariant(),
                                    MagnetLink = magnetLink,
                                    Size = size,
                                    Seeders = seeders,
                                    Leechers = leechers,
                                    IndexerName = "1337x"
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse 1337x search result row");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse 1337x HTML");
            return Array.Empty<TorrentResult>();
        }
    }

    /// <summary>
    /// Parses human-readable size (e.g., "1.5 GB") to bytes.
    /// </summary>
    private static long ParseSize(string sizeText)
    {
        if (string.IsNullOrWhiteSpace(sizeText))
            return 0;

        // Match pattern like "1.5 GB" or "500 MB"
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

    /// <summary>
    /// Fetches magnet link from 1337x detail page.
    /// </summary>
    private async Task<string?> FetchMagnetLinkAsync(string detailUrl, CancellationToken cancellationToken)
    {
        try
        {
            var html = await FetchHtmlAsync(detailUrl, cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find magnet link in detail page
            var magnetNode = doc.DocumentNode.SelectSingleNode("//a[starts-with(@href, 'magnet:')]");
            return magnetNode?.GetAttributeValue("href", null);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch magnet link from {Url}", detailUrl);
            return null;
        }
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
