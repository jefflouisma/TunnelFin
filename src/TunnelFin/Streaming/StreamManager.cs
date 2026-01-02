using System.Collections.Concurrent;
using TunnelFin.Models;

namespace TunnelFin.Streaming;

/// <summary>
/// Routing mode for stream creation (FR-035, FR-036).
/// </summary>
public enum RoutingMode
{
    /// <summary>
    /// Attempt anonymous routing through Tribler network first (FR-035).
    /// </summary>
    AnonymousFirst,

    /// <summary>
    /// Use standard BitTorrent network (requires explicit consent per FR-036).
    /// </summary>
    NonAnonymous
}

/// <summary>
/// Manages HTTP streaming endpoints for active torrents (FR-009, FR-011, FR-012, FR-013, FR-035, FR-036).
/// Creates and manages HTTP endpoints for Jellyfin player consumption.
/// </summary>
public class StreamManager
{
    private readonly ConcurrentDictionary<Guid, StreamInfo> _streams = new();
    private readonly int _maxConcurrentStreams;
    private readonly int _streamInitializationTimeoutSeconds;
    private readonly HashSet<string> _nonAnonymousConsents = new();
    private const string BaseUrl = "http://localhost:8765/stream";

    /// <summary>
    /// Maximum number of concurrent streams allowed (FR-013).
    /// </summary>
    public int MaxConcurrentStreams => _maxConcurrentStreams;

    /// <summary>
    /// Stream initialization timeout in seconds (FR-012).
    /// </summary>
    public int StreamInitializationTimeoutSeconds => _streamInitializationTimeoutSeconds;

    /// <summary>
    /// Initializes a new instance of the StreamManager class.
    /// </summary>
    /// <param name="maxConcurrentStreams">Maximum concurrent streams (default: 3).</param>
    /// <param name="streamInitializationTimeoutSeconds">Stream initialization timeout in seconds (default: 60 per FR-012).</param>
    public StreamManager(int maxConcurrentStreams = 3, int streamInitializationTimeoutSeconds = 60)
    {
        if (maxConcurrentStreams < 1)
            throw new ArgumentException("Max concurrent streams must be at least 1", nameof(maxConcurrentStreams));

        if (streamInitializationTimeoutSeconds < 5)
            throw new ArgumentException("Stream initialization timeout must be at least 5 seconds", nameof(streamInitializationTimeoutSeconds));

        _maxConcurrentStreams = maxConcurrentStreams;
        _streamInitializationTimeoutSeconds = streamInitializationTimeoutSeconds;
    }

    /// <summary>
    /// Creates a new HTTP stream endpoint for a torrent file (FR-009, FR-012).
    /// Uses anonymous-first routing by default (FR-035).
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <param name="fileIndex">Index of the file within the torrent to stream.</param>
    /// <returns>Unique identifier for the stream.</returns>
    /// <exception cref="InvalidOperationException">Thrown when concurrent stream limit is reached (FR-013).</exception>
    /// <exception cref="TimeoutException">Thrown when stream initialization exceeds timeout (FR-012).</exception>
    public async Task<Guid> CreateStreamAsync(Guid torrentId, int fileIndex)
    {
        return await CreateStreamAsync(torrentId, fileIndex, RoutingMode.AnonymousFirst, userId: null);
    }

    /// <summary>
    /// Creates a new HTTP stream endpoint for a torrent file with explicit routing mode (FR-035, FR-036).
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <param name="fileIndex">Index of the file within the torrent to stream.</param>
    /// <param name="routingMode">Routing mode (anonymous-first or non-anonymous).</param>
    /// <param name="userId">User identifier for consent tracking (required for non-anonymous mode).</param>
    /// <returns>Unique identifier for the stream.</returns>
    /// <exception cref="InvalidOperationException">Thrown when concurrent stream limit is reached or consent is missing.</exception>
    /// <exception cref="TimeoutException">Thrown when stream initialization exceeds timeout.</exception>
    public async Task<Guid> CreateStreamAsync(
        Guid torrentId,
        int fileIndex,
        RoutingMode routingMode,
        string? userId = null)
    {
        // Check concurrent stream limit (FR-013)
        if (_streams.Count >= _maxConcurrentStreams)
        {
            throw new InvalidOperationException(
                $"Cannot create stream: maximum concurrent streams ({_maxConcurrentStreams}) reached");
        }

        // Non-anonymous mode requires explicit consent (FR-036)
        if (routingMode == RoutingMode.NonAnonymous)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required for non-anonymous routing", nameof(userId));

            if (!HasNonAnonymousConsent(userId))
            {
                throw new InvalidOperationException(
                    "Non-anonymous routing requires explicit user consent. " +
                    "User must acknowledge that their IP address will be exposed to BitTorrent peers.");
            }
        }

