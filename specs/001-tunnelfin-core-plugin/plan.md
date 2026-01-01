# Implementation Plan: TunnelFin Core Plugin

**Branch**: `001-tunnelfin-core-plugin` | **Date**: January 1, 2026 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-tunnelfin-core-plugin/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

TunnelFin is a native Jellyfin plugin that enables privacy-first torrent streaming by integrating anonymous onion routing (via Tribler network) with seamless content discovery and playback. The plugin reimplements IPv8 protocol in C# to participate in the existing Tribler anonymity network, uses MonoTorrent for BitTorrent streaming, and provides advanced content filtering/aggregation capabilities directly within Jellyfin's native UI. All operations default to anonymous routing with explicit user consent required for non-anonymous fallback.

## Technical Context

**Language/Version**: C# / .NET 10.0
**Primary Dependencies**: MonoTorrent 3.0.2 (BitTorrent), NSec.Cryptography 25.4.0 (Ed25519), Jellyfin.Controller 10.11.5 (Plugin API), Microsoft.Extensions.Http 10.0.1 (HTTP client)
**Storage**: Jellyfin's encrypted configuration storage (plugin settings, cryptographic identity), in-memory caching (search results 5-15 min, metadata failures 5 min), temporary disk cache (torrent data, max 10GB default)
**Testing**: xUnit 2.9.3, FluentAssertions 8.8.0, Moq 4.20.72, Coverlet 6.0.4 (80%+ coverage requirement)
**Target Platform**: Self-hosted Jellyfin servers (Linux/Windows/macOS), single-user instances
**Project Type**: Single plugin project with modular architecture (Core, Networking, BitTorrent, Indexers, Streaming)
**Performance Goals**: Stream initialization <30s (success) or <60s (timeout), search results <5s (5 indexers), filter/sort <1s (100+ results), buffer >10s during playback, real-time metrics <1s latency
**Constraints**: Self-contained (no external services/Docker), 95% anonymous routing success rate, privacy-first defaults (no PII in logs), wire-compatible IPv8 protocol, proportional bandwidth contribution
**Scale/Scope**: Single-user optimization, 3 concurrent streams (default), 10GB cache (default), 5 concurrent indexer searches (default), 47 functional requirements, 4 user stories

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verify alignment with TunnelFin Constitution (`.specify/memory/constitution.md`):

- [x] **Privacy-First**: ✅ PASS
  - All BitTorrent traffic routes through Tribler anonymity network by default (FR-002, FR-035)
  - Non-anonymous streaming requires explicit per-session consent with clear warnings (FR-036, FR-040)
  - Color-coded UI (green=anonymous, orange=non-anonymous) provides immediate visual feedback (FR-034)
  - Minimal logging with no PII or content titles by default (FR-037, FR-041)
  - Cryptographic identity stored in Jellyfin's encrypted configuration (FR-038)
  - Default to maximum privacy settings: 3-hop routing, anonymous-only mode (FR-006)

- [x] **Seamless Integration**: ✅ PASS
  - Registers as native Jellyfin search provider (FR-027)
  - Presents torrents as native library items with rich metadata (FR-028)
  - Exposes streams via standard HTTP endpoints for Jellyfin player (FR-009)
  - Self-contained C# plugin, no external services/Docker required (SC-012)
  - Follows Jellyfin plugin API patterns (Jellyfin.Controller 10.9.0 dependency)

- [x] **Test-First Development**: ✅ PASS
  - xUnit + FluentAssertions + Moq testing stack specified
  - 80%+ coverage requirement documented in Technical Context
  - Critical areas identified for testing: filtering engine, sorting, deduplication, IPv8 crypto, privacy fallback
  - Unit tests (90%) and integration tests (10%) structure defined in PRD
  - Test-first workflow mandated by constitution

- [x] **Decentralized Architecture**: ✅ PASS
  - Wire-compatible IPv8 protocol implementation to join existing Tribler network (FR-001)
  - Proportional bandwidth contribution for network fairness (FR-005, SC-010)
  - Multiple decentralized indexers: built-in (1337x, Nyaa.si, RARBG) + custom Torznab (FR-016, FR-017)
  - No debrid services or centralized caching (PRD non-goal)
  - Runs entirely within Jellyfin process, no external daemons (Technical Context)

