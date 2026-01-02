# Data Model: TunnelFin Core Plugin

**Date**: January 1, 2026  
**Feature**: 001-tunnelfin-core-plugin  
**Source**: Extracted from feature specification and functional requirements

## Core Entities

### 1. TorrentStream

Represents an active torrent being downloaded and streamed.

**Fields**:
- `Id` (Guid): Unique identifier for the stream
- `InfoHash` (string): BitTorrent infohash (40-char hex)
- `Name` (string): Torrent name/title
- `TotalSize` (long): Total size in bytes
- `DownloadedBytes` (long): Bytes downloaded so far
- `UploadedBytes` (long): Bytes uploaded (for contribution tracking)
- `DownloadSpeed` (double): Current download speed (bytes/sec)
- `UploadSpeed` (double): Current upload speed (bytes/sec)
- `PeerCount` (int): Number of connected peers
- `BufferStatus` (TimeSpan): Amount of buffered playback time
- `HttpEndpoint` (Uri): Local HTTP stream URL for Jellyfin player
- `IsAnonymous` (bool): Whether stream is routed through Tribler network
- `CircuitId` (Guid?): Associated anonymity circuit (null if non-anonymous)
- `State` (TorrentStreamState): Current state (Initializing, Downloading, Streaming, Paused, Stopped)
- `CreatedAt` (DateTimeOffset): Stream creation timestamp
- `LastActivityAt` (DateTimeOffset): Last activity timestamp

**Relationships**:
- Has one `AnonymityCircuit` (optional, if anonymous)
- Has many `TorrentPiece` (internal to MonoTorrent)

**Validation Rules** (from FR-012, FR-013, FR-014, FR-015):
- Stream initialization must timeout after 60 seconds (FR-012)
- Maximum concurrent streams enforced by configuration (default: 3) (FR-013)
- Total cache size must not exceed configured limit (default: 10GB) (FR-014)
- Buffer must maintain >10 seconds during playback (SC-003)

**State Transitions**:
```
Initializing → Downloading → Streaming → Stopped
     ↓              ↓            ↓
   Error         Paused       Paused
```

---

### 2. AnonymityCircuit

Represents a multi-hop onion routing path through Tribler network peers.

**Fields**:
- `Id` (Guid): Unique circuit identifier
- `HopCount` (int): Number of hops (1-3)
- `RelayNodes` (List<PeerInfo>): Ordered list of relay peers
- `State` (CircuitState): Current state (Creating, Established, Failed, Closed)
- `SharedKeys` (List<byte[]>): Shared encryption keys for each hop (DH key exchange)
- `CreatedAt` (DateTimeOffset): Circuit creation timestamp
- `LastUsedAt` (DateTimeOffset): Last data transmission timestamp
- `BytesTransmitted` (long): Total bytes sent through circuit
- `FailureCount` (int): Number of failures (for health tracking)

**Relationships**:
- Has many `TorrentStream` (one circuit can serve multiple streams)
- Has many `PeerInfo` (relay nodes)

**Validation Rules** (from FR-003, FR-006, FR-040):
- Hop count must be between 1-3 (FR-003)
- Default hop count is 3 for maximum privacy (FR-006)
- Circuit establishment retries for configurable duration (default 30s) (FR-040)

**State Transitions**:
```
Creating → Established → Closed
   ↓
 Failed
```

---

### 3. SearchResult

Represents a discovered torrent with metadata.

**Fields**:
- `Id` (Guid): Unique result identifier
- `InfoHash` (string): BitTorrent infohash
- `Title` (string): Parsed/matched title
- `OriginalFilename` (string): Raw torrent filename
- `Resolution` (string): Parsed resolution (e.g., "1080p", "2160p")
- `Quality` (string): Parsed quality (e.g., "WEB-DL", "BluRay")
- `VideoCodec` (string): Parsed video codec (e.g., "x265", "HEVC")
- `AudioCodec` (string): Parsed audio codec (e.g., "AAC", "DTS")
- `AudioChannels` (string): Audio channels (e.g., "5.1", "7.1")
- `HdrFormat` (string?): HDR format if applicable (e.g., "HDR10", "Dolby Vision")
- `Language` (string): Content language
- `FileSize` (long): Total size in bytes
- `Seeders` (int): Number of seeders
- `Leechers` (int): Number of leechers
- `SourceIndexer` (string): Indexer that provided this result
- `MatchConfidence` (double): Confidence score (0.0-1.0) for title matching
- `Year` (int?): Release year (for movies)
- `Season` (int?): Season number (for TV shows)
- `Episode` (int?): Episode number (for TV shows)
- `IsAnonymousAvailable` (bool): Whether available on Tribler network
- `Metadata` (MediaMetadata?): Fetched metadata from TMDB/AniList
- `DiscoveredAt` (DateTimeOffset): Discovery timestamp

**Relationships**:
- Has one `MediaMetadata` (optional)
- Belongs to one `IndexerConfiguration`

**Validation Rules** (from FR-025, FR-026, SC-007, SC-008):
- Deduplication by infohash, filename, or smart hash (FR-025)
- Title/year/episode matching against TMDB/AniList (FR-026)
- 90% deduplication success rate (SC-007)
- 95% metadata fetch success rate (SC-008)

---

### 4. FilterProfile

Represents user-defined filtering and sorting rules for a content type.

