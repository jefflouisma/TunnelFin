# Implementation Plan: Core Integration Layer

**Branch**: `003-core-integration` | **Date**: 2026-01-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-core-integration/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

This feature implements the core integration layer that connects all TunnelFin components into a functional streaming system. The implementation includes: (1) MonoTorrent streaming engine with HTTP endpoints for video playback, (2) Torznab and HTML-based indexer scrapers for content discovery, (3) Jellyfin channel/search providers for native UI integration, and (4) circuit-routed peer connections for anonymous BitTorrent traffic. The technical approach leverages MonoTorrent 3.0.2's StreamProvider API for sequential piece downloading with range request support, implements Torznab v1.3 XML parsing with rate limiting, uses HtmlAgilityPack for scraping built-in indexers (1337x, Nyaa, TorrentGalaxy, EZTV), integrates with Jellyfin 10.11.5's IChannel/ISearchProvider interfaces, and wraps peer connections through Tribler circuit infrastructure using SOCKS5 proxy patterns.

## Technical Context

**Language/Version**: C# / .NET 10.0 (Jellyfin plugin requirement)
**Primary Dependencies**: MonoTorrent 3.0.2 (BitTorrent streaming), NSec.Cryptography 25.4.0 (Ed25519), HtmlAgilityPack 1.11.x (HTML scraping), System.Xml.Linq (Torznab XML), Jellyfin.Controller 10.11.5 + Jellyfin.Model 10.11.5 (Plugin API)
**Storage**: Ephemeral torrent data (deleted when stream ends), Jellyfin config for persistent identity, in-memory peer table
**Testing**: xUnit, FluentAssertions, Moq (unit tests), Docker-based integration tests
**Target Platform**: Cross-platform (.NET 10.0 runtime), Jellyfin server environment
**Project Type**: Single project (Jellyfin plugin)
**Performance Goals**: Video playback starts within 30s, HTTP seeking <5s response, indexer search <10s, 10 concurrent streams without degradation
**Constraints**: Sequential piece downloading for streaming, 1 request/second per indexer rate limit, 100% IP anonymity when circuits available, ephemeral storage only
**Scale/Scope**: 20 unimplemented components, ~90 hours estimated implementation, supports multiple concurrent users

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verify alignment with TunnelFin Constitution (`.specify/memory/constitution.md`):

- [x] **Privacy-First**: Does this feature maintain anonymous-by-default behavior? Are there any privacy risks?
  - ✅ All BitTorrent peer connections route through Tribler circuits by default (SOCKS5 proxy pattern)
  - ✅ Onion routing with Curve25519 ECDH + AES-GCM-128 ensures IP anonymity
  - ✅ No external services or third-party tracking
  - ✅ Configurable circuit settings with clear privacy warnings when disabled

- [x] **Seamless Integration**: Does this integrate natively with Jellyfin's UX? Any external dependencies?
  - ✅ Native Jellyfin plugin using IChannel and ISearchProvider interfaces
  - ✅ Content appears in standard Jellyfin library UI
  - ✅ MediaSourceProvider delivers HTTP streams compatible with Jellyfin's player
  - ✅ No external deployment required, single plugin DLL

- [x] **Test-First Development**: Are tests defined before implementation? 80%+ coverage planned?
  - ✅ xUnit + FluentAssertions + Moq for unit tests
  - ✅ TDD workflow enforced: tests written before implementation
  - ✅ 80%+ coverage target with CI/CD gates
  - ✅ Docker-based integration tests for end-to-end validation

- [x] **Decentralized Architecture**: Does this avoid centralized services? Wire-compatible with existing protocols?
  - ✅ P2P BitTorrent via MonoTorrent (no central servers)
  - ✅ IPv8 wire-compatible circuit routing (Tribler network)
  - ✅ Configurable indexers (Torznab + HTML scrapers), no mandatory dependencies
  - ✅ Magnet links and DHT for decentralized peer discovery

