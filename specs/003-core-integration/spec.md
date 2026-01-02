# Feature Specification: Core Integration Layer

**Feature Branch**: `003-core-integration`
**Created**: January 2, 2026
**Status**: Draft
**Input**: User description: "Implement core integration layer: MonoTorrent streaming engine, HTTP endpoints, indexer scrapers, Jellyfin channel/search providers, and circuit-routed peer connections - completing all 20 unimplemented components"

**Dependency**: This feature extends `001-tunnelfin-core-plugin` and `002-network-transport` by implementing the missing integration layer that connects all components into a functional streaming system.

## Clarifications

### Session 2026-01-02

- Q: Which indexers should be implemented first? → A: Torznab (Jackett/Prowlarr compatible) has highest priority as it provides unified API; 1337x, Nyaa, TorrentGalaxy, EZTV are secondary built-in scrapers
- Q: Should HTTP streaming use Kestrel or Jellyfin's built-in HTTP? → A: Use MonoTorrent's built-in HttpStream with Jellyfin's HTTP prefix routing
- Q: How to handle indexer rate limiting? → A: 1 request/second per indexer with exponential backoff on 429/503 errors
- Q: Replace RARBG (shut down 2023)? → A: Replace with TorrentGalaxy (movies/TV, verified uploaders) and EZTV (TV-specialized). Both have clean HTML structures suitable for scraping.
- Q: Metadata enrichment approach? → A: Defer to Jellyfin's existing metadata providers - let Jellyfin handle IMDB/TMDB enrichment for consistency with library items
- Q: Download persistence? → A: Ephemeral - torrents are stream-only, deleted when stream ends. Simpler implementation, no disk management complexity.
- Q: Multi-file torrents? → A: Present as playlist - group files as single item with episode/file selection UI
- Q: Subtitle support? → A: Use Jellyfin's existing subtitle plugins (Open Subtitles, Subscene, etc.) - consistent UX, no additional dependencies

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Torrent Streaming via MonoTorrent (Priority: P1)

A user adds a magnet link and wants to stream the video content. The system must initialize MonoTorrent engine, download metadata, create an HTTP streaming endpoint, and serve video data with seeking support.

**Why this priority**: This is the core streaming capability - without it, no video playback is possible. All other features depend on this working.

**Independent Test**: Can be tested by adding Big Buck Bunny magnet link and verifying HTTP stream returns valid MP4 data with range request support.

**Acceptance Scenarios**:

1. **Given** a valid magnet link, **When** AddTorrentAsync is called, **Then** MonoTorrent manager is created and starts downloading metadata
2. **Given** metadata is received, **When** CreateStreamAsync is called, **Then** HTTP endpoint is created at configured prefix
3. **Given** HTTP endpoint exists, **When** GET request with Range header is sent, **Then** correct byte range is returned with 206 Partial Content
4. **Given** video is partially downloaded, **When** user seeks to undownloaded position, **Then** MonoTorrent prioritizes those pieces and stream waits

---

### User Story 2 - Indexer Search Results (Priority: P1)

A user searches for content and expects results from configured indexers. The system must query Torznab APIs and/or scrape built-in indexers, parse responses, and return normalized search results.

**Why this priority**: Content discovery is essential - users need to find torrents before streaming them. Without indexers, users must manually find magnet links.

**Independent Test**: Can be tested by querying a Torznab endpoint (Jackett) with a known query and verifying parsed results contain title, size, seeders, and magnet link.

**Acceptance Scenarios**:

1. **Given** Torznab indexer is configured, **When** SearchAsync is called, **Then** HTTP GET to `/api?t=search&q=query` returns XML results
2. **Given** XML response is received, **When** parsing completes, **Then** torznab:attr elements are extracted (seeders, size, infohash)
3. **Given** built-in indexer (1337x), **When** SearchAsync is called, **Then** HTML is fetched and parsed for torrent rows
4. **Given** rate limit is exceeded, **When** 429 response received, **Then** exponential backoff is applied (1s, 2s, 4s)

