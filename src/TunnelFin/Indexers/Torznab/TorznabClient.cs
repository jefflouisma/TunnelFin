using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using TunnelFin.Configuration;
using TunnelFin.Models;

namespace TunnelFin.Indexers.Torznab;

/// <summary>
/// HTTP client for Torznab-compatible indexers (e.g., Jackett, Prowlarr).
/// Implements rate limiting (1 req/sec) and exponential backoff for 429/503 errors.
/// Parses RSS 2.0 + Torznab XML extensions.
/// </summary>
public class TorznabClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TorznabClient>? _logger;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(1);

    public TorznabClient(HttpClient httpClient, ILogger<TorznabClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        _rateLimiter = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Searches a Torznab indexer for content.
    /// </summary>
    /// <param name="config">Indexer configuration with BaseUrl and ApiKey</param>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of torrent results</returns>
    public async Task<IReadOnlyList<TorrentResult>> SearchAsync(
        IndexerConfig config,
        string query,
        CancellationToken cancellationToken)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        // Build Torznab query URL
        var url = BuildQueryUrl(config, query);

        _logger?.LogDebug("Searching Torznab indexer {IndexerName}: {Url}", config.Name, url);

        // Apply rate limiting (1 req/sec)
        await _rateLimiter.WaitAsync(cancellationToken);
        try
        {
            // Fetch XML with exponential backoff
            var xml = await FetchWithRetryAsync(url, cancellationToken);

            // Parse Torznab response
            var results = ParseTorznabResponse(xml, config.Name);

            _logger?.LogInformation("Torznab indexer {IndexerName} returned {Count} results for query '{Query}'",
                config.Name, results.Count, query);

            return results;
        }
        finally
        {
            // Release rate limiter after delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(_rateLimitDelay, CancellationToken.None);
                _rateLimiter.Release();
            });
        }
    }

    /// <summary>
    /// Builds Torznab query URL with API key and search parameters.
    /// Supports both Jackett-style (baseUrl/api) and Prowlarr-style (baseUrl/{id}/api) URLs.
    /// If baseUrl already ends with /api, appends query params directly.
    /// </summary>
    private string BuildQueryUrl(IndexerConfig config, string query)
    {
        var baseUrl = config.BaseUrl.TrimEnd('/');
        var apiKey = config.ApiKey ?? string.Empty;
        var encodedQuery = Uri.EscapeDataString(query);

        // If URL already contains /api path (Prowlarr-style), append query params directly
        // Otherwise append /api (Jackett-style)
        var separator = baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? "?" : "/api?";

        return $"{baseUrl}{separator}t=search&q={encodedQuery}&apikey={apiKey}";
    }

    /// <summary>
    /// Fetches XML from URL with exponential backoff for 429/503 errors.
    /// Retry delays: 1s, 2s, 4s, 8s (max 60s total).
    /// </summary>
    private async Task<string> FetchWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        int maxRetries = 4;
        int delaySeconds = 1;

        while (true)
        {
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);

                // Handle rate limiting (429) and service unavailable (503)
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (retryCount >= maxRetries)
                    {
                        throw new HttpRequestException(
                            $"Torznab indexer returned {response.StatusCode} after {maxRetries} retries");
                    }

                    _logger?.LogWarning(
                        "Torznab indexer returned {StatusCode}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                        response.StatusCode, delaySeconds, retryCount + 1, maxRetries);

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                    delaySeconds *= 2; // Exponential backoff
                    retryCount++;
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (HttpRequestException) when (retryCount < maxRetries)
            {
                _logger?.LogWarning(
                    "HTTP request failed, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    delaySeconds, retryCount + 1, maxRetries);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds *= 2;
                retryCount++;
            }
        }
    }

    /// <summary>
    /// Parses Torznab XML response (RSS 2.0 + torznab:attr extensions).
    /// </summary>
    private IReadOnlyList<TorrentResult> ParseTorznabResponse(string xml, string indexerName)
    {
        var results = new List<TorrentResult>();

        try
        {
            var doc = XDocument.Parse(xml);
            var ns = XNamespace.Get("http://torznab.com/schemas/2015/feed");

            // Parse RSS items
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                try
                {
                    var title = item.Element("title")?.Value;
                    var link = item.Element("link")?.Value;
                    var guid = item.Element("guid")?.Value;
                    var pubDate = item.Element("pubDate")?.Value;

                    // Prowlarr puts magnet link in <guid>, download URL in <link>
                    // Prefer magnet link from guid if available
                    var magnetLink = guid?.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) == true
                        ? guid
                        : (link?.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) == true ? link : null);

                    // Parse torznab:attr elements
                    var attrs = item.Elements(ns + "attr")
                        .ToDictionary(
                            a => a.Attribute("name")?.Value ?? string.Empty,
                            a => a.Attribute("value")?.Value ?? string.Empty
                        );

                    // Extract required fields - need title and either magnet or infohash
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    // Parse size (bytes)
                    long size = 0;
                    if (attrs.TryGetValue("size", out var sizeStr))
                        long.TryParse(sizeStr, out size);

                    // Parse seeders
                    int seeders = 0;
                    if (attrs.TryGetValue("seeders", out var seedersStr))
                        int.TryParse(seedersStr, out seeders);

                    // Parse leechers
                    int leechers = 0;
                    if (attrs.TryGetValue("peers", out var peersStr))
                        int.TryParse(peersStr, out leechers);

                    // Extract InfoHash - prefer torznab:attr, fallback to magnet link parsing
                    string? infoHash = null;
                    if (attrs.TryGetValue("infohash", out var attrInfoHash) && !string.IsNullOrWhiteSpace(attrInfoHash))
                    {
                        infoHash = attrInfoHash.ToUpperInvariant();
                    }
                    else if (!string.IsNullOrWhiteSpace(magnetLink))
                    {
                        var urnIndex = magnetLink.IndexOf("urn:btih:", StringComparison.OrdinalIgnoreCase);
                        if (urnIndex >= 0)
                        {
                            var hashStart = urnIndex + 9;
                            var hashEnd = magnetLink.IndexOf('&', hashStart);
                            if (hashEnd < 0)
                                hashEnd = magnetLink.Length;

                            infoHash = magnetLink.Substring(hashStart, hashEnd - hashStart).ToUpperInvariant();
                        }
                    }

                    // Skip if InfoHash is missing or invalid
                    if (string.IsNullOrWhiteSpace(infoHash) || infoHash.Length != 40)
                        continue;

                    var result = new TorrentResult
                    {
                        Title = title,
                        InfoHash = infoHash.ToLowerInvariant(),
                        MagnetLink = magnetLink ?? $"magnet:?xt=urn:btih:{infoHash}",
                        Size = size,
                        Seeders = seeders,
                        Leechers = leechers,
                        IndexerName = indexerName
                    };

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse Torznab item from {IndexerName}", indexerName);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse Torznab XML response from {IndexerName}", indexerName);
            return Array.Empty<TorrentResult>();
        }
    }
}
