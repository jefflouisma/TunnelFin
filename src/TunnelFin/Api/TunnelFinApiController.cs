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
}