---

### User Story 3 - Jellyfin Channel Integration (Priority: P2)

Users browse TunnelFin content through Jellyfin's channel interface. The system must implement IChannel to present search results as browsable library items with metadata.

**Why this priority**: Native Jellyfin integration is important for UX but not blocking - users can still search and stream without it.

**Independent Test**: Can be tested by loading Jellyfin, navigating to TunnelFin channel, and verifying items appear with correct titles and artwork.

**Acceptance Scenarios**:

1. **Given** ChannelProvider is registered, **When** GetChannelItemsAsync is called, **Then** search results are converted to ChannelItem list
2. **Given** ChannelItem is selected, **When** GetChannelItemAsync is called, **Then** detailed item with stream URL is returned
3. **Given** item has IMDB/TMDB ID, **When** metadata is requested, **Then** Jellyfin metadata providers populate artwork and descriptions

---

### User Story 4 - Anonymous Peer Connections (Priority: P2)

BitTorrent peer connections must route through established Tribler circuits to hide the user's IP address from swarm peers.

**Why this priority**: Anonymity is core to TunnelFin's value proposition but requires working circuits from 002-network-transport first.

**Independent Test**: Can be tested by establishing circuit, connecting to a peer via TunnelSocketConnector, and verifying traffic flows through circuit (peer sees relay IP).

**Acceptance Scenarios**:

1. **Given** established circuit exists, **When** PeerManager.ConnectToPeerAsync is called, **Then** connection routes through TunnelProxy
2. **Given** circuit fails mid-download, **When** traffic cannot route, **Then** fallback prompt appears (if AllowNonAnonymousFallback=true)
3. **Given** no circuits available, **When** connection attempted, **Then** operation waits or fails based on settings

---

### User Story 5 - Network Availability Check (Priority: P3)

The system checks Tribler network availability and displays appropriate UI indicators (green/orange play button) based on anonymity status.

**Why this priority**: UI feedback is important but not blocking - streaming works regardless of indicator color.

**Independent Test**: Can be tested by checking IsNetworkAvailable when circuits exist vs. when they don't.

**Acceptance Scenarios**:

1. **Given** active circuits exist, **When** CheckNetworkAvailabilityAsync is called, **Then** returns true and play button is green
2. **Given** no circuits exist, **When** CheckNetworkAvailabilityAsync is called, **Then** returns false and play button is orange
3. **Given** network status changes, **When** status event fires, **Then** UI updates within 5 seconds

---

### Edge Cases

- **Metadata timeout**: If torrent metadata doesn't arrive within 90 seconds, system cancels operation and reports error
- **Indexer unreachable**: If indexer returns 5xx error 3 times, mark as temporarily unavailable for 5 minutes
- **Zero seeders**: Torrents with 0 seeders are shown with warning indicator; download may not start
- **Circuit exhaustion**: If all circuits are in use, queue connection requests with 30s timeout
- **Disk space**: Check available disk space before starting download; warn if < 2x torrent size available
- **Invalid magnet link**: Validate infohash format (40 hex chars) before attempting to add torrent


## Requirements *(mandatory)*

### Functional Requirements

**Streaming Engine (MonoTorrent Integration)**

- **FR-001**: System MUST initialize MonoTorrent ClientEngine with streaming-optimized settings (sequential piece picking, disk cache enabled)
- **FR-002**: System MUST support adding torrents via magnet link using AddStreamingAsync
- **FR-003**: System MUST wait for metadata (BEP-9) with configurable timeout (default 90s)
- **FR-004**: System MUST create HTTP streaming endpoints via StreamProvider.CreateHttpStreamAsync
- **FR-005**: System MUST support HTTP Range requests for video seeking (206 Partial Content)
- **FR-006**: System MUST prioritize pieces based on current stream position for smooth playback
- **FR-007**: System MUST expose stream URLs at configurable HTTP prefix (default: /tunnelfin/stream/)
- **FR-007a**: System MUST delete torrent data when stream ends (ephemeral mode)
- **FR-007b**: System MUST present multi-file torrents as playlist with file selection UI

