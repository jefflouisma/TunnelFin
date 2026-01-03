# Data Model: Core Integration Layer

**Feature**: 003-core-integration
**Date**: 2026-01-02
**Status**: Design Phase

## Overview

This document defines the core data entities for the TunnelFin core integration layer. These entities represent the domain model for torrent metadata, indexer configuration, active streaming sessions, and circuit routing state. All entities are designed for ephemeral storage (in-memory or short-lived disk cache) except for configuration entities which persist in Jellyfin's configuration system.

---

## Entity: TorrentMetadata

**Purpose**: Represents metadata for a torrent discovered from indexers or parsed from magnet links.

**Lifecycle**: Created when search results are returned, persists while torrent is active, deleted when stream ends.

**Storage**: In-memory dictionary keyed by InfoHash.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| InfoHash | string | Yes | SHA-1 hash of torrent info dictionary (40-char hex, lowercase) |
| Title | string | Yes | Human-readable title from indexer or torrent metadata |
| Size | long | Yes | Total size in bytes of all files in torrent |
| Seeders | int | No | Number of seeders (from indexer, may be stale) |
| Leechers | int | No | Number of leechers (from indexer, may be stale) |
| MagnetLink | string | Yes | Magnet URI for torrent (magnet:?xt=urn:btih:...) |
| Files | List\<TorrentFile\> | Yes | List of files in torrent with paths and sizes |
| PieceLength | int | Yes | Size of each piece in bytes (typically 256KB-4MB) |
| TotalPieces | int | Yes | Total number of pieces in torrent |
| CreatedAt | DateTime | Yes | Timestamp when metadata was discovered |
| IndexerSource | string | No | Name of indexer that provided this result |

### Relationships

- **One-to-Many** with `StreamSession`: A torrent can have multiple active streams (different files or users)
- **One-to-Many** with `TorrentFile`: A torrent contains one or more files

### Validation Rules

- InfoHash must be exactly 40 hexadecimal characters (lowercase)
- Size must be positive
- MagnetLink must start with "magnet:?xt=urn:btih:"
- Files list must not be empty
- PieceLength must be power of 2 between 16KB and 16MB

---

## Entity: TorrentFile

**Purpose**: Represents a single file within a multi-file torrent.

**Lifecycle**: Created when torrent metadata is parsed, deleted when parent torrent is removed.

**Storage**: In-memory as part of TorrentMetadata.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Path | string | Yes | Relative path within torrent (e.g., "Season 1/Episode 01.mkv") |
| Size | long | Yes | File size in bytes |
| StartPiece | int | Yes | Index of first piece containing this file's data |
| EndPiece | int | Yes | Index of last piece containing this file's data |
| MediaType | string | No | MIME type (e.g., "video/x-matroska", "video/mp4") |

### Validation Rules