- [x] **User Empowerment**: ✅ PASS
  - Configurable hop count (1-3 hops) for anonymity/performance balance (FR-003)
  - Real-time stream health metrics (peer count, download speed, buffer status) (FR-011)
  - Explicit warnings before non-anonymous operations (FR-036, FR-040)
  - Advanced filtering/sorting controls with content-type profiles (FR-019-FR-024)
  - User-configurable resource limits (streams, cache, searches) (FR-013, FR-014, FR-015, FR-018)
  - Comprehensive observability metrics without exposing PII (FR-041-FR-047, SC-013)

**Violations Requiring Justification**: None

## Project Structure

### Documentation (this feature)

```text
specs/001-tunnelfin-core-plugin/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── streaming-api.yaml       # HTTP streaming endpoints (OpenAPI)
│   ├── search-provider.yaml     # Jellyfin search provider contract
│   └── plugin-config.yaml       # Configuration schema
├── checklists/
│   └── requirements.md  # Specification quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
└── TunnelFin/                    # Main plugin source code
    ├── Core/                     # Plugin core, Jellyfin integration
    │   ├── Plugin.cs             # Main plugin entry point
    │   ├── PluginConfiguration.cs
    │   └── ServiceRegistration.cs
    ├── Networking/               # IPv8 protocol & Tribler network layer
    │   ├── IPv8/                 # IPv8 protocol implementation
    │   │   ├── Protocol.cs
    │   │   ├── Peer.cs
    │   │   └── Handshake.cs
    │   ├── Circuits/             # Onion routing circuits
    │   │   ├── CircuitManager.cs
    │   │   ├── Circuit.cs
    │   │   └── HopNode.cs
    │   └── Identity/             # Cryptographic identity
    │       ├── NetworkIdentity.cs
    │       └── Ed25519KeyPair.cs
    ├── BitTorrent/               # MonoTorrent integration & torrent management
    │   ├── TorrentEngine.cs
    │   ├── StreamingTorrent.cs
    │   ├── PiecePrioritizer.cs   # Sequential piece selection
    │   └── PeerManager.cs
    ├── Streaming/                # HTTP stream exposure & playback
    │   ├── StreamManager.cs
    │   ├── HttpStreamEndpoint.cs
    │   └── BufferManager.cs
    ├── Indexers/                 # Torrent indexer implementations
    │   ├── IIndexer.cs
    │   ├── BuiltIn/              # Built-in indexers
    │   │   ├── Indexer1337x.cs
    │   │   ├── IndexerNyaa.cs
    │   │   └── IndexerRARBG.cs
    │   ├── Torznab/              # Custom Torznab support
    │   │   └── TorznabIndexer.cs
    │   └── IndexerManager.cs
    ├── Discovery/                # Content discovery & aggregation
    │   ├── SearchEngine.cs
    │   ├── FilterEngine.cs       # Advanced filtering logic
    │   ├── SortEngine.cs         # Multi-criteria sorting
    │   ├── Deduplicator.cs       # Torrent deduplication
    │   └── MetadataFetcher.cs    # TMDB/AniList integration
    ├── Models/                   # Data models & DTOs
    │   ├── TorrentStream.cs
    │   ├── AnonymityCircuit.cs
    │   ├── SearchResult.cs
    │   ├── FilterProfile.cs
    │   ├── IndexerConfiguration.cs
    │   └── NetworkIdentity.cs
    ├── Configuration/            # Plugin settings & user preferences
    │   ├── PluginSettings.cs
    │   ├── AnonymitySettings.cs
    │   ├── FilterSettings.cs
    │   └── ResourceLimits.cs
    └── Jellyfin/                 # Jellyfin-specific integration
        ├── SearchProvider.cs     # ISearchProvider implementation
        ├── ChannelProvider.cs    # IChannel implementation
        └── MetadataProvider.cs   # Metadata integration

tests/
├── TunnelFin.Tests/              # Unit tests (90% of tests)
│   ├── BitTorrent/               # BitTorrent engine tests
│   │   ├── TorrentEngineTests.cs
│   │   └── PiecePrioritizerTests.cs
│   ├── Core/                     # Core functionality tests
│   │   └── PluginTests.cs
│   ├── Discovery/                # Discovery & filtering tests
│   │   ├── FilterEngineTests.cs
│   │   ├── SortEngineTests.cs
│   │   └── DeduplicatorTests.cs
│   ├── Fixtures/                 # Test fixtures & sample data
│   │   ├── SampleTorrents.cs
│   │   └── MockIndexerData.cs
│   ├── Indexers/                 # Indexer tests
│   │   └── IndexerManagerTests.cs
│   ├── Networking/               # IPv8 & network layer tests
│   │   ├── IPv8ProtocolTests.cs
│   │   ├── CircuitManagerTests.cs
│   │   └── Ed25519KeyPairTests.cs
│   └── Streaming/                # Streaming engine tests
│       ├── StreamManagerTests.cs
│       └── BufferManagerTests.cs
└── TunnelFin.Integration/        # Integration tests (10% of tests)
    ├── Jellyfin/                 # Jellyfin integration test utilities
    │   ├── PluginLoadingTests.cs
    │   └── SearchProviderTests.cs
    └── Helpers/                  # Integration test helpers
        └── JellyfinTestServer.cs

reference_repos/                  # Cloned reference implementations
├── AIOStreams/                   # Content aggregation reference (TypeScript)
├── Gelato/                       # Jellyfin plugin integration reference (C#)
├── monotorrent/                  # C# BitTorrent library source
├── TorrServer/                   # Torrent streaming reference (Go)
└── tribler/                      # Tribler anonymity network reference (Python)
```

