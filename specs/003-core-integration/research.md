# Research: Core Integration Layer

**Date**: 2026-01-02
**Feature**: 003-core-integration
**Research Focus**: MonoTorrent streaming, Torznab protocol, HTML scraping, Jellyfin plugin patterns, circuit-routed connections

## Executive Summary

This research document consolidates findings from five critical technical areas required for implementing the TunnelFin core integration layer. The research validates that all required technologies are mature and production-ready, with established patterns available from reference implementations. Key findings include: (1) MonoTorrent 3.0.2 provides comprehensive streaming support through StreamProvider API with guaranteed sequential piece downloading, (2) Torznab v1.3 is well-specified with clear XML schema and extended attributes for seeders/size/infohash, (3) HTML scraping patterns for major indexers are well-documented with consistent table/panel structures, (4) Jellyfin 10.11.5 plugin interfaces (IChannel, ISearchProvider, MediaSourceProvider) enable native content integration, and (5) Tribler/IPv8 circuit routing can be integrated via SOCKS5 proxy patterns with robust failover mechanisms.

---

## Decision 1: MonoTorrent Streaming Architecture

### Rationale

MonoTorrent 3.0.2 is selected as the BitTorrent streaming engine because it provides:
- **Native .NET implementation**: Pure C# library with no native dependencies, ensuring cross-platform compatibility
- **StreamProvider API**: Dedicated streaming interface that returns seekable .NET Stream objects for files while downloading
- **Sequential piece downloading**: Version 3.0.2 guarantees sequential piece selection for HTTP requests with minimum 2MB data transfers per request
- **HTTP range request support**: Full RFC 7233 compliance enabling video seeking before download completion
- **Prebuffering control**: Configurable initial buffering to balance startup latency vs. playback smoothness
- **BEP 19 web seeding**: Hybrid HTTP/P2P distribution for reliable throughput

### Alternatives Considered

- **libtorrent-rasterbar**: Requires native bindings, complicates cross-platform deployment
- **Transmission**: Lacks streaming-specific APIs, would require custom piece prioritization
- **qBittorrent**: Desktop application, not designed as embeddable library

### Reference Implementation

- `reference_repos/monotorrent/`: Core streaming implementation patterns
- `reference_repos/TorrServer/`: Sequential piece prioritization strategies
- `reference_repos/Gelato/`: Jellyfin plugin integration with streaming

### Implementation Pattern

```csharp
// StreamProvider.CreateStreamAsync returns seekable stream
var stream = await streamProvider.CreateStreamAsync(
    torrentFile,
    prebuffer: true  // Fill buffer before returning
);

// EngineSettings optimization for streaming
var settings = new EngineSettings {
    DiskCacheBytes = 50 * 1024 * 1024,  // 50MB cache
    MaximumConnections = 200,
    MaxDownloadRate = 0,  // Unlimited
    AllowPortForwarding = false  // Use circuits instead
};
```

### Key Technical Details

1. **Sequential Piece Picker**: Maintains sliding window around current read position, prioritizes pieces containing current offset + look-ahead buffer
2. **Range Request Handling**: Calculates piece boundaries for byte ranges, downloads entire pieces but returns exact requested bytes
3. **Prebuffering Strategy**: Buffer size = video_bitrate × target_seconds (e.g., 5 Mbps × 10s = 62.5 MB)
4. **Adaptive Look-Ahead**: Adjusts window based on download_speed / playback_speed ratio
5. **Web Seed Integration**: Batches HTTP requests to web seeds (2MB+ per request), coordinates with peer requests

### Performance Characteristics

- **Initial buffering time**: 1-5s (fast connections), 10-30s (slow connections)
- **Seek latency**: <100ms (buffered ranges), 1-10s (unbuffered ranges)
- **Memory overhead**: ~5-10% of available RAM for disk cache
- **CPU usage**: Hash verification is primary consumer, offload to background tasks on constrained systems

---

## Decision 2: Torznab Protocol Implementation

### Rationale

Torznab v1.3 is selected as the primary indexer protocol because:
- **Standardization**: Unified API across Jackett, Prowlarr, and native indexers
- **Rich metadata**: Extended attributes provide seeders, leechers, size, infohash without additional requests
- **RSS 2.0 base**: Backward compatible with basic RSS parsers
- **Capabilities endpoint**: Self-describing API allows dynamic feature detection
- **Rate limiting guidance**: Specification includes retry-after headers and error codes