- Path must not be empty or contain ".." (directory traversal)
- Size must be positive
- StartPiece must be <= EndPiece
- MediaType should be video/* for streamable content

---

## Entity: IndexerConfig

**Purpose**: Configuration for a Torznab or HTML-based indexer.

**Lifecycle**: Created by user in plugin settings, persists in Jellyfin configuration.

**Storage**: Jellyfin's PluginConfiguration (XML file in config directory).

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for this indexer |
| Name | string | Yes | Display name (e.g., "Jackett - 1337x", "Nyaa Direct") |
| Type | IndexerType | Yes | Enum: Torznab, Html1337x, HtmlNyaa, HtmlTorrentGalaxy, HtmlEZTV |
| BaseUrl | string | Yes | Base URL for indexer (e.g., "http://localhost:9117/api/v2.0/indexers/1337x") |
| ApiKey | string | No | API key for Torznab indexers (not used for HTML scrapers) |
| RateLimitPerSecond | double | Yes | Maximum requests per second (default: 1.0) |
| Enabled | bool | Yes | Whether this indexer is active (default: true) |
| Priority | int | Yes | Search priority (lower = higher priority, default: 100) |
| Categories | List\<int\> | No | Torznab category IDs to search (e.g., [2000, 5000] for movies/TV) |

### Validation Rules

- Name must not be empty
- BaseUrl must be valid HTTP/HTTPS URL
- RateLimitPerSecond must be between 0.1 and 10.0
- Priority must be non-negative
- ApiKey required if Type == Torznab

---

## Entity: StreamSession

**Purpose**: Represents an active streaming session for a torrent file.

**Lifecycle**: Created when user starts playback, deleted when stream ends or times out (30 min idle).

**Storage**: In-memory dictionary keyed by SessionId.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| SessionId | Guid | Yes | Unique identifier for this stream session |
| InfoHash | string | Yes | InfoHash of torrent being streamed |
| FilePath | string | Yes | Path of file within torrent being streamed |
| CircuitId | Guid | No | ID of circuit used for peer connections (null if direct) |
| BufferStatus | BufferStatus | Yes | Current buffer state (see BufferStatus entity) |
| PlaybackPosition | long | Yes | Current byte offset in file (for seeking) |
| CreatedAt | DateTime | Yes | Timestamp when stream started |
| LastAccessedAt | DateTime | Yes | Timestamp of last HTTP request (for idle timeout) |
| StreamUrl | string | Yes | HTTP URL for this stream (e.g., "/stream/{sessionId}") |
| TorrentEngine | object | Yes | Reference to MonoTorrent ClientEngine instance |

### Relationships

- **Many-to-One** with `TorrentMetadata`: Multiple sessions can stream from same torrent
- **Many-to-One** with `CircuitMetadata`: Multiple sessions can share same circuit

### Validation Rules

- InfoHash must reference existing TorrentMetadata
- FilePath must exist in torrent's file list
- PlaybackPosition must be between 0 and file size
- LastAccessedAt must be updated on every HTTP request




---

## Entity: BufferStatus

**Purpose**: Tracks buffering state for an active stream session.

**Lifecycle**: Created with StreamSession, updated continuously during playback.

**Storage**: In-memory as part of StreamSession.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| BufferedRanges | List\<ByteRange\> | Yes | List of byte ranges that are fully downloaded |
| PrebufferComplete | bool | Yes | Whether initial prebuffer is complete (ready to play) |
| PrebufferTargetBytes | long | Yes | Target prebuffer size in bytes (bitrate × target_seconds) |
| CurrentBufferedBytes | long | Yes | Total bytes currently buffered |
| DownloadRate | long | Yes | Current download speed in bytes/second |
| PlaybackRate | long | Yes | Estimated playback bitrate in bytes/second |

### Validation Rules

- BufferedRanges must not overlap
- CurrentBufferedBytes must equal sum of all buffered ranges
- DownloadRate and PlaybackRate must be non-negative

---

## Entity: CircuitMetadata

**Purpose**: Represents metadata for an active Tribler/IPv8 circuit used for peer connections.

**Lifecycle**: Created when circuit is constructed, deleted when circuit is destroyed or marked unhealthy.

**Storage**: In-memory dictionary keyed by CircuitId.

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| CircuitId | Guid | Yes | Unique identifier for this circuit |
| HopNodes | List\<string\> | Yes | List of relay node IDs in circuit path (3-5 hops) |
| CreatedAt | DateTime | Yes | Timestamp when circuit was constructed |
| LastHealthCheck | DateTime | Yes | Timestamp of last PING/PONG health check |
| State | CircuitState | Yes | Enum: Constructing, Ready, Unhealthy, Destroyed |
| IsHealthy | bool | Yes | Whether circuit passed last health check |
| AverageLatency | TimeSpan | Yes | Average round-trip time from recent PING/PONG |
| TotalBytesTransferred | long | Yes | Total bytes sent/received through this circuit |
| ActiveConnections | int | Yes | Number of peer connections currently using this circuit |

### Relationships

- **One-to-Many** with `StreamSession`: A circuit can be shared by multiple streams

### Validation Rules

- HopNodes must contain 1-5 elements
- State must transition: Constructing → Ready → (Unhealthy | Destroyed)
- IsHealthy must be false if State == Unhealthy or Destroyed
- AverageLatency must be positive if IsHealthy == true

---

## Entity: TorrentResult

**Purpose**: Represents a search result from an indexer (Torznab or HTML scraper).

**Lifecycle**: Created when indexer returns results, short-lived (discarded after user selection).

**Storage**: In-memory, returned from IndexerManager.SearchAsync().

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Title | string | Yes | Torrent title from indexer |
| InfoHash | string | No | SHA-1 hash if available from indexer (Torznab provides, HTML may not) |
| MagnetLink | string | Yes | Magnet URI for torrent |
| Size | long | Yes | Total size in bytes |
| Seeders | int | Yes | Number of seeders (0 if unknown) |
| Leechers | int | Yes | Number of leechers (0 if unknown) |
| PublishedDate | DateTime | No | When torrent was uploaded to indexer |
| Category | string | No | Category from indexer (e.g., "Movies", "TV", "Anime") |
| IndexerName | string | Yes | Name of indexer that provided this result |
| PosterUrl | string | No | URL to poster image if available |

### Validation Rules

- Title must not be empty
- MagnetLink must start with "magnet:?xt=urn:btih:"
- Size must be positive
- Seeders and Leechers must be non-negative
- IndexerName must match configured indexer

---

## Enumerations

### IndexerType

```csharp
public enum IndexerType
{
    Torznab,           // Torznab v1.3 protocol (Jackett, Prowlarr)
    Html1337x,         // 1337x.to HTML scraper
    HtmlNyaa,          // Nyaa.si HTML scraper
    HtmlTorrentGalaxy, // TorrentGalaxy.to HTML scraper
    HtmlEZTV           // EZTV.re HTML scraper
}
```

### CircuitState

```csharp
public enum CircuitState
{
    Constructing, // Circuit is being built (CREATE/EXTEND messages in flight)
    Ready,        // Circuit is healthy and ready for connections
    Unhealthy,    // Circuit failed health check, should not be used
    Destroyed     // Circuit has been torn down
}
```

---

## Entity Relationships Diagram

```text
┌─────────────────┐
│ IndexerConfig   │ (Persistent, Jellyfin config)
└─────────────────┘
        │
        │ configured in
        ▼
┌─────────────────┐      produces      ┌─────────────────┐
│ IndexerManager  │ ─────────────────> │ TorrentResult   │ (Ephemeral)
└─────────────────┘                    └─────────────────┘
                                               │
                                               │ selected by user
                                               ▼
                                       ┌─────────────────┐
                                       │ TorrentMetadata │ (In-memory)
                                       └─────────────────┘
                                               │
                                               │ contains
                                               ▼
                                       ┌─────────────────┐
                                       │ TorrentFile     │ (In-memory)
                                       └─────────────────┘
                                               │
                                               │ streamed via
                                               ▼
┌─────────────────┐      uses          ┌─────────────────┐
│ CircuitMetadata │ <───────────────── │ StreamSession   │ (In-memory)
└─────────────────┘                    └─────────────────┘
        │                                      │
        │                                      │ tracks
        ▼                                      ▼
   (Shared by                          ┌─────────────────┐
    multiple                           │ BufferStatus    │ (In-memory)
    sessions)                          └─────────────────┘
```

---

## Storage Strategy

### In-Memory Storage
- **TorrentMetadata**: `ConcurrentDictionary<string, TorrentMetadata>` keyed by InfoHash
- **StreamSession**: `ConcurrentDictionary<Guid, StreamSession>` keyed by SessionId
- **CircuitMetadata**: `ConcurrentDictionary<Guid, CircuitMetadata>` keyed by CircuitId
- **TorrentResult**: Returned from methods, not stored

### Persistent Storage
- **IndexerConfig**: Jellyfin's PluginConfiguration (XML serialization)
- Stored in: `{JellyfinConfigDir}/plugins/TunnelFin/configuration.xml`

### Ephemeral Disk Storage
- **Torrent data**: MonoTorrent's disk cache (configurable size, LRU eviction)
- Stored in: `{JellyfinDataDir}/plugins/TunnelFin/cache/`
- Deleted when: Stream ends or 30-minute idle timeout