- [x] **User Empowerment**: Are privacy/performance trade-offs transparent? User controls provided?
  - ✅ UI displays circuit status, latency overhead, throughput impact
  - ✅ Configurable anonymity settings (circuit hop count, direct connection fallback)
  - ✅ Indexer configuration (add/remove Torznab/HTML sources)
  - ✅ Stream quality selection (720p/1080p/4K) with bandwidth implications
  - ✅ Transparent error messages when circuits unavailable

**Violations Requiring Justification**: None

**Post-Research Validation**: All research findings (MonoTorrent streaming, Torznab protocol, HTML scraping, Jellyfin plugin patterns, circuit-routed connections) align with constitutional principles. See `research.md` for detailed constitutional compliance analysis.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/TunnelFin/
├── BitTorrent/
│   ├── TorrentEngine.cs           # MonoTorrent wrapper, StreamProvider integration
│   ├── TorrentMetadata.cs         # Torrent info, piece status, download progress
│   └── SequentialPiecePicker.cs   # Custom piece selection for streaming
├── Streaming/
│   ├── StreamManager.cs           # HTTP endpoint management, range request handling
│   ├── StreamSession.cs           # Active stream state, buffer status
│   └── AdaptivePrebuffer.cs       # Dynamic buffer sizing based on bitrate
├── Indexers/
│   ├── IndexerManager.cs          # Aggregates Torznab + HTML scrapers
│   ├── Torznab/
│   │   ├── TorznabClient.cs       # XML parsing, rate limiting
│   │   └── TorznabConfig.cs       # Indexer configuration
│   └── HtmlScrapers/
│       ├── Scraper1337x.cs        # 1337x table-based scraping
│       ├── ScraperNyaa.cs         # Nyaa panel-based scraping
│       ├── ScraperTorrentGalaxy.cs
│       └── ScraperEZTV.cs
├── Jellyfin/
│   ├── TunnelFinChannel.cs        # IChannel implementation
│   ├── TunnelFinSearchProvider.cs # ISearchProvider implementation
│   ├── TunnelFinMediaSource.cs    # MediaSourceProvider for streams
│   └── PluginConfiguration.cs     # Settings UI, indexer config
├── Networking/
│   ├── TunnelSocketConnector.cs   # ISocketConnector for circuit-routed peers
│   ├── CircuitHealthMonitor.cs    # PING/PONG health checks
│   └── CircuitConnectionPool.cs   # ObjectPool for circuit reuse
├── Configuration/
│   ├── IndexerConfig.cs           # Torznab/HTML indexer settings
│   ├── CircuitConfig.cs           # Hop count, timeout, pool size
│   └── StreamingConfig.cs         # Prebuffer size, max concurrent streams
└── Models/
    ├── TorrentResult.cs           # Search result from indexers
    ├── StreamSession.cs           # Active stream metadata
    └── CircuitMetadata.cs         # Circuit state, latency, health

tests/TunnelFin.Tests/
├── Unit/
│   ├── BitTorrent/
│   │   ├── TorrentEngineTests.cs
│   │   └── SequentialPiecePickerTests.cs
│   ├── Streaming/
│   │   ├── StreamManagerTests.cs
│   │   └── AdaptivePrebufferTests.cs
│   ├── Indexers/
│   │   ├── TorznabClientTests.cs
│   │   └── HtmlScrapers/
│   │       ├── Scraper1337xTests.cs
│   │       └── ScraperNyaaTests.cs
│   ├── Jellyfin/
│   │   ├── TunnelFinChannelTests.cs
│   │   └── TunnelFinSearchProviderTests.cs
│   └── Networking/
│       ├── TunnelSocketConnectorTests.cs
│       └── CircuitHealthMonitorTests.cs
└── Integration/
    ├── EndToEndStreamingTests.cs  # Full workflow: search → stream → playback
    ├── CircuitRoutingTests.cs     # Peer connections through circuits
    └── IndexerIntegrationTests.cs # Live indexer queries (Docker-based)
```

**Structure Decision**: Single project structure (Option 1) is selected because TunnelFin is a Jellyfin plugin with all components in a single assembly. The structure follows Jellyfin plugin conventions with feature-based organization (BitTorrent, Streaming, Indexers, Jellyfin, Networking). Tests mirror the source structure with Unit and Integration directories. This aligns with existing TunnelFin architecture established in features 001 and 002.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
