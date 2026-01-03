using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TunnelFin.Streaming;

/// <summary>
/// Manages HTTP streaming endpoints for torrent files.
/// Handles stream session lifecycle, range requests, and idle timeouts.
/// </summary>
public interface IStreamManager
{
    /// <summary>
    /// Creates a new stream session for a torrent file.
    /// Returns HTTP URL that can be used for playback.
    /// </summary>
    /// <param name="infoHash">SHA-1 hash of torrent</param>
    /// <param name="filePath">Relative path of file within torrent</param>
    /// <param name="circuitId">Optional circuit ID for peer connections (null for direct)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream session with HTTP URL</returns>
    Task<StreamSession> CreateSessionAsync(string infoHash, string filePath, Guid? circuitId, CancellationToken cancellationToken);

    /// <summary>
    /// Handles HTTP GET request for streaming.
    /// Supports range requests (RFC 7233) for seeking.
    /// </summary>
    /// <param name="sessionId">Stream session ID from URL</param>
    /// <param name="httpContext">HTTP context with request/response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleStreamRequestAsync(Guid sessionId, HttpContext httpContext, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active stream session by ID.
    /// </summary>
    /// <param name="sessionId">Stream session ID</param>
    /// <returns>Stream session or null if not found</returns>
    StreamSession? GetSession(Guid sessionId);

    /// <summary>
    /// Gets all active stream sessions.
    /// </summary>
    /// <returns>List of active sessions</returns>
    IReadOnlyList<StreamSession> GetActiveSessions();

    /// <summary>
    /// Ends a stream session and cleans up resources.
    /// Removes torrent if no other sessions are using it.
    /// </summary>
    /// <param name="sessionId">Stream session ID to end</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EndSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the HTTP URL for a stream session.
    /// </summary>
    /// <param name="sessionId">Stream session ID</param>
    /// <returns>HTTP URL (e.g., "http://localhost:8096/TunnelFin/stream/{sessionId}")</returns>
    string GetStreamUrl(Guid sessionId);

    /// <summary>
    /// Cleans up idle sessions that haven't been accessed in the timeout period.
    /// Called periodically by background service.
    /// </summary>
    /// <param name="idleTimeout">Timeout period (default: 30 minutes)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupIdleSessionsAsync(TimeSpan idleTimeout, CancellationToken cancellationToken);
}