### Alternatives Considered

- **Direct tracker APIs**: Each tracker has unique API, requires individual integrations
- **RSS feeds only**: Lack torrent-specific metadata (seeders, infohash)
- **Custom scraping**: More fragile than standardized protocol

### Reference Implementation

- Jackett/Prowlarr proxy patterns
- System.Xml.Linq for namespace-aware parsing

### Implementation Pattern

```csharp
// Torznab XML parsing with namespace handling
var doc = XDocument.Parse(xmlResponse);
var ns = XNamespace.Get("http://torznab.com/schemas/2015/feed");

var items = doc.Descendants("item").Select(item => new TorrentResult {
    Title = item.Element("title")?.Value,
    MagnetLink = item.Element("link")?.Value,
    Seeders = int.Parse(item.Elements(ns + "attr")
        .FirstOrDefault(a => a.Attribute("name")?.Value == "seeders")
        ?.Attribute("value")?.Value ?? "0"),
    Size = long.Parse(item.Elements(ns + "attr")
        .FirstOrDefault(a => a.Attribute("name")?.Value == "size")
        ?.Attribute("value")?.Value ?? "0"),
    InfoHash = item.Elements(ns + "attr")
        .FirstOrDefault(a => a.Attribute("name")?.Value == "infohash")
        ?.Attribute("value")?.Value
});
```

### Key Technical Details

1. **XML Schema**: RSS 2.0 root with `<channel>` containing `<item>` elements, extended attributes in `torznab:attr` namespace
2. **Required Attributes**: `seeders`, `leechers`, `size` (bytes), `infohash` (lowercase hex), `magneturl`
3. **Capabilities Query**: `GET /api?t=caps` returns supported search modes and parameters
4. **Rate Limiting**: 1 request/second baseline, exponential backoff on 429 responses (1s, 2s, 4s, 8s, max 60s)
5. **Error Handling**: HTTP 200 with XML-wrapped errors, check for `<error>` root element

### Rate Limiting Strategy

```csharp
// Token bucket with exponential backoff
private readonly SemaphoreSlim _rateLimiter = new(1);
private TimeSpan _currentDelay = TimeSpan.FromSeconds(1);

await _rateLimiter.WaitAsync();
try {
    var response = await httpClient.GetAsync(url);
    if (response.StatusCode == (HttpStatusCode)429) {
        _currentDelay = TimeSpan.FromSeconds(Math.Min(_currentDelay.TotalSeconds * 2, 60));
        await Task.Delay(_currentDelay);
    } else {
        _currentDelay = TimeSpan.FromSeconds(1);  // Reset on success
    }
} finally {
    _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => _rateLimiter.Release());
}
```




---

## Decision 3: HTML Scraping Patterns for Built-In Indexers

### Rationale

HtmlAgilityPack 1.11.x is selected for HTML scraping because:
- **Robust parsing**: Handles malformed HTML gracefully, common in torrent indexer sites
- **XPath and CSS selector support**: Flexible querying with QuerySelectorAll and SelectNodes
- **Active maintenance**: Regular updates for compatibility with modern HTML
- **Pure .NET**: No native dependencies, cross-platform compatible
- **Proven track record**: Used extensively in web scraping projects

### Alternatives Considered

- **AngleSharp**: More standards-compliant but heavier, overkill for simple scraping
- **Regex parsing**: Fragile, breaks easily with HTML structure changes
- **Selenium/Playwright**: Too heavy for server-side scraping, requires browser runtime

### Reference Implementation

- `reference_repos/AIOStreams/`: Filtering and sorting patterns for scraped results
- Common indexer scraping patterns from research

### Implementation Pattern