**Structure Decision**: Single plugin project structure (Option 1) with modular organization by functional domain. The plugin is self-contained within `src/TunnelFin/` with clear separation of concerns: Core (Jellyfin integration), Networking (IPv8/Tribler), BitTorrent (MonoTorrent), Streaming (HTTP endpoints), Indexers (search sources), Discovery (filtering/aggregation), Models (data structures), Configuration (settings), and Jellyfin (provider implementations). Tests mirror the source structure with 90% unit tests and 10% integration tests. Reference repositories provide implementation patterns for complex protocols and integrations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations detected. All constitutional principles are satisfied.

---

## Phase 0: Research Complete ✅

**Output**: `specs/001-tunnelfin-core-plugin/research.md`

**Key Technical Decisions**:

1. **IPv8 Protocol Implementation**: Custom C# implementation using NSec.Cryptography for wire compatibility with Tribler network
2. **BitTorrent Streaming**: MonoTorrent.Streaming with custom sequential piece prioritizer
3. **Filtering/Sorting Engine**: Expression-based engine inspired by AIOStreams for power user requirements
4. **Jellyfin Integration**: Gelato patterns with dependency injection and provider interfaces
5. **Metadata Fetching**: Exponential backoff retry (1s, 2s, 4s) with 5-minute failure caching