**Indexer Integration**

- **FR-008**: System MUST support Torznab API protocol for querying Jackett/Prowlarr indexers
- **FR-009**: System MUST parse Torznab XML responses extracting: title, size, seeders, leechers, infohash, magnet link
- **FR-010**: System MUST implement rate limiting (1 request/second per indexer)
- **FR-011**: System MUST implement exponential backoff on 429/503 errors (1s, 2s, 4s, 8s, max 60s)
- **FR-012**: System MUST support built-in scrapers for: 1337x, Nyaa, TorrentGalaxy, EZTV
- **FR-013**: System MUST normalize search results into common TorrentSearchResult model

**Jellyfin Integration**

- **FR-014**: System MUST implement IChannel interface for browsable content
- **FR-015**: System MUST implement ISearchProvider for Jellyfin search integration
- **FR-016**: System MUST convert search results to ChannelItem with proper MediaType
- **FR-017**: System MUST provide stream URLs compatible with Jellyfin's media player
- **FR-018**: System MUST support IMDB/TMDB ID extraction for metadata enrichment

**Anonymous Networking**

- **FR-019**: System MUST route BitTorrent peer connections through established Tribler circuits
- **FR-020**: System MUST implement TunnelSocketConnector that wraps connections in circuit routing
- **FR-021**: System MUST provide fallback behavior when circuits unavailable (configurable: wait, fail, or prompt)
- **FR-022**: System MUST expose network availability status (circuits available, anonymity level)
- **FR-023**: System MUST NOT expose user's real IP to BitTorrent swarm peers when anonymous mode enabled

**Configuration**

- **FR-024**: System MUST persist indexer configurations (URL, API key, enabled status)
- **FR-025**: System MUST allow per-user indexer configurations via Jellyfin user settings
- **FR-026**: System MUST provide configuration UI in Jellyfin plugin settings page

### Key Entities

- **TorrentManager**: Represents an active torrent download/stream session with MonoTorrent
- **TorrentSearchResult**: Normalized search result from any indexer (title, size, seeders, infohash, magnet)
- **IndexerConfig**: Configuration for a single indexer (type, URL, API key, rate limits)
- **StreamSession**: Active streaming session linking TorrentManager to HTTP endpoint
- **CircuitConnection**: Wrapper around peer connection that routes through Tribler circuit

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Video playback starts within 30 seconds of adding magnet link (with seeders available)
- **SC-002**: HTTP streaming supports seeking to any position with < 5 second response time
- **SC-003**: Indexer search returns results within 10 seconds for responsive indexers
- **SC-004**: System handles 10 concurrent streaming sessions without degradation
- **SC-005**: Anonymous mode hides user IP from 100% of swarm peers when circuits available
- **SC-006**: Jellyfin channel displays search results with correct metadata and artwork
- **SC-007**: Rate limiting prevents more than 1 request/second per indexer under load
- **SC-008**: Circuit failover completes within 10 seconds when primary circuit fails


## Technical Design *(mandatory)*

### Technology Stack

- **Runtime**: C# / .NET 10.0 (Jellyfin plugin requirement)
- **BitTorrent Engine**: MonoTorrent 3.0.2 (streaming support via StreamProvider)
- **Cryptography**: NSec.Cryptography 25.4.0 (Ed25519 for circuit encryption)
- **HTTP Parsing**: HtmlAgilityPack 1.11.x (for built-in indexer scrapers)
- **XML Parsing**: System.Xml.Linq (for Torznab XML responses)
- **Jellyfin Integration**: Jellyfin.Controller 10.11.5, Jellyfin.Model 10.11.5

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Jellyfin Server                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │
│  │ TunnelFin       │  │ TunnelFin       │  │ TunnelFin           │  │
│  │ Channel         │  │ SearchProvider  │  │ MediaSourceProvider │  │
│  └────────┬────────┘  └────────┬────────┘  └──────────┬──────────┘  │
└───────────┼─────────────────────┼─────────────────────┼─────────────┘
            │                     │                     │
            ▼                     ▼                     ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      TunnelFin Core Layer                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │
│  │ IndexerManager  │  │ StreamManager   │  │ TorrentEngine       │  │
│  │ (Torznab, 1337x)│  │ (HTTP endpoints)│  │ (MonoTorrent)       │  │
│  └────────┬────────┘  └────────┬────────┘  └──────────┬──────────┘  │
└───────────┼─────────────────────┼─────────────────────┼─────────────┘
            │                     │                     │
            ▼                     ▼                     ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Network Transport Layer                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │
│  │ CircuitManager  │  │ TunnelProxy     │  │ TunnelSocketConnector│  │
│  │ (Tribler)       │  │ (Onion routing) │  │ (MonoTorrent hook)  │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Specifications

#### 1. TorrentEngine (MonoTorrent Integration)

**File**: `src/TunnelFin/BitTorrent/TorrentEngine.cs`

```csharp
public class TorrentEngine : IDisposable
{
    private readonly ClientEngine _engine;
    private readonly ConcurrentDictionary<string, TorrentManager> _managers;

    // Initialize with streaming-optimized settings
    public TorrentEngine(TorrentEngineSettings settings)
    {
        var engineSettings = new EngineSettings
        {
            AllowMultipleConnectionsPerIp = true,
            MaxConnectionsTotal = settings.MaxConnections,
            MaxConnectionsPerTorrent = settings.MaxConnectionsPerTorrent,
            DiskCacheEnabled = true,
            DiskCacheMaxBytes = settings.DiskCacheBytes,
            HttpStreamingPrefix = settings.HttpStreamingPrefix
        };
        _engine = new ClientEngine(engineSettings);
    }

    // Add torrent with streaming support
    public async Task<TorrentManager> AddStreamingAsync(
        string magnetLink,
        string downloadPath,
        CancellationToken ct = default);

    // Wait for metadata (BEP-9)
    public async Task WaitForMetadataAsync(
        TorrentManager manager,
        TimeSpan timeout,
        CancellationToken ct = default);

    // Create HTTP stream for file
    public async Task<IHttpStream> CreateHttpStreamAsync(
        TorrentManager manager,
        ITorrentManagerFile file,
        bool prebuffer = true,
        CancellationToken ct = default);
}
```

#### 2. TorznabIndexer (Full Implementation)

**File**: `src/TunnelFin/Indexers/Torznab/TorznabIndexer.cs`

```csharp
public class TorznabIndexer : IIndexer
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;
    private readonly TimeSpan _requestDelay = TimeSpan.FromSeconds(1);

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        ContentType contentType,
        CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            // Build Torznab query URL
            var url = BuildSearchUrl(query, contentType);

            // Fetch XML response
            var xml = await _httpClient.GetStringAsync(url, ct);

            // Parse torznab:attr elements
            return ParseTorznabResponse(xml);
        }
        finally
        {
            _ = Task.Delay(_requestDelay).ContinueWith(_ => _rateLimiter.Release());
        }
    }

    private List<SearchResult> ParseTorznabResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://torznab.com/schemas/2015/feed");

        return doc.Descendants("item").Select(item => new SearchResult
        {
            Title = item.Element("title")?.Value ?? "",
            Size = GetAttrValue(item, ns, "size"),
            Seeders = GetAttrValue(item, ns, "seeders"),
            Leechers = GetAttrValue(item, ns, "peers"),
            InfoHash = GetAttrValue(item, ns, "infohash"),
            MagnetLink = BuildMagnetLink(item, ns)
        }).ToList();
    }
}
```

#### 3. Built-in Indexer Scraper (1337x Example)

