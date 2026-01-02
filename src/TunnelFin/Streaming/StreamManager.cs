using System.Collections.Concurrent;
using TunnelFin.Models;

namespace TunnelFin.Streaming;

/// <summary>
/// Manages HTTP streaming endpoints for active torrents (FR-009, FR-011, FR-012, FR-013).
/// Creates and manages HTTP endpoints for Jellyfin player consumption.
/// </summary>
public class StreamManager
{
    private readonly ConcurrentDictionary<Guid, StreamInfo> _streams = new();
    private readonly int _maxConcurrentStreams;
    private const string BaseUrl = "http://localhost:8765/stream";

    /// <summary>
    /// Maximum number of concurrent streams allowed (FR-013).
    /// </summary>
    public int MaxConcurrentStreams => _maxConcurrentStreams;

    /// <summary>
    /// Initializes a new instance of the StreamManager class.
    /// </summary>
    /// <param name="maxConcurrentStreams">Maximum concurrent streams (default: 3).</param>
    public StreamManager(int maxConcurrentStreams = 3)
    {
        if (maxConcurrentStreams < 1)
            throw new ArgumentException("Max concurrent streams must be at least 1", nameof(maxConcurrentStreams));

        _maxConcurrentStreams = maxConcurrentStreams;
    }

    /// <summary>
    /// Creates a new HTTP stream endpoint for a torrent file (FR-009).
    /// </summary>
    /// <param name="torrentId">Torrent identifier.</param>
    /// <param name="fileIndex">Index of the file within the torrent to stream.</param>
    /// <returns>Unique identifier for the stream.</returns>
    /// <exception cref="InvalidOperationException">Thrown when concurrent stream limit is reached (FR-013).</exception>
    public async Task<Guid> CreateStreamAsync(Guid torrentId, int fileIndex)
    {
        // Check concurrent stream limit (FR-013)
        if (_streams.Count >= _maxConcurrentStreams)
        {
            throw new InvalidOperationException(
                $"Cannot create stream: maximum concurrent streams ({_maxConcurrentStreams}) reached");
        }

        var streamId = Guid.NewGuid();
        var info = new StreamInfo
        {
            StreamId = streamId,
            TorrentId = torrentId,
            FileIndex = fileIndex,
            Endpoint = $"{BaseUrl}/{streamId}",
            CreatedAt = DateTime.UtcNow
        };

        _streams[streamId] = info;

        // TODO: Initialize HTTP endpoint
        // TODO: Wire up to TorrentEngine's stream provider
        await Task.CompletedTask;

        return streamId;
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
    /// Internal class for tracking stream information.
    /// </summary>
    private class StreamInfo
    {
        public Guid StreamId { get; set; }
        public Guid TorrentId { get; set; }
        public int FileIndex { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int PeerCount { get; set; }
        public long DownloadSpeedBytesPerSecond { get; set; }
        public double BufferSeconds { get; set; }
        public bool IsBuffering { get; set; } = true;
        public bool IsReadyForPlayback { get; set; }
        public double DownloadProgress { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