**Fields**:
- `Id` (Guid): Unique profile identifier
- `Name` (string): Profile name (e.g., "High Quality Movies")
- `ContentType` (ContentType): Movies, TV Shows, or Anime
- `RequiredFilters` (List<FilterRule>): Must match all
- `PreferredFilters` (List<FilterRule>): Boost score if matched
- `ExcludedFilters` (List<FilterRule>): Must not match any
- `IncludeFilters` (List<FilterRule>): Whitelist (if specified, only these match)
- `ConditionalFilters` (List<ConditionalFilterRule>): Expression-based rules
- `SortCriteria` (List<SortCriterion>): Multi-criteria sort order
- `IsDefault` (bool): Whether this is the default profile for content type
- `CreatedAt` (DateTimeOffset): Profile creation timestamp
- `UpdatedAt` (DateTimeOffset): Last update timestamp

**Nested Types**:
```csharp
public class FilterRule
{
    public FilterAttribute Attribute { get; set; } // Resolution, Quality, Codec, etc.
    public FilterOperator Operator { get; set; }   // Equals, Contains, GreaterThan, etc.
    public string Value { get; set; }              // Filter value
}

public class ConditionalFilterRule
{
    public string Expression { get; set; }         // e.g., "exclude 720p if >5 results at 1080p"
}

public class SortCriterion
{
    public SortAttribute Attribute { get; set; }   // Resolution, Seeders, FileSize, etc.
    public SortDirection Direction { get; set; }   // Ascending, Descending
    public int Priority { get; set; }              // Sort order priority
}
```

**Validation Rules** (from FR-019-FR-024, SC-005, SC-006):
- Support Required, Preferred, Excluded, Include filter types (FR-019)
- Filter by resolution, quality, codecs, audio, HDR, language, size, seeders, release group (FR-020)
- Support keyword/regex matching (FR-021)
- Support conditional expressions (FR-022)
- Multi-criteria sorting (FR-023)
- Separate profiles for Movies/TV/Anime (FR-024)
- Filter/sort 100+ results in <1 second (SC-005)
- Configure profile in <2 minutes (SC-006)

---

### 5. IndexerConfiguration

Represents a torrent indexer source (built-in or custom Torznab).

**Fields**:
- `Id` (Guid): Unique indexer identifier
- `Name` (string): Indexer name (e.g., "1337x", "Custom Tracker")
- `Type` (IndexerType): BuiltIn or Torznab
- `EndpointUrl` (Uri?): API endpoint (for Torznab)
- `ApiKey` (string?): API key (for Torznab)
- `IsEnabled` (bool): Whether indexer is active
- `Capabilities` (IndexerCapabilities): Supported search types (movie, tv, anime)
- `Priority` (int): Search priority (lower = higher priority)
- `TimeoutSeconds` (int): Request timeout
- `LastSuccessAt` (DateTimeOffset?): Last successful query
- `LastFailureAt` (DateTimeOffset?): Last failed query
- `FailureCount` (int): Consecutive failure count
- `AverageResponseTime` (TimeSpan): Average response time (for metrics)

**Validation Rules** (from FR-016, FR-017, FR-018, SC-004, FR-046):
- Built-in indexers: 1337x, Nyaa.si, RARBG (FR-016)
- Custom indexers via Torznab API (FR-017)
- Maximum concurrent searches configurable (default: 5) (FR-018)
- Search results return within 5 seconds (SC-004)
- Track indexer response times (FR-046)

---

### 6. NetworkIdentity

Represents user's persistent Tribler network identity.

**Fields**:
- `PeerId` (string): Unique peer identifier (derived from public key)
- `PublicKey` (byte[]): Ed25519 public key
- `PrivateKey` (byte[]): Ed25519 private key (encrypted at rest)
- `NetworkContribution` (NetworkContributionStats): Bandwidth contribution statistics
- `CreatedAt` (DateTimeOffset): Identity creation timestamp
- `LastSeenAt` (DateTimeOffset): Last network activity

**Nested Type**:
```csharp
public class NetworkContributionStats
{
    public long TotalBytesDownloaded { get; set; }
    public long TotalBytesUploaded { get; set; }
    public long TotalBytesRelayed { get; set; }
    public double ContributionRatio { get; set; } // Relayed / Downloaded
}
```

**Validation Rules** (from FR-004, FR-005, FR-038, SC-010):
- Ed25519 keypair for network participation (FR-004)
- Proportional bandwidth contribution (FR-005)
- Stored in Jellyfin's encrypted configuration (FR-038)
- Contribution within 5% accuracy (SC-010)

---

## Enumerations

```csharp
public enum TorrentStreamState { Initializing, Downloading, Streaming, Paused, Stopped, Error }
public enum CircuitState { Creating, Established, Failed, Closed }
public enum ContentType { Movies, TVShows, Anime }
public enum IndexerType { BuiltIn, Torznab }
public enum FilterAttribute { Resolution, Quality, VideoCodec, AudioCodec, AudioChannels, HdrFormat, Language, FileSize, Seeders, ReleaseGroup }
public enum FilterOperator { Equals, NotEquals, Contains, NotContains, GreaterThan, LessThan, Regex }
public enum SortAttribute { Resolution, Quality, Seeders, Leechers, FileSize, MatchConfidence, DiscoveredAt }
public enum SortDirection { Ascending, Descending }
```

---

## Entity Relationship Diagram

```
NetworkIdentity (1) ──< (N) AnonymityCircuit
                              │
                              │ (1)
                              │
                              ↓
                            (N) TorrentStream
                              │
                              │ (N)
                              │
                              ↓
                            (1) SearchResult ──> (1) MediaMetadata
                              │
                              │ (N)
                              │
                              ↓
                            (1) IndexerConfiguration

FilterProfile (independent, applied to SearchResult collections)
```