**File**: `src/TunnelFin/Indexers/BuiltIn/Indexer1337x.cs`

```csharp
public class Indexer1337x : IIndexer
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _rateLimiter;

    public async Task<List<SearchResult>> SearchAsync(
        string query,
        ContentType contentType,
        CancellationToken ct = default)
    {
        await _rateLimiter.WaitAsync(ct);
        try
        {
            var url = $"https://1337x.to/search/{Uri.EscapeDataString(query)}/1/";
            var html = await _httpClient.GetStringAsync(url, ct);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return ParseSearchResults(doc);
        }
        finally
        {
            _ = Task.Delay(TimeSpan.FromSeconds(1))
                .ContinueWith(_ => _rateLimiter.Release());
        }
    }

    private List<SearchResult> ParseSearchResults(HtmlDocument doc)
    {
        var results = new List<SearchResult>();
        var rows = doc.DocumentNode.SelectNodes("//table[@class='table-list']//tr");

        foreach (var row in rows?.Skip(1) ?? Enumerable.Empty<HtmlNode>())
        {
            var title = row.SelectSingleNode(".//td[1]//a[2]")?.InnerText;
            var seeders = row.SelectSingleNode(".//td[2]")?.InnerText;
            var leechers = row.SelectSingleNode(".//td[3]")?.InnerText;
            var size = row.SelectSingleNode(".//td[5]")?.InnerText;
            var detailUrl = row.SelectSingleNode(".//td[1]//a[2]")?.GetAttributeValue("href", "");

            if (!string.IsNullOrEmpty(title))
            {
                results.Add(new SearchResult
                {
                    Title = title.Trim(),
                    Seeders = int.TryParse(seeders, out var s) ? s : 0,
                    Leechers = int.TryParse(leechers, out var l) ? l : 0,
                    Size = ParseSize(size),
                    DetailUrl = detailUrl
                });
            }
        }
        return results;
    }
}
```

#### 4. Jellyfin Channel Provider

**File**: `src/TunnelFin/Jellyfin/TunnelFinChannel.cs`

```csharp
public class TunnelFinChannel : IChannel, IHasChangeEvent
{
    public string Name => "TunnelFin";
    public string Description => "Stream torrents anonymously";
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    public async Task<ChannelItemResult> GetChannelItems(
        InternalChannelItemQuery query,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(query.SearchTerm))
        {
            // Return categories
            return new ChannelItemResult
            {
                Items = GetCategories().Select(c => new ChannelItemInfo
                {
                    Name = c,
                    Type = ChannelItemType.Folder,
                    Id = c
                }).ToList()
            };
        }

        // Search indexers
        var results = await _indexerManager.SearchAsync(query.SearchTerm, ct);

        return new ChannelItemResult
        {
            Items = results.Select(r => new ChannelItemInfo
            {
                Name = r.Title,
                Type = ChannelItemType.Media,
                MediaType = ChannelMediaType.Video,
                Id = r.InfoHash,
                Overview = $"Size: {FormatSize(r.Size)} | Seeders: {r.Seeders}"
            }).ToList()
        };
    }
}
```

#### 5. TunnelSocketConnector (Circuit Integration)

**File**: `src/TunnelFin/Networking/Tunnel/TunnelSocketConnector.cs`

The existing implementation is mostly complete. Key additions needed:

```csharp
// Add connection pooling for circuit reuse
private readonly ConcurrentDictionary<uint, CircuitConnection> _circuitPool;

// Add metrics tracking
public CircuitConnectionMetrics GetMetrics() => new CircuitConnectionMetrics
{
    ActiveCircuits = _circuitPool.Count,
    TotalBytesRouted = _totalBytesRouted,
    AverageLatencyMs = _latencyTracker.Average
};
```

### Data Flow

1. **Search Flow**:
   ```
   User Search → IndexerManager → [Torznab API | HTML Scraper] → SearchResult[]
   ```

