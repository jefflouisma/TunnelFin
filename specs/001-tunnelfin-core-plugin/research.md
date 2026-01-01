# Research: TunnelFin Core Plugin

**Date**: January 1, 2026  
**Feature**: 001-tunnelfin-core-plugin  
**Purpose**: Resolve technical unknowns and document implementation patterns

## Research Areas

### 1. IPv8 Protocol Implementation in C#

**Decision**: Implement wire-compatible IPv8 protocol subset focusing on tunnel community and circuit management

**Rationale**:
- Tribler's IPv8 is Python-based; no existing C# implementation exists
- Wire compatibility is essential to join existing Tribler network (Constitution: Decentralized Architecture)
- Full protocol reimplementation is unnecessary; focus on tunnel overlay and circuit management
- Reference: `tribler/src/tribler/core/tunnel/community.py` provides canonical implementation

**Key Components to Implement**:
1. **Peer Discovery**: DHT-based peer finding and handshake protocol
2. **Circuit Creation**: Multi-hop circuit establishment with relay selection
3. **Data Tunneling**: Onion-encrypted packet routing through circuits
4. **Ed25519 Identity**: Cryptographic peer identity using NSec.Cryptography
5. **SOCKS5 Proxy**: Local proxy for routing BitTorrent traffic through circuits

**Alternatives Considered**:
- **Alternative A**: Use Tribler as external service via IPC → Rejected: Violates self-contained principle, adds Docker dependency
- **Alternative B**: Implement custom anonymity protocol → Rejected: No existing network to join, would be isolated
- **Alternative C**: Use I2P or Tor → Rejected: Not optimized for BitTorrent, different threat model

**Implementation Pattern** (from `tribler/src/tribler/core/tunnel/`):
```python
# Reference pattern from Tribler
class TunnelCommunity:
    def create_circuit(self, hops):
        # 1. Select relay peers from known peers
        # 2. Send CREATE message to first hop
        # 3. Extend circuit hop-by-hop with EXTEND messages
        # 4. Establish shared keys using DH key exchange
        # 5. Return circuit ID for data tunneling
```

**C# Translation Strategy**:
- Use NSec.Cryptography for Ed25519 and X25519 (DH key exchange)
- Implement message serialization with System.Buffers for performance
- Use async/await for non-blocking network I/O
- Reference MonoTorrent's peer communication patterns for socket management

---

### 2. Sequential Torrent Streaming with MonoTorrent

**Decision**: Use MonoTorrent.Streaming namespace with custom piece prioritization strategy

**Rationale**:
- MonoTorrent 3.0.2 includes built-in streaming support (`MonoTorrent.Streaming`)
- Sequential piece download is critical for immediate playback (SC-001: <30s to stream start)
- Reference: `monotorrent/src/MonoTorrent.Client/MonoTorrent.Streaming/` shows streaming API
- TorrServer (Go) demonstrates sequential prioritization patterns

**Key Implementation Details**:
1. **Piece Prioritization**: Override `IPieceRequester` to prioritize sequential pieces
2. **Buffer Management**: Maintain 10+ second buffer ahead of playback position (SC-003)
3. **HTTP Streaming**: Expose torrent data via local HTTP endpoint for Jellyfin player
4. **Partial File Handling**: Handle incomplete pieces gracefully during streaming

**Alternatives Considered**:
- **Alternative A**: Implement BitTorrent from scratch → Rejected: Reinventing wheel, MonoTorrent is mature and maintained
- **Alternative B**: Use libtorrent-rasterbar via P/Invoke → Rejected: Cross-platform complexity, native C# preferred
- **Alternative C**: Random piece selection → Rejected: Prevents immediate playback, violates SC-001

**Implementation Pattern** (from MonoTorrent.Streaming):
```csharp
// Reference pattern from MonoTorrent
var manager = await engine.AddStreamingAsync(torrent, downloadPath);
var stream = await manager.StreamProvider.CreateStreamAsync(file);
// Expose stream via HTTP endpoint for Jellyfin
```

**Custom Piece Prioritizer**:
```csharp
public class SequentialPrioritizer : IPieceRequester
{
    public void RequestPieces(IPeer peer, ReadOnlySpan<BlockInfo> available)
    {
        // Prioritize pieces sequentially from current playback position
        // Maintain buffer window (e.g., next 50 pieces)
        // Deprioritize pieces far ahead to avoid wasting bandwidth
    }
}
```

---

### 3. Advanced Filtering and Sorting Engine

**Decision**: Implement expression-based filtering with multi-criteria sorting inspired by AIOStreams

**Rationale**:
- FR-019 through FR-024 require sophisticated filtering (Required/Preferred/Excluded/Include)
- FR-022 requires conditional filtering with expression language
- FR-023 requires multi-criteria sorting (e.g., "resolution desc, seeders desc, size asc")
- Reference: `AIOStreams/packages/` shows TypeScript implementation patterns

**Key Components**:
1. **Filter Rule Engine**: Parse and evaluate filter expressions
2. **Attribute Extractors**: Parse resolution, quality, codecs from torrent filenames
3. **Conditional Logic**: Support expressions like "exclude 720p if >5 results at 1080p"
4. **Profile Management**: Separate profiles for Movies, TV Shows, Anime
5. **Deduplication**: Smart hash generation from file attributes (FR-025)