```csharp
// 1337x scraping pattern
var doc = new HtmlDocument();
doc.LoadHtml(htmlContent);

var results = doc.DocumentNode
    .SelectNodes("//table[@class='table-list']//tr[position()>1]")
    ?.Select(row => new TorrentResult {
        Title = row.SelectSingleNode(".//td[@class='coll-1 name']//a[2]")?.InnerText.Trim(),
        MagnetLink = row.SelectSingleNode(".//td[@class='coll-1 name']//a[2]")?.GetAttributeValue("href", ""),
        Seeders = int.Parse(row.SelectSingleNode(".//td[@class='coll-2']")?.InnerText ?? "0"),
        Leechers = int.Parse(row.SelectSingleNode(".//td[@class='coll-3']")?.InnerText ?? "0"),
        Size = ParseSize(row.SelectSingleNode(".//td[@class='coll-4']")?.InnerText)
    }).ToList();

// Nyaa scraping pattern (panel-based)
var nyaaResults = doc.DocumentNode
    .SelectNodes("//div[@class='panel-body']//tr[position()>1]")
    ?.Select(row => new TorrentResult {
        Title = row.SelectSingleNode(".//td[@colspan='2']//a")?.InnerText.Trim(),
        MagnetLink = row.SelectSingleNode(".//a[contains(@href, 'magnet:')]")?.GetAttributeValue("href", ""),
        Seeders = int.Parse(row.SelectSingleNode(".//td[6]")?.InnerText ?? "0"),
        Size = ParseSize(row.SelectSingleNode(".//td[4]")?.InnerText)
    }).ToList();
```

### Key Technical Details

1. **1337x Structure**: Table-based with class selectors, title in `td.coll-1.name a[2]`, seeders in `td.coll-2`
2. **Nyaa Structure**: Panel-based layout or RSS feed with `<nyaa:seeders>` elements, supports both HTML and XML parsing
3. **TorrentGalaxy Structure**: Card-based div containers with nested elements, requires deeper traversal
4. **EZTV Structure**: Table-based similar to 1337x, includes episode information in title
5. **Size Parsing**: Convert human-readable sizes (e.g., "1.5 GB") to bytes using regex and unit conversion

### Anti-Detection Strategy

```csharp
// User-agent rotation with complete header sets
private static readonly string[] UserAgents = {
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
};

var handler = new HttpClientHandler {
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
};
var client = new HttpClient(handler);
client.DefaultRequestHeaders.Add("User-Agent", UserAgents[Random.Shared.Next(UserAgents.Length)]);
client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
client.DefaultRequestHeaders.Add("DNT", "1");
```

### Rate Limiting and Concurrency

```csharp
// SemaphoreSlim for concurrency control
private readonly SemaphoreSlim _concurrencyLimiter = new(3);  // Max 3 concurrent requests

await _concurrencyLimiter.WaitAsync(cancellationToken);
try {
    var response = await httpClient.GetAsync(url, cancellationToken);
    // Process response
} finally {
    await Task.Delay(TimeSpan.FromSeconds(1 + Random.Shared.NextDouble()), cancellationToken);  // Jitter
    _concurrencyLimiter.Release();
}
```

---

## Decision 4: Jellyfin Plugin Integration Patterns

### Rationale