**Reference Repositories Identified**:
- `tribler/` - IPv8 protocol and circuit management patterns (Python)
- `monotorrent/` - BitTorrent streaming implementation (C#)
- `TorrServer/` - Sequential piece prioritization patterns (Go)
- `AIOStreams/` - Advanced filtering and aggregation (TypeScript)
- `Gelato/` - Jellyfin plugin architecture (C#)

**No Unknowns Remaining**: All technical stack details were concrete from PRD and spec.

---

## Phase 1: Design & Contracts Complete ✅

**Outputs**:

1. **Data Model**: `specs/001-tunnelfin-core-plugin/data-model.md`
   - 6 core entities: TorrentStream, AnonymityCircuit, SearchResult, FilterProfile, IndexerConfiguration, NetworkIdentity
   - Complete field definitions with validation rules from functional requirements
   - State transition diagrams for TorrentStream and AnonymityCircuit
   - Entity relationship diagram

2. **API Contracts**: `specs/001-tunnelfin-core-plugin/contracts/`
   - `streaming-api.yaml` - HTTP streaming endpoints (OpenAPI 3.0.3)
     - GET /stream/{streamId} - Stream torrent content with range support
     - GET /stream/{streamId}/health - Real-time stream health metrics
     - POST /stream/{streamId}/stop - Stop active stream
   - `search-provider.yaml` - Jellyfin search provider contract (OpenAPI 3.0.3)
     - GET /search - Search across indexers with filtering/sorting
     - POST /stream/initialize - Initialize torrent stream with circuit establishment
   - `plugin-config.yaml` - Configuration schema (OpenAPI 3.0.3)
     - AnonymitySettings, ResourceLimits, IndexerConfiguration, FilterProfile, NetworkIdentity

3. **Developer Quickstart**: `specs/001-tunnelfin-core-plugin/quickstart.md`
   - Prerequisites and initial setup
   - Test-driven development workflow
   - Running Jellyfin locally in Kubernetes
   - Plugin deployment and debugging
   - Constitution compliance checklist

4. **Agent Context**: `.augment/rules/specify-rules.md`
   - Updated with C# / .NET 10.0 technology stack
   - MonoTorrent, NSec.Cryptography, Jellyfin.Controller dependencies
   - Storage patterns (encrypted config, in-memory cache, disk cache)

---

## Planning Summary

### Artifacts Generated

| Artifact | Path | Purpose |
|----------|------|---------|
| Implementation Plan | `specs/001-tunnelfin-core-plugin/plan.md` | This file - overall planning document |
| Research | `specs/001-tunnelfin-core-plugin/research.md` | Technical decisions and reference patterns |
| Data Model | `specs/001-tunnelfin-core-plugin/data-model.md` | Entity definitions and relationships |
| Streaming API | `specs/001-tunnelfin-core-plugin/contracts/streaming-api.yaml` | HTTP streaming endpoints (OpenAPI) |
| Search Provider | `specs/001-tunnelfin-core-plugin/contracts/search-provider.yaml` | Jellyfin search integration (OpenAPI) |
| Configuration | `specs/001-tunnelfin-core-plugin/contracts/plugin-config.yaml` | Plugin settings schema (OpenAPI) |
| Quickstart Guide | `specs/001-tunnelfin-core-plugin/quickstart.md` | Developer onboarding |
| Agent Context | `.augment/rules/specify-rules.md` | AI agent technology awareness |

### Constitution Compliance

✅ **Privacy-First**: Default anonymous routing (95% target), explicit consent for non-anonymous
✅ **Seamless Integration**: Native Jellyfin plugin, no external dependencies
✅ **Test-First Development**: xUnit + FluentAssertions + Moq, 80%+ coverage requirement
✅ **Decentralized Architecture**: Wire-compatible IPv8, proportional bandwidth contribution
✅ **User Empowerment**: Configurable settings, transparent controls, filter profiles

**Violations**: None

### Technical Stack Summary

- **Language**: C# / .NET 10.0
- **BitTorrent**: MonoTorrent 3.0.2 (streaming support)
- **Cryptography**: NSec.Cryptography 25.4.0 (Ed25519, X25519)
- **Jellyfin**: Jellyfin.Controller 10.9.0 (plugin API)
- **HTTP**: Microsoft.Extensions.Http 10.0.1 (resilient HTTP)
- **Testing**: xUnit 2.9.3, FluentAssertions 8.8.0, Moq 4.20.72, Coverlet 6.0.4
- **Storage**: Jellyfin encrypted config, in-memory cache, temporary disk cache

### Success Criteria Mapped

| Criterion | Target | Requirement |
|-----------|--------|-------------|
| SC-001 | <30s stream init | FR-007, FR-008, FR-009 |
| SC-002 | 95% anonymous routing | FR-001, FR-002, FR-003 |
| SC-003 | >10s buffer | FR-010, FR-011 |
| SC-004 | <5s search | FR-016, FR-017, FR-018 |
| SC-005 | <1s filter/sort | FR-019-FR-024 |
| SC-006 | <2min profile config | FR-024 |
| SC-007 | 90% deduplication | FR-025 |
| SC-008 | 95% metadata fetch | FR-026, FR-030-FR-032 |
| SC-009 | <1s metrics latency | FR-041-FR-047 |
| SC-010 | 5% contribution accuracy | FR-005, FR-047 |

### Next Steps

**Phase 2: Task Breakdown** (NOT part of `/speckit.plan` - use `/speckit.tasks` command)

The planning phase is complete. To proceed with implementation:

```bash
/speckit.tasks
```

This will generate `specs/001-tunnelfin-core-plugin/tasks.md` with:
- Granular development tasks broken down from functional requirements
- Task dependencies and sequencing
- Effort estimates
- Test coverage requirements per task

---

**Planning Status**: ✅ COMPLETE
**Branch**: `001-tunnelfin-core-plugin`
**Ready for**: Task breakdown and implementation