2. **Stream Flow**:
   ```
   Select Result → TorrentEngine.AddStreamingAsync → WaitForMetadata
   → CreateHttpStreamAsync → HTTP URL → Jellyfin Player
   ```

3. **Anonymous Connection Flow**:
   ```
   MonoTorrent PeerConnect → TunnelSocketConnector → CircuitManager
   → TunnelProxy → Circuit Hops → Exit Node → Peer
   ```

### Configuration Schema

```csharp
public class TunnelFinConfiguration
{
    // Streaming settings
    public string HttpStreamingPrefix { get; set; } = "/tunnelfin/stream/";
    public int MaxConcurrentStreams { get; set; } = 3;
    public int MetadataTimeoutSeconds { get; set; } = 90;
    public long DiskCacheBytes { get; set; } = 100 * 1024 * 1024; // 100MB

    // Indexer settings
    public List<IndexerConfig> Indexers { get; set; } = new();
    public int IndexerTimeoutSeconds { get; set; } = 15;
    public int MaxResultsPerIndexer { get; set; } = 50;

    // Anonymity settings
    public bool RequireAnonymousRouting { get; set; } = true;
    public bool AllowNonAnonymousFallback { get; set; } = false;
    public int DefaultHopCount { get; set; } = 3;
}

public class IndexerConfig
{
    public string Name { get; set; } = "";
    public IndexerType Type { get; set; }
    public string Url { get; set; } = "";
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

public enum IndexerType
{
    Torznab,
    BuiltIn1337x,
    BuiltInNyaa,
    BuiltInTorrentGalaxy,
    BuiltInEZTV
}
```

### Error Handling Strategy

| Error Type | Handling | User Feedback |
|------------|----------|---------------|
| Metadata timeout | Cancel operation, cleanup | "Could not retrieve torrent metadata. Try a different source." |
| Indexer 429 | Exponential backoff | Silent retry, show cached results |
| Indexer 5xx | Mark unavailable 5min | "Indexer temporarily unavailable" |
| Circuit failure | Attempt failover | "Reconnecting..." or fallback prompt |
| No seeders | Allow start, warn user | "No seeders available - download may not start" |
| Disk full | Prevent start | "Insufficient disk space" |

### Testing Strategy

1. **Unit Tests**: Mock MonoTorrent, test parsing logic
2. **Integration Tests**: Real Torznab endpoint (local Jackett), real torrents (legal test files)
3. **E2E Tests**: Full flow from search to playback with Big Buck Bunny torrent


## Implementation Plan *(mandatory)*

### Phase 1: MonoTorrent Streaming Engine (P1)

**Goal**: Enable video streaming from magnet links

| Task | Description | Estimate |
|------|-------------|----------|
| T001 | Implement TorrentEngine wrapper with streaming settings | 4h |
| T002 | Implement AddStreamingAsync with magnet link parsing | 2h |
| T003 | Implement WaitForMetadataAsync with timeout | 2h |
| T004 | Implement CreateHttpStreamAsync with prebuffering | 3h |
| T005 | Wire StreamManager to TorrentEngine | 2h |
| T006 | Add HTTP Range request support verification | 2h |
| T007 | Unit tests for TorrentEngine | 3h |
| T008 | Integration test with Big Buck Bunny magnet | 2h |

**Deliverable**: Working video stream from magnet link via HTTP endpoint

### Phase 2: Indexer Integration (P1)

**Goal**: Enable content discovery through indexers

| Task | Description | Estimate |
|------|-------------|----------|
| T009 | Implement TorznabIndexer.SearchAsync with XML parsing | 4h |
| T010 | Implement rate limiting with SemaphoreSlim | 1h |
| T011 | Implement exponential backoff for errors | 2h |
| T012 | Implement Indexer1337x HTML scraper | 4h |
| T013 | Implement IndexerNyaa HTML scraper | 3h |
| T013a | Implement IndexerTorrentGalaxy HTML scraper | 3h |
| T013b | Implement IndexerEZTV HTML scraper | 2h |
| T014 | Update IndexerManager to aggregate results | 2h |
| T015 | Unit tests for Torznab XML parsing | 2h |
| T016 | Unit tests for HTML scraping | 2h |
| T017 | Integration test with local Jackett | 2h |

