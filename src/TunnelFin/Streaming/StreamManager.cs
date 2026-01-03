using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TunnelFin.BitTorrent;
using TunnelFin.Configuration;
using TunnelFin.Models;

namespace TunnelFin.Streaming;

/// <summary>
/// Manages HTTP streaming endpoints for torrent files.
/// Handles stream session lifecycle, range requests (RFC 7233), and idle timeouts.
/// Implements FR-003 (HTTP streaming), FR-004 (range requests), FR-007a (ephemeral sessions).
/// </summary>
public class StreamManager : IStreamManager
{
    private readonly ITorrentEngine _torrentEngine;
    private readonly StreamingConfig _config;
    private readonly ILogger<StreamManager>? _logger;
    private readonly ConcurrentDictionary<Guid, StreamSession> _sessions;
    private readonly ConcurrentDictionary<Guid, Stream> _activeStreams;

    public StreamManager(
        ITorrentEngine torrentEngine,
        StreamingConfig config,
        ILogger<StreamManager>? logger = null)
    {
        _torrentEngine = torrentEngine ?? throw new ArgumentNullException(nameof(torrentEngine));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _sessions = new ConcurrentDictionary<Guid, StreamSession>();
        _activeStreams = new ConcurrentDictionary<Guid, Stream>();
    }

    /// <summary>
    /// Creates a new stream session for a torrent file.
    /// </summary>
    public async Task<StreamSession> CreateSessionAsync(
        string infoHash,
        string filePath,
        Guid? circuitId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(infoHash))
            throw new ArgumentException("InfoHash cannot be empty", nameof(infoHash));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("FilePath cannot be empty", nameof(filePath));

        // Check concurrent stream limit (FR-014)
        if (_sessions.Count >= _config.MaxConcurrentStreams)
        {
            throw new InvalidOperationException(
                $"Maximum concurrent streams ({_config.MaxConcurrentStreams}) reached");
        }

        // Create stream from torrent engine
        var stream = await _torrentEngine.CreateStreamAsync(
            infoHash,
            filePath,
            prebuffer: true,
            cancellationToken);

        // Create session
        var sessionId = Guid.NewGuid();

        // Get initial buffer status
        var bufferStatus = _torrentEngine.GetBufferStatus(infoHash, filePath) ?? new BufferStatus
        {
            BufferedRanges = new List<(long Start, long End)>(),
            PrebufferComplete = false,
            CurrentBufferedBytes = 0,
            DownloadRate = 0,
            LastUpdated = DateTime.UtcNow
        };

        var session = new StreamSession
        {
            SessionId = sessionId,
            InfoHash = infoHash,
            FilePath = filePath,
            CircuitId = circuitId,
            BufferStatus = bufferStatus,
            StreamUrl = GetStreamUrl(sessionId),
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            PlaybackPosition = 0
        };

        _sessions[sessionId] = session;
        _activeStreams[sessionId] = stream;

        _logger?.LogInformation(
            "Created stream session {SessionId} for {InfoHash}/{FilePath} (Circuit: {CircuitId})",
            sessionId, infoHash, filePath, circuitId);