**Alternatives Considered**:
- **Alternative A**: Simple keyword matching → Rejected: Insufficient for power users (FR-020, FR-021, FR-022)
- **Alternative B**: Use external library (e.g., Flee expression evaluator) → Rejected: Adds dependency, overkill for domain-specific rules
- **Alternative C**: Hardcoded filter logic → Rejected: Not extensible, violates user empowerment principle

**Implementation Pattern**:
```csharp
public class FilterEngine
{
    public IEnumerable<SearchResult> ApplyFilters(
        IEnumerable<SearchResult> results,
        FilterProfile profile)
    {
        // 1. Apply Required filters (must match)
        // 2. Apply Excluded filters (must not match)
        // 3. Apply Include whitelist (if specified)
        // 4. Score results based on Preferred filters
        // 5. Evaluate conditional expressions
        return filteredResults;
    }
}

public class SortEngine
{
    public IEnumerable<SearchResult> Sort(
        IEnumerable<SearchResult> results,
        SortCriteria[] criteria)
    {
        // Multi-level sorting using LINQ OrderBy().ThenBy()
        return results.OrderByDescending(r => r.Resolution)
                     .ThenByDescending(r => r.Seeders)
                     .ThenBy(r => r.FileSize);
    }
}
```

**Attribute Parsing** (from torrent filename):
- Resolution: Regex `(720p|1080p|2160p|4K)`
- Quality: Regex `(WEB-DL|BluRay|BRRip|HDTV|CAM|TS)`
- Codecs: Regex `(x264|x265|HEVC|AV1|AAC|DTS|Atmos)`

---

### 4. Jellyfin Plugin Integration Patterns

**Decision**: Follow Gelato's plugin architecture with dependency injection and provider pattern

**Rationale**:
- Gelato demonstrates proven Jellyfin plugin patterns for search and channel providers
- Jellyfin uses Microsoft.Extensions.DependencyInjection for service registration
- Reference: `Gelato/Plugin.cs` shows plugin lifecycle and service registration

**Key Integration Points**:
1. **Plugin Entry Point**: Implement `IPlugin` interface
2. **Search Provider**: Implement `ISearchProvider` for native search bar integration (FR-027)
3. **Channel Provider**: Implement `IChannel` for content presentation (FR-028)
4. **Configuration**: Use Jellyfin's configuration API for settings persistence (FR-038)
5. **Metadata Provider**: Integrate with Jellyfin's metadata system (FR-029)

**Implementation Pattern** (from Gelato):
```csharp
public class TunnelFinPlugin : BasePlugin<PluginConfiguration>
{
    public override void RegisterServices(IServiceCollection serviceCollection)
    {
        // Register all plugin services
        serviceCollection.AddSingleton<ICircuitManager, CircuitManager>();
        serviceCollection.AddSingleton<ITorrentEngine, TorrentEngine>();
        serviceCollection.AddSingleton<IIndexerManager, IndexerManager>();
        // ... other services
    }
}

public class TunnelFinSearchProvider : ISearchProvider
{
    public async Task<IEnumerable<SearchResult>> GetSearchResults(
        SearchQuery query, CancellationToken cancellationToken)
    {
        // 1. Query all enabled indexers
        // 2. Apply filters and sorting
        // 3. Deduplicate results
        // 4. Fetch metadata from TMDB/AniList
        // 5. Return as Jellyfin search results
    }
}
```

---

### 5. Metadata Fetching with Retry and Caching

**Decision**: Implement exponential backoff retry with failure caching for TMDB/AniList APIs

**Rationale**:
- FR-030: Retry with exponential backoff (1s, 2s, 4s) when services unreachable
- FR-031: Cache failures for 5 minutes to avoid repeated failed requests
- FR-032: Fallback to filename parsing when all retries fail
- SC-008: 95% metadata fetch success rate

**Implementation Pattern**:
```csharp
public class MetadataFetcher
{
    private readonly IMemoryCache _failureCache;
    private readonly HttpClient _httpClient;

    public async Task<Metadata> FetchMetadata(string title, int? year)
    {
        // Check failure cache first
        if (_failureCache.TryGetValue(cacheKey, out _))
            return ParseFromFilename(title);

        // Retry with exponential backoff
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var result = await _httpClient.GetAsync(tmdbUrl);
                if (result.IsSuccessStatusCode)
                    return await ParseResponse(result);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        // Cache failure for 5 minutes
        _failureCache.Set(cacheKey, true, TimeSpan.FromMinutes(5));
        return ParseFromFilename(title);
    }
}
```

---

## Summary of Technical Decisions

| Area | Technology/Pattern | Rationale |
|------|-------------------|-----------|
| IPv8 Protocol | Custom C# implementation with NSec.Cryptography | Wire compatibility with Tribler network |
| BitTorrent Streaming | MonoTorrent.Streaming with custom piece prioritizer | Mature library, built-in streaming support |
| Filtering/Sorting | Expression-based engine inspired by AIOStreams | Power user requirements (FR-019-FR-024) |
| Jellyfin Integration | Gelato patterns with DI and provider interfaces | Proven plugin architecture |
| Metadata Fetching | Exponential backoff + failure caching | Resilience requirements (FR-030-FR-032) |
| Cryptography | NSec.Cryptography (Ed25519, X25519) | Modern, audited crypto library |
| HTTP Client | Microsoft.Extensions.Http with Polly | Resilient HTTP with retry policies |
| Testing | xUnit + FluentAssertions + Moq | Standard .NET testing stack |

---

## Next Steps

Phase 0 research complete. Proceed to Phase 1: Design & Contracts.