**Deliverable**: Search returns results from configured indexers

### Phase 3: Jellyfin Integration (P2)

**Goal**: Native Jellyfin channel and search experience

| Task | Description | Estimate |
|------|-------------|----------|
| T018 | Implement IChannel interface in TunnelFinChannel | 4h |
| T019 | Implement GetChannelItems with category support | 2h |
| T020 | Implement ISearchProvider for global search | 3h |
| T021 | Implement MediaSourceProvider for stream URLs | 3h |
| T022 | Add configuration UI page (HTML/JS) | 4h |
| T023 | Integration test with Jellyfin test server | 3h |

**Deliverable**: TunnelFin appears as channel in Jellyfin with working search

### Phase 4: Anonymous Networking (P2)

**Goal**: Route BitTorrent traffic through Tribler circuits

| Task | Description | Estimate |
|------|-------------|----------|
| T024 | Add connection pooling to TunnelSocketConnector | 3h |
| T025 | Implement circuit failover logic | 3h |
| T026 | Add metrics tracking for circuit connections | 2h |
| T027 | Implement network availability check | 2h |
| T028 | Add UI indicator for anonymity status | 2h |
| T029 | Integration test with circuit routing | 4h |

**Deliverable**: Peer connections route through circuits when available

### Phase 5: Polish & Testing (P3)

**Goal**: Production-ready quality

| Task | Description | Estimate |
|------|-------------|----------|
| T030 | End-to-end test: search → stream → playback | 4h |
| T031 | Performance testing (10 concurrent streams) | 3h |
| T032 | Error handling edge cases | 3h |
| T033 | Documentation and configuration guide | 2h |

**Total Estimate**: ~90 hours (includes TorrentGalaxy + EZTV scrapers)

### Dependencies

```
Phase 1 (Streaming) ──┬──► Phase 3 (Jellyfin)
                      │
Phase 2 (Indexers) ───┘

Phase 1 (Streaming) ──────► Phase 4 (Anonymous)

All Phases ───────────────► Phase 5 (Polish)
```

## Open Questions

*All questions resolved - see Clarifications section above.*

~~1. **Indexer Selection**: Should we implement RARBG scraper given the site's uncertain status, or focus on more stable alternatives like TorrentGalaxy?~~
**Resolved**: Replace RARBG with TorrentGalaxy and EZTV.

~~2. **Metadata Enrichment**: Should we automatically fetch IMDB/TMDB metadata for search results, or defer to Jellyfin's existing metadata providers?~~
**Resolved**: Defer to Jellyfin's existing metadata providers.

~~3. **Download Persistence**: Should torrents persist across Jellyfin restarts, or should they be ephemeral (stream-only)?~~
**Resolved**: Ephemeral (stream-only).

~~4. **Multi-file Torrents**: How should we handle torrents with multiple video files? Present as playlist or individual items?~~
**Resolved**: Present as playlist with file selection.

~~5. **Subtitle Support**: Should we integrate OpenSubtitles or similar for automatic subtitle fetching?~~
**Resolved**: Use Jellyfin's existing subtitle plugins.

## References

- [MonoTorrent Streaming Documentation](https://github.com/alanmcgovern/monotorrent)
- [Torznab Specification v1.3](https://torznab.github.io/spec-1.3-draft/torznab/Specification-v1.3.html)
- [Jellyfin Plugin Development](https://jellyfin.org/docs/general/server/plugins/)
- [Tribler IPv8 Protocol](https://github.com/Tribler/py-ipv8)
- [Gelato Plugin Reference](https://github.com/gelato-jellyfin/gelato) - Similar Jellyfin torrent integration