        var streamId = Guid.NewGuid();
        var info = new StreamInfo
        {
            StreamId = streamId,
            TorrentId = torrentId,
            FileIndex = fileIndex,
            Endpoint = $"{BaseUrl}/{streamId}",
            CreatedAt = DateTime.UtcNow,
            RoutingMode = routingMode
        };

        _streams[streamId] = info;

        // Initialize stream with timeout (FR-012)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_streamInitializationTimeoutSeconds));
        try
        {
            await InitializeStreamAsync(streamId, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Clean up failed stream
            _streams.TryRemove(streamId, out _);
            throw new TimeoutException(
                $"Stream initialization timed out after {_streamInitializationTimeoutSeconds} seconds");
        }
        catch (Exception)
        {
            // Clean up failed stream
            _streams.TryRemove(streamId, out _);
            throw;
        }

        return streamId;
    }

    /// <summary>
    /// Initializes a stream with the torrent engine and HTTP endpoint.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    private async Task InitializeStreamAsync(Guid streamId, CancellationToken cancellationToken)
    {
        // TODO: Initialize HTTP endpoint
        // TODO: Wire up to TorrentEngine's stream provider
        // TODO: Wait for initial buffer to be ready
        await Task.Delay(10, cancellationToken); // Placeholder
    }

    /// <summary>
    /// Gets the HTTP endpoint URL for a stream.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <returns>HTTP endpoint URL.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when stream ID is not found.</exception>
    public string GetStreamEndpoint(Guid streamId)
    {
        if (!_streams.TryGetValue(streamId, out var info))
        {
            throw new KeyNotFoundException($"Stream {streamId} not found");
        }

        return info.Endpoint;
    }

    /// <summary>
    /// Gets real-time health metrics for a stream (FR-011).
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <returns>Stream health metrics.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when stream ID is not found.</exception>
    public StreamHealth GetStreamHealth(Guid streamId)
    {
        if (!_streams.TryGetValue(streamId, out var info))
        {
            throw new KeyNotFoundException($"Stream {streamId} not found");
        }

        return new StreamHealth
        {
            StreamId = streamId,
            PeerCount = info.PeerCount,
            DownloadSpeedBytesPerSecond = info.DownloadSpeedBytesPerSecond,
            BufferSeconds = info.BufferSeconds,
            IsBuffering = info.IsBuffering,
            IsReadyForPlayback = info.IsReadyForPlayback,
            DownloadProgress = info.DownloadProgress,
            LastUpdated = info.LastUpdated
        };
    }

    /// <summary>
    /// Stops a stream and cleans up resources.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    public async Task StopStreamAsync(Guid streamId)
    {
        if (_streams.TryRemove(streamId, out var info))
        {
            // TODO: Stop HTTP endpoint
            // TODO: Notify TorrentEngine to release resources
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Gets the number of active streams.
    /// </summary>
    /// <returns>Number of active streams.</returns>
    public int GetActiveStreamCount()
    {
        return _streams.Count;
    }

    /// <summary>
    /// Records user consent for non-anonymous streaming (FR-036).
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <exception cref="ArgumentException">Thrown when userId is null or empty.</exception>
    public void GrantNonAnonymousConsent(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        _nonAnonymousConsents.Add(userId);
    }

    /// <summary>
    /// Revokes user consent for non-anonymous streaming (FR-036).
    /// </summary>
    /// <param name="userId">User identifier.</param>
    public void RevokeNonAnonymousConsent(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            _nonAnonymousConsents.Remove(userId);
    }

    /// <summary>
    /// Checks if a user has granted consent for non-anonymous streaming (FR-036).
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <returns>True if consent has been granted.</returns>
    public bool HasNonAnonymousConsent(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return _nonAnonymousConsents.Contains(userId);
    }

    /// <summary>
    /// Internal class for tracking stream information.
    /// </summary>
    private class StreamInfo
    {
        public Guid StreamId { get; set; }
        public Guid TorrentId { get; set; }
        public int FileIndex { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public RoutingMode RoutingMode { get; set; } = RoutingMode.AnonymousFirst;
        public int PeerCount { get; set; }
        public long DownloadSpeedBytesPerSecond { get; set; }
        public double BufferSeconds { get; set; }
        public bool IsBuffering { get; set; } = true;
        public bool IsReadyForPlayback { get; set; }
        public double DownloadProgress { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

