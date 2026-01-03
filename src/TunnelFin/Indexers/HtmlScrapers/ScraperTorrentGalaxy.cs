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
/// HTML scraper for TorrentGalaxy.to torrent indexer.
/// Parses card-based div container layout.
/// </summary>
public class ScraperTorrentGalaxy
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScraperTorrentGalaxy>? _logger;
    private const string BaseUrl = "https://torrentgalaxy.to";
    private const string SearchUrl = "https://torrentgalaxy.to/torrents.php?search={0}";

    private static readonly string[] UserAgents = {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
    };

    public ScraperTorrentGalaxy(HttpClient httpClient, ILogger<ScraperTorrentGalaxy>? logger = null)
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

        _logger?.LogDebug("Searching TorrentGalaxy: {Url}", url);

        try
        {
            var html = await FetchHtmlAsync(url, cancellationToken);
            var results = ParseSearchResults(html);

            _logger?.LogInformation("TorrentGalaxy returned {Count} results for query '{Query}'", results.Count, query);

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search TorrentGalaxy for query '{Query}'", query);
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

            // TorrentGalaxy uses div.tgxtablerow for each result
            var rows = doc.DocumentNode.SelectNodes("//div[contains(@class, 'tgxtablerow')]");
            if (rows == null)
                return results;

            foreach (var row in rows)
            {
                try
                {
                    // Extract title
                    var titleNode = row.SelectSingleNode(".//div[contains(@class, 'tgxtablecell')]//a[@title]");
                    if (titleNode == null)
                        continue;

                    var title = titleNode.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(title))
                        title = titleNode.InnerText.Trim();

                    // Find magnet link
                    var magnetNode = row.SelectSingleNode(".//a[contains(@href, 'magnet:')]");
                    if (magnetNode == null)
                        continue;

                    var magnetLink = magnetNode.GetAttributeValue("href", "");

                    // Extract size
                    var sizeNode = row.SelectSingleNode(".//span[contains(@class, 'badge-secondary')]");
                    var sizeText = sizeNode?.InnerText.Trim() ?? "0";
                    var size = ParseSize(sizeText);

                    // Extract seeders and leechers
                    var seedersNode = row.SelectSingleNode(".//span[@title='Seeders/Leechers']/font[@color='green']");
                    var leechersNode = row.SelectSingleNode(".//span[@title='Seeders/Leechers']/font[@color='#ff0000']");
                    
                    int.TryParse(seedersNode?.InnerText.Trim() ?? "0", out var seeders);
                    int.TryParse(leechersNode?.InnerText.Trim() ?? "0", out var leechers);

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
                        IndexerName = "TorrentGalaxy"
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse TorrentGalaxy search result row");
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse TorrentGalaxy HTML");
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