        return session;
    }

    /// <summary>
    /// Handles HTTP GET request for streaming with range request support (RFC 7233).
    /// </summary>
    public async Task HandleStreamRequestAsync(
        Guid sessionId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            httpContext.Response.StatusCode = 404;
            await httpContext.Response.WriteAsync("Stream session not found", cancellationToken);
            return;
        }

        if (!_activeStreams.TryGetValue(sessionId, out var stream))
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsync("Stream not available", cancellationToken);
            return;
        }

        // Update last accessed time
        session.LastAccessedAt = DateTime.UtcNow;

        // Get file size
        long fileSize = stream.Length;

        // Parse Range header (RFC 7233)
        var rangeHeader = httpContext.Request.Headers["Range"].ToString();
        long startByte = 0;
        long endByte = fileSize - 1;

        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
        {
            var range = rangeHeader.Substring(6);
            var parts = range.Split('-');

            if (parts.Length == 2)
            {
                if (!string.IsNullOrEmpty(parts[0]))
                    startByte = long.Parse(parts[0]);

                if (!string.IsNullOrEmpty(parts[1]))
                    endByte = long.Parse(parts[1]);
            }

            // Validate range
            if (startByte > endByte || startByte >= fileSize)
            {
                httpContext.Response.StatusCode = 416; // Range Not Satisfiable
                httpContext.Response.Headers["Content-Range"] = $"bytes */{fileSize}";
                return;
            }

            // Clamp endByte to file size
            endByte = Math.Min(endByte, fileSize - 1);
        }

        long contentLength = endByte - startByte + 1;

        // Set response headers
        httpContext.Response.StatusCode = string.IsNullOrEmpty(rangeHeader) ? 200 : 206;
        httpContext.Response.ContentType = "application/octet-stream";
        httpContext.Response.Headers["Accept-Ranges"] = "bytes";
        httpContext.Response.Headers["Content-Length"] = contentLength.ToString();

        if (!string.IsNullOrEmpty(rangeHeader))
        {
            httpContext.Response.Headers["Content-Range"] = $"bytes {startByte}-{endByte}/{fileSize}";
        }

        // Seek to start position
        stream.Seek(startByte, SeekOrigin.Begin);

        // Update playback position
        session.PlaybackPosition = startByte;

        // Stream data to response
        var buffer = new byte[81920]; // 80KB buffer
        long bytesRemaining = contentLength;

        while (bytesRemaining > 0 && !cancellationToken.IsCancellationRequested)
        {
            int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
            int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, cancellationToken);

            if (bytesRead == 0)
                break;

            await httpContext.Response.Body.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            bytesRemaining -= bytesRead;
        }

        _logger?.LogDebug(
            "Served {BytesServed} bytes ({StartByte}-{EndByte}) for session {SessionId}",
            contentLength, startByte, endByte, sessionId);
    }

    /// <summary>
    /// Gets an active stream session by ID.
    /// </summary>
    public StreamSession? GetSession(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    /// <summary>
    /// Gets all active stream sessions.
    /// </summary>
    public IReadOnlyList<StreamSession> GetActiveSessions()
    {
        return _sessions.Values.ToList();
    }

    /// <summary>
    /// Ends a stream session and cleans up resources.
    /// </summary>
    public async Task EndSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return;

        // Close and dispose stream
        if (_activeStreams.TryRemove(sessionId, out var stream))
        {
            await stream.DisposeAsync();
        }

        // Remove torrent if no other sessions are using it
        var otherSessions = _sessions.Values.Where(s => s.InfoHash == session.InfoHash).ToList();
        if (otherSessions.Count == 0)
        {
            await _torrentEngine.RemoveTorrentAsync(session.InfoHash, cancellationToken);
        }

        _logger?.LogInformation(
            "Ended stream session {SessionId} for {InfoHash}/{FilePath}",
            sessionId, session.InfoHash, session.FilePath);
    }

    /// <summary>
    /// Gets the HTTP URL for a stream session.
    /// </summary>
    public string GetStreamUrl(Guid sessionId)
    {
        return $"{_config.HttpStreamingPrefix}/stream/{sessionId}";
    }

    /// <summary>
    /// Cleans up idle sessions that haven't been accessed in the timeout period.
    /// </summary>
    public async Task<int> CleanupIdleSessionsAsync(TimeSpan idleTimeout, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var idleSessions = _sessions.Values
            .Where(s => now - s.LastAccessedAt > idleTimeout)
            .ToList();

        foreach (var session in idleSessions)
        {
            await EndSessionAsync(session.SessionId, cancellationToken);
        }

        if (idleSessions.Count > 0)
        {
            _logger?.LogInformation(
                "Cleaned up {Count} idle sessions (timeout: {Timeout})",
                idleSessions.Count, idleTimeout);
        }

        return idleSessions.Count;
    }
}