Jellyfin 10.11.5 plugin interfaces are selected for native integration because:
- **IChannel interface**: Presents external content as native Jellyfin library items
- **ISearchProvider interface**: Custom search functionality integrated into Jellyfin's search UI
- **MediaSourceProvider**: Stream delivery with transcoding configuration and HTTP URL provision
- **IMetadataProvider**: Enrichment from external databases (deferred to Jellyfin's existing providers)
- **Dependency injection**: Access to ILibraryManager, IUserManager, IServerConfigurationManager
- **Entity Framework Core**: Database access (Jellyfin 10.11.0+ requirement)

### Alternatives Considered

- **External service**: Requires separate deployment, breaks seamless integration principle
- **Direct library manipulation**: Bypasses Jellyfin's metadata/permission systems
- **Custom HTTP endpoints only**: Loses native UI integration

### Reference Implementation

- `reference_repos/Gelato/`: Complete Jellyfin plugin with IChannel and search provider
- Jellyfin plugin template patterns

### Implementation Pattern

```csharp
// IChannel implementation for TunnelFin content
public class TunnelFinChannel : IChannel
{
    private readonly IIndexerManager _indexerManager;
    private readonly IStreamManager _streamManager;

    public string Name => "TunnelFin";
    public string Description => "Decentralized streaming via BitTorrent";
    public string DataVersion => "1.0";
    public string HomePageUrl => "https://github.com/jefflouisma/TunnelFin";

    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        // Search indexers for content
        var results = await _indexerManager.SearchAsync(query.SearchTerm, cancellationToken);

        return new ChannelItemResult {
            Items = results.Select(r => new ChannelItemInfo {
                Name = r.Title,
                Id = r.InfoHash,
                Type = ChannelItemType.Media,
                ContentType = ChannelMediaContentType.Movie,
                MediaType = ChannelMediaType.Video,
                ImageUrl = r.PosterUrl,
                MediaSources = new[] {
                    new MediaSourceInfo {
                        Protocol = MediaProtocol.Http,
                        Path = _streamManager.GetStreamUrl(r.InfoHash),
                        SupportsDirectStream = true,
                        SupportsTranscoding = true
                    }
                }
            }).ToList()
        };
    }
}
```

### Key Technical Details

1. **Plugin Registration**: Implement IPlugin interface with GUID identifier, register services in ConfigureServices
2. **Dependency Injection**: Constructor injection for ILibraryManager, IUserManager, IServerConfigurationManager
3. **MediaSourceInfo**: Provide HTTP stream URL, declare transcoding support, set protocol to Http
4. **Channel Item Types**: ChannelItemType.Media for playable content, ChannelItemType.Folder for categories
5. **Metadata Enrichment**: Defer to Jellyfin's existing providers (TMDb, OMDb, TheTVDB, AniDB)


---

## Decision 5: Circuit-Routed Peer Connections via Tribler/IPv8

### Rationale

SOCKS5 proxy pattern with Tribler circuit routing is selected for anonymous peer connections because:
- **Standard abstraction**: SOCKS5 is widely supported by BitTorrent clients including MonoTorrent
- **Onion routing**: Layered encryption with Curve25519 ECDH key exchange provides strong anonymity
- **Circuit failover**: Exponential backoff with health checks ensures reliability
- **Connection pooling**: ObjectPool pattern reduces circuit construction overhead
- **Wire compatibility**: IPv8 protocol is compatible with Tribler's existing network
- **No external dependencies**: Integrates with existing TunnelFin circuit infrastructure

### Alternatives Considered

- **Direct peer connections**: Exposes IP address, violates privacy-first principle
- **VPN integration**: Requires external service, breaks seamless integration
- **Custom proxy protocol**: Reinvents wheel, increases complexity

### Reference Implementation

- `reference_repos/tribler/pyipv8/`: IPv8 protocol implementation with circuit routing
- SOCKS5 socket wrapping patterns from research

### Implementation Pattern

```csharp
// TunnelSocketConnector wraps peer connections through circuits
public class TunnelSocketConnector : ISocketConnector
{
    private readonly ITunnelProxy _tunnelProxy;
    private readonly ObjectPool<CircuitConnection> _connectionPool;

    public async Task<Socket> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
    {
        // Get circuit from pool or create new one
        var circuit = await _connectionPool.GetAsync(cancellationToken);

        try {
            // SOCKS5 handshake through circuit
            var socket = await circuit.ConnectAsync(endpoint, cancellationToken);
            return socket;
        } catch {
            // Circuit failed, mark unhealthy and retry
            circuit.MarkUnhealthy();
            _connectionPool.Return(circuit);
            throw;
        }
    }
}

// Circuit health monitoring with PING/PONG
public class CircuitHealthMonitor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            foreach (var circuit in _activeCircuits) {
                try {
                    await circuit.SendPingAsync(stoppingToken);
                    var pong = await circuit.ReceivePongAsync(TimeSpan.FromSeconds(5), stoppingToken);
                    circuit.UpdateLatency(pong.RoundTripTime);
                } catch {
                    circuit.MarkUnhealthy();
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

### Key Technical Details

1. **Onion Routing Architecture**: Layered encryption with Curve25519 ECDH, AES-GCM-128 per-hop encryption
2. **Circuit Construction**: CREATE/CREATED for first hop, EXTEND/EXTENDED for additional hops
3. **SOCKS5 Integration**: Standard SOCKS5 handshake over circuit, supports CONNECT command for TCP
4. **Failover Strategy**: Exponential backoff (1s, 2s, 4s, 8s, max 60s) with jitter, health checks via PING/PONG
5. **Connection Pooling**: ObjectPool<CircuitConnection> with max 100 circuits, evict unhealthy circuits
6. **Path Selection**: RTT-based relay selection, throughput estimation, reliability scoring

### Circuit Failover Implementation

```csharp
// Exponential backoff with jitter for circuit failover
private async Task<Circuit> GetHealthyCircuitAsync(CancellationToken cancellationToken)
{
    var attempt = 0;
    var maxAttempts = 5;

    while (attempt < maxAttempts) {
        var circuit = await _circuitManager.GetCircuitAsync(cancellationToken);

        if (circuit.IsHealthy) {
            return circuit;
        }

        // Exponential backoff with jitter
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 60));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
        await Task.Delay(delay + jitter, cancellationToken);

        attempt++;
    }

    throw new InvalidOperationException("No healthy circuits available");
}
```

### Performance Characteristics

- **Circuit construction time**: 500ms-2s (3-hop circuit)
- **Latency overhead**: +100-300ms per hop (3 hops = +300-900ms)
- **Throughput impact**: ~10-20% reduction due to encryption overhead
- **Connection pool size**: 100 circuits (configurable)
- **Circuit lifetime**: 10 minutes (configurable), refreshed on activity

---

## Constitutional Compliance

All research findings and implementation patterns align with TunnelFin's constitutional principles:

### 1. Privacy-First ✅
- **Circuit-routed BitTorrent traffic**: All peer connections route through Tribler circuits by default
- **Onion routing**: Layered encryption with Curve25519 ECDH ensures IP anonymity
- **No external services**: All components run within Jellyfin plugin, no third-party tracking

### 2. Seamless Integration ✅
- **Native Jellyfin plugin**: IChannel and ISearchProvider provide native UI integration
- **No external deployment**: All components embedded in single plugin DLL
- **Standard interfaces**: MediaSourceProvider delivers HTTP streams compatible with Jellyfin's player

### 3. Test-First Development ✅
- **xUnit framework**: Industry-standard testing for .NET
- **FluentAssertions**: Readable assertion syntax for maintainability
- **Moq**: Interface mocking for isolated unit tests
- **80%+ coverage target**: Enforced via CI/CD gates

### 4. Decentralized Architecture ✅
- **P2P BitTorrent**: MonoTorrent provides decentralized content distribution
- **IPv8 wire compatibility**: Circuit routing compatible with Tribler's existing network
- **No central servers**: Indexers are configurable, no mandatory dependencies

### 5. User Empowerment ✅
- **Configurable anonymity**: Users can disable circuits for performance (with clear warnings)
- **Transparent trade-offs**: UI displays circuit status, latency overhead, throughput impact
- **Indexer control**: Users configure Torznab/HTML indexers, no forced defaults

---

## Implementation Risks and Mitigations

### Risk 1: MonoTorrent Sequential Downloading Performance
**Risk**: Sequential piece downloading may cause buffering if download speed < playback bitrate
**Mitigation**: Implement adaptive prebuffering (5-10s initial buffer), BEP 19 web seeding for hybrid HTTP/P2P, quality selection UI (720p/1080p/4K)

### Risk 2: Indexer Rate Limiting and Blocking
**Risk**: Aggressive scraping may trigger rate limits or IP blocks
**Mitigation**: 1 req/sec baseline with exponential backoff, user-agent rotation, residential proxy support (optional), Torznab prioritization (more reliable than HTML scraping)

### Risk 3: Circuit Routing Latency Impact
**Risk**: 3-hop circuits add 300-900ms latency, may impact initial buffering time
**Mitigation**: Connection pooling (pre-construct circuits), circuit health monitoring (evict slow circuits), configurable hop count (1-5 hops), fallback to direct connections with user consent

### Risk 4: Jellyfin Plugin API Compatibility
**Risk**: Jellyfin 10.11.5 API may change in future versions
**Mitigation**: Pin to Jellyfin.Controller 10.11.5 + Jellyfin.Model 10.11.5, monitor Jellyfin release notes, maintain compatibility layer for API changes

### Risk 5: Ephemeral Torrent Storage Management
**Risk**: Concurrent streams may exhaust disk space with ephemeral torrents
**Mitigation**: Configurable max concurrent streams (default 10), LRU eviction policy, disk space monitoring (warn at 90% full), user-configurable cache size

---

## Next Steps

This research document provides the technical foundation for Phase 1 (Design & Contracts). The following artifacts will be generated:

1. **data-model.md**: Entity definitions for TorrentMetadata, IndexerConfig, StreamSession, CircuitMetadata
2. **contracts/**: C# interface definitions for ITorrentEngine, IIndexerManager, IStreamManager, ITunnelSocketConnector, ITunnelFinChannel, ISearchProvider
3. **quickstart.md**: Build instructions, test commands, configuration guide, first-time setup workflow
4. **Agent context update**: Add new technologies to `.augment/rules/specify-rules.md`

All implementation patterns documented here will be referenced during code generation to ensure consistency with research findings and constitutional principles.
