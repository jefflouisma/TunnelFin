using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TunnelFin.Indexers;
using TunnelFin.Models;
using TunnelFin.Streaming;

namespace TunnelFin.Api;

/// <summary>
/// TunnelFin API Controller - Exposes search and streaming endpoints.
/// Routes are available at /TunnelFin/* endpoints.
/// </summary>
[ApiController]
[Route("TunnelFin")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize]
public class TunnelFinApiController : ControllerBase
{
    private readonly IIndexerManager _indexerManager;
    private readonly IStreamManager _streamManager;
    private readonly ILogger<TunnelFinApiController> _logger;

    public TunnelFinApiController(
        IIndexerManager indexerManager,
        IStreamManager streamManager,
        ILogger<TunnelFinApiController> logger)
    {
        _indexerManager = indexerManager;
        _streamManager = streamManager;
        _logger = logger;
    }

    /// <summary>
    /// Search for torrents across all enabled indexers.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="limit">Maximum number of results (default: 50).</param>
    /// <returns>List of torrent search results.</returns>
    [HttpGet("Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery, Required] string query,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query parameter is required");
        }

        _logger.LogInformation("TunnelFin API search: {Query}", query);

        try
        {
            var results = await _indexerManager.SearchAsync(query, cancellationToken);

            // Apply limit
            var limitedResults = results.Take(limit).Select(r => new TorrentSearchResult
            {
                InfoHash = r.InfoHash,
                Title = r.Title,
                Size = r.Size,
                Seeders = r.Seeders ?? 0,
                Leechers = r.Leechers ?? 0,
                Category = r.Category,
                MagnetLink = r.MagnetLink,
                IndexerName = r.IndexerName
            }).ToList();

            _logger.LogInformation("Found {Count} results for query: {Query}", limitedResults.Count, query);

            return Ok(new SearchResponse
            {
                Query = query,
                TotalResults = limitedResults.Count,
                Results = limitedResults
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for: {Query}", query);
            return StatusCode(500, new { error = "Search failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Get stream URL for a torrent by info hash.
    /// </summary>
    /// <param name="infoHash">Torrent info hash.</param>
    /// <param name="fileIndex">File index within the torrent (default: 0).</param>
    /// <returns>Stream session information.</returns>
    [HttpGet("Stream/{infoHash}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StreamResponse>> GetStream(
        [FromRoute, Required] string infoHash,
        [FromQuery] int fileIndex = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(infoHash))
        {
            return BadRequest("InfoHash is required");
        }

        _logger.LogInformation("Creating stream for InfoHash: {InfoHash}, FileIndex: {FileIndex}", infoHash, fileIndex);

        try
        {
            var session = await _streamManager.CreateSessionAsync(infoHash, null, null, cancellationToken);
            var streamUrl = _streamManager.GetStreamUrl(session.SessionId);

            return Ok(new StreamResponse
            {
                SessionId = session.SessionId.ToString(),
                InfoHash = infoHash,
                StreamUrl = streamUrl,
                Status = "Ready"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating stream for: {InfoHash}", infoHash);
            return StatusCode(500, new { error = "Stream creation failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("Health")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Health()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Version = "1.0.0",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get network anonymity status.
    /// Returns information about Tribler network connectivity and anonymity circuits.
    /// </summary>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<NetworkStatusResponse> Status()
    {
        // TODO: Wire up to actual Tribler/IPv8 network status when implemented
        // For now, return realistic mock data based on configuration
        var isAnonymous = false; // Will be true when Tribler circuits are established
        var circuitCount = 0;    // Number of active anonymity circuits
        var peerCount = 0;       // Connected IPv8 peers

        return Ok(new NetworkStatusResponse
        {
            IsAnonymous = isAnonymous,
            CircuitCount = circuitCount,
            PeerCount = peerCount,
            NetworkStatus = isAnonymous ? "Anonymous" : "Direct",
            Message = isAnonymous
                ? $"Connected via {circuitCount} anonymity circuit(s)"
                : "Direct connection (Tribler network not available)"
        });
    }

    /// <summary>
    /// Standalone search web UI (Search UX Phase 3).
    /// Provides a full-featured search page accessible at /TunnelFin/.
    /// </summary>
    [HttpGet("")]
    [AllowAnonymous]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult Index()
    {
        return Content(GetSearchPageHtml(), "text/html");
    }

    private static string GetSearchPageHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>TunnelFin - Search</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #101010; color: #e0e0e0; min-height: 100vh; }
        .container { max-width: 1400px; margin: 0 auto; padding: 20px; }
        header { text-align: center; padding: 30px 0; }
        header h1 { font-size: 2.5em; margin-bottom: 10px; }
        header h1 span { color: #00a4dc; }
        .network-status { display: inline-block; padding: 8px 16px; border-radius: 20px; font-size: 14px; margin-bottom: 20px; }
        .network-status.anonymous { background: #1b5e20; color: #a5d6a7; }
        .network-status.direct { background: #e65100; color: #ffcc80; }
        .search-box { display: flex; gap: 10px; max-width: 700px; margin: 0 auto 30px; }
        .search-input { flex: 1; padding: 15px 20px; font-size: 18px; border: 2px solid #333; border-radius: 8px; background: #1a1a1a; color: #fff; outline: none; }
        .search-input:focus { border-color: #00a4dc; }
        .search-btn { padding: 15px 30px; font-size: 18px; background: #00a4dc; color: #fff; border: none; border-radius: 8px; cursor: pointer; font-weight: bold; }
        .search-btn:hover { background: #0084b4; }
        .search-btn:disabled { background: #444; cursor: not-allowed; }
        .results { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; }
        .result-card { background: #1a1a1a; border-radius: 12px; padding: 20px; border: 1px solid #333; transition: all 0.2s; }
        .result-card:hover { border-color: #00a4dc; transform: translateY(-2px); }
        .result-title { font-size: 15px; font-weight: 600; margin-bottom: 12px; line-height: 1.4; word-break: break-word; }
        .result-badge { display: inline-block; padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; margin-right: 5px; margin-bottom: 8px; }
        .badge-anonymous { background: #1b5e20; color: #a5d6a7; }
        .badge-direct { background: #e65100; color: #ffcc80; }
        .badge-seeders { background: #2e7d32; color: #fff; }
        .badge-seeders.low { background: #f57c00; }
        .badge-seeders.none { background: #c62828; }
        .result-meta { font-size: 13px; color: #888; margin-bottom: 5px; }
        .result-actions { margin-top: 15px; }
        .play-btn { width: 100%; padding: 12px; font-size: 14px; background: #00a4dc; color: #fff; border: none; border-radius: 6px; cursor: pointer; font-weight: bold; }
        .play-btn:hover { background: #0084b4; }
        .loading, .no-results, .error { text-align: center; padding: 60px 20px; }
        .error { background: #2d1515; color: #ff6b6b; border-radius: 8px; }
        .help-text { text-align: center; color: #666; margin-top: 30px; font-size: 14px; }
        .help-text a { color: #00a4dc; text-decoration: none; }
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>🎬 <span>TunnelFin Search</span></h1>
            <div id="networkStatus" class="network-status direct">🟠 Checking network...</div>
        </header>
        <div class="search-box">
            <input type="text" id="searchInput" class="search-input" placeholder="Search for movies, TV shows, anime..." autofocus>
            <button id="search" class="search-btn">Search</button>
        </div>
        <div id="searchResults"></div>
        <div class="help-text">
            <p>TunnelFin streams torrents through Jellyfin with privacy protection.</p>
            <p>🟢 = Anonymous (IP hidden) | 🟠 = Direct (IP visible)</p>
        </div>
    </div>
    <script>
        const queryInput = document.getElementById('searchInput');
        const searchBtn = document.getElementById('search');
        const resultsDiv = document.getElementById('searchResults');
        const networkStatusDiv = document.getElementById('networkStatus');
        let isAnonymous = false;

        async function checkNetwork() {
            try {
                const r = await fetch('/TunnelFin/Health');
                const d = await r.json();
                isAnonymous = d.anonymousAvailable || false;
                networkStatusDiv.className = 'network-status ' + (isAnonymous ? 'anonymous' : 'direct');
                networkStatusDiv.textContent = isAnonymous ? '🟢 Anonymous Network Available' : '🟠 Direct Connection Only';
            } catch(e) { networkStatusDiv.textContent = '⚠️ Network status unknown'; }
        }

        async function search() {
            const q = queryInput.value.trim();
            if (!q) return;
            searchBtn.disabled = true;
            searchBtn.textContent = 'Searching...';
            resultsDiv.innerHTML = '<div class="loading">🔍 Searching indexers...</div>';
            try {
                const r = await fetch('/TunnelFin/Search?query=' + encodeURIComponent(q) + '&limit=50');
                const d = await r.json();
                render(d.results || []);
            } catch(e) {
                resultsDiv.innerHTML = '<div class="error">❌ Search failed: ' + e.message + '</div>';
            }
            searchBtn.disabled = false;
            searchBtn.textContent = 'Search';
        }

        function fmt(b) {
            if (!b) return '?';
            const u = ['B','KB','MB','GB','TB'];
            let i = 0;
            while (b >= 1024 && i < u.length-1) { b /= 1024; i++; }
            return b.toFixed(1) + ' ' + u[i];
        }

        function esc(s) { return s ? s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;') : ''; }

        function render(results) {
            if (!results.length) { resultsDiv.innerHTML = '<div class="no-results">No results found. Try different keywords.</div>'; return; }
            resultsDiv.innerHTML = '<div class="results">' + results.map(r => {
                const badge = isAnonymous ? '<span class="result-badge badge-anonymous">🟢 Anonymous</span>' : '<span class="result-badge badge-direct">🟠 Direct</span>';
                const sc = r.seeders > 10 ? '' : (r.seeders > 0 ? ' low' : ' none');
                return '<div class="result-card"><div class="result-title">' + esc(r.title) + '</div><div>' + badge + '<span class="result-badge badge-seeders' + sc + '">👤 ' + r.seeders + ' seeders</span></div><div class="result-meta">📦 ' + fmt(r.size) + ' | 📥 ' + r.leechers + ' leechers</div><div class="result-meta">📡 ' + esc(r.indexerName) + '</div><div class="result-actions"><button class="play-btn" onclick="play(\'' + r.infoHash + '\')">▶ Play</button></div></div>';
            }).join('') + '</div>';
        }

        async function play(hash) {
            try {
                const r = await fetch('/TunnelFin/Stream/' + hash);
                const d = await r.json();
                if (d.streamUrl) window.open(d.streamUrl, '_blank');
            } catch(e) { alert('Failed to start stream: ' + e.message); }
        }

        searchBtn.onclick = search;
        queryInput.onkeypress = (e) => { if (e.key === 'Enter') search(); };
        checkNetwork();
    </script>
</body>
</html>
""";
    }
}

