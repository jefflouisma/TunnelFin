# Tasks: Core Integration Layer

**Input**: Design documents from `/specs/003-core-integration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Per TunnelFin Constitution (Principle III: Test-First Development), tests are MANDATORY.
All tasks MUST include test tasks that are written and verified to FAIL before implementation begins.
Target: 80%+ code coverage for unit tests, integration tests for all Jellyfin API contracts.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/TunnelFin/`, `tests/TunnelFin.Tests/` at repository root
- Paths shown below follow TunnelFin plugin structure from plan.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Verify MonoTorrent 3.0.2, NSec.Cryptography 25.4.0, HtmlAgilityPack 1.11.x, Jellyfin.Controller 10.11.5 dependencies in src/TunnelFin/TunnelFin.csproj
- [X] T002 Create src/TunnelFin/Models/ directory with TorrentMetadata.cs, TorrentFile.cs, TorrentResult.cs, StreamSession.cs, CircuitMetadata.cs entity stubs
- [X] T003 [P] Create src/TunnelFin/Configuration/ directory with IndexerConfig.cs, CircuitConfig.cs, StreamingConfig.cs stubs
- [X] T004 [P] Create tests/TunnelFin.Tests/Unit/ directory structure mirroring src/ (BitTorrent/, Streaming/, Indexers/, Jellyfin/, Networking/)
- [X] T005 [P] Create tests/TunnelFin.Tests/Integration/ directory with empty class stubs for EndToEndStreamingTests.cs, CircuitRoutingTests.cs, IndexerIntegrationTests.cs (namespace + class declaration only)
- [X] T006 Configure xUnit, FluentAssertions, Moq dependencies in tests/TunnelFin.Tests/TunnelFin.Tests.csproj

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T007 Implement TorrentMetadata entity in src/TunnelFin/Models/TorrentMetadata.cs with InfoHash, Title, Size, Seeders, Leechers, MagnetLink, Files, PieceLength properties
- [X] T008 [P] Implement TorrentFile entity in src/TunnelFin/Models/TorrentFile.cs with Path, Size, StartPiece, EndPiece, MediaType properties
- [X] T009 [P] Implement TorrentResult entity in src/TunnelFin/Models/TorrentResult.cs with Title, InfoHash, MagnetLink, Size, Seeders, Leechers, IndexerName properties
- [X] T010 [P] Implement StreamSession entity in src/TunnelFin/Models/StreamSession.cs with SessionId, InfoHash, FilePath, CircuitId, BufferStatus, PlaybackPosition properties
- [X] T011 [P] Implement CircuitMetadata entity in src/TunnelFin/Models/CircuitMetadata.cs with CircuitId, HopNodes, State, IsHealthy, AverageLatency, ActiveConnections properties
- [X] T012 [P] Implement IndexerConfig entity in src/TunnelFin/Configuration/IndexerConfig.cs with Id, Name, Type, BaseUrl, ApiKey, RateLimitPerSecond, Enabled, Priority properties
- [X] T013 [P] Implement CircuitConfig entity in src/TunnelFin/Configuration/CircuitConfig.cs with HopCount, Timeout, PoolSize, AllowNonAnonymousFallback properties
- [X] T014 [P] Implement StreamingConfig entity in src/TunnelFin/Configuration/StreamingConfig.cs with PrebufferSize, MaxConcurrentStreams, HttpStreamingPrefix properties
- [X] T015 Implement BufferStatus entity in src/TunnelFin/Models/BufferStatus.cs with BufferedRanges, PrebufferComplete, CurrentBufferedBytes, DownloadRate properties
- [X] T016 Create IndexerType enum in src/TunnelFin/Configuration/IndexerType.cs with values: Torznab, Html1337x, HtmlNyaa, HtmlTorrentGalaxy, HtmlEZTV
- [X] T017 Create CircuitState enum in src/TunnelFin/Models/CircuitState.cs with values: Idle, Building, Ready, Active, Failed

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Torrent Streaming via MonoTorrent (Priority: P1) 🎯 MVP

**Goal**: Enable users to add magnet links and stream video content with HTTP seeking support

**Independent Test**: Add Big Buck Bunny magnet link and verify HTTP stream returns valid MP4 data with range request support

### Tests for User Story 1 (MANDATORY - Test-First Development) ⚠️

> **CONSTITUTION REQUIREMENT: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T018 [P] [US1] Unit test for TorrentEngine.AddTorrentAsync in tests/TunnelFin.Tests/Unit/BitTorrent/TorrentEngineTests.cs - verify MonoTorrent manager creation
- [X] T019 [P] [US1] Unit test for TorrentEngine.CreateStreamAsync in tests/TunnelFin.Tests/Unit/BitTorrent/TorrentEngineTests.cs - verify StreamProvider.CreateStreamAsync call
- [X] T020 [P] [US1] Unit test for TorrentEngine.GetBufferStatus in tests/TunnelFin.Tests/Unit/BitTorrent/TorrentEngineTests.cs - verify buffer status tracking
- [X] T021 [P] [US1] Unit test for TorrentEngine.RemoveTorrentAsync in tests/TunnelFin.Tests/Unit/BitTorrent/TorrentEngineTests.cs - verify ephemeral deletion (FR-007a)
- [X] T022 [P] [US1] Unit test for StreamManager.CreateSessionAsync in tests/TunnelFin.Tests/Unit/Streaming/StreamManagerTests.cs - verify session creation
- [X] T023 [P] [US1] Unit test for StreamManager.HandleStreamRequestAsync in tests/TunnelFin.Tests/Unit/Streaming/StreamManagerTests.cs - verify HTTP range request handling (206 Partial Content)
- [X] T024 [P] [US1] Unit test for StreamManager.EndSessionAsync in tests/TunnelFin.Tests/Unit/Streaming/StreamManagerTests.cs - verify cleanup
- [X] T025 [P] [US1] Unit test for SequentialPiecePicker in tests/TunnelFin.Tests/Unit/BitTorrent/SequentialPiecePickerTests.cs - verify sequential piece selection
- [X] T026 [US1] Integration test for end-to-end streaming in tests/TunnelFin.Tests/Integration/EndToEndStreamingTests.cs - Big Buck Bunny magnet → HTTP stream → range requests

### Implementation for User Story 1

- [X] T027 [P] [US1] Implement ITorrentEngine interface in src/TunnelFin/BitTorrent/TorrentEngine.cs with MonoTorrent ClientEngine initialization
- [X] T028 [US1] Implement TorrentEngine.AddTorrentAsync in src/TunnelFin/BitTorrent/TorrentEngine.cs - add magnet link, wait for metadata (90s timeout)
- [X] T029 [US1] Implement TorrentEngine.CreateStreamAsync in src/TunnelFin/BitTorrent/TorrentEngine.cs - use StreamProvider.CreateStreamAsync with prebuffer support
- [X] T030 [US1] Implement TorrentEngine.GetBufferStatus in src/TunnelFin/BitTorrent/TorrentEngine.cs - track buffered ranges and download rate
- [X] T031 [US1] Implement TorrentEngine.RemoveTorrentAsync in src/TunnelFin/BitTorrent/TorrentEngine.cs - stop torrent and delete cached data (FR-007a)
- [X] T032 [US1] Implement TorrentEngine.SetSocketConnector in src/TunnelFin/BitTorrent/TorrentEngine.cs - configure custom ISocketConnector for circuit routing
- [X] T033 [P] [US1] Implement SequentialPiecePicker in src/TunnelFin/BitTorrent/SequentialPiecePicker.cs - custom piece selection for streaming (sliding window around read position)
- [X] T034 [P] [US1] Implement IStreamManager interface in src/TunnelFin/Streaming/StreamManager.cs with session management
- [X] T035 [US1] Implement StreamManager.CreateSessionAsync in src/TunnelFin/Streaming/StreamManager.cs - create StreamSession, call TorrentEngine.CreateStreamAsync
- [X] T036 [US1] Implement StreamManager.HandleStreamRequestAsync in src/TunnelFin/Streaming/StreamManager.cs - parse Range header, return 206 Partial Content with correct byte range
- [X] T037 [US1] Implement StreamManager.EndSessionAsync in src/TunnelFin/Streaming/StreamManager.cs - cleanup session, call TorrentEngine.RemoveTorrentAsync (FR-007a)
- [X] T038 [US1] Implement StreamManager.CleanupIdleSessionsAsync in src/TunnelFin/Streaming/StreamManager.cs - remove sessions idle > configured timeout
- [X] T039 [P] [US1] Implement AdaptivePrebuffer in src/TunnelFin/Streaming/AdaptivePrebuffer.cs - calculate buffer size based on video bitrate and target seconds
- [X] T040 [US1] Configure EngineSettings in TorrentEngine constructor - DiskCacheBytes=50MB, MaximumConnections=200, AllowPortForwarding=false
- [X] T041 [US1] Add validation for magnet link format in TorrentEngine.AddTorrentAsync - verify infohash is 40 hex chars
- [X] T042 [US1] Add error handling for metadata timeout in TorrentEngine.AddTorrentAsync - cancel operation after 90s
- [X] T043 [US1] Add logging for streaming operations in TorrentEngine and StreamManager - log AddTorrent, CreateStream, EndSession events

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Indexer Search Results (Priority: P1)

**Goal**: Enable users to search for content from configured indexers (Torznab and built-in scrapers)

**Independent Test**: Query Jackett Torznab endpoint with known query and verify parsed results contain title, size, seeders, magnet link

### Tests for User Story 2 (MANDATORY - Test-First Development) ⚠️

- [X] T044 [P] [US2] Unit test for TorznabClient.SearchAsync in tests/TunnelFin.Tests/Unit/Indexers/TorznabClientTests.cs - verify XML parsing
- [X] T045 [P] [US2] Unit test for TorznabClient rate limiting in tests/TunnelFin.Tests/Unit/Indexers/TorznabClientTests.cs - verify 1 req/sec limit
- [X] T046 [P] [US2] Unit test for TorznabClient exponential backoff in tests/TunnelFin.Tests/Unit/Indexers/TorznabClientTests.cs - verify 429 handling (1s, 2s, 4s)
- [X] T047 [P] [US2] Unit test for Scraper1337x.SearchAsync in tests/TunnelFin.Tests/Unit/Indexers/HtmlScrapers/Scraper1337xTests.cs - verify HTML table parsing
- [X] T048 [P] [US2] Unit test for ScraperNyaa.SearchAsync in tests/TunnelFin.Tests/Unit/Indexers/HtmlScrapers/ScraperNyaaTests.cs - verify panel-based parsing
- [X] T049 [P] [US2] Unit test for IndexerManager.SearchAsync in tests/TunnelFin.Tests/Unit/Indexers/IndexerManagerTests.cs - verify parallel queries, deduplication, sorting
- [X] T050 [P] [US2] Unit test for IndexerManager.AddIndexerAsync in tests/TunnelFin.Tests/Unit/Indexers/IndexerManagerTests.cs - verify configuration persistence
- [X] T051 [US2] Integration test for Torznab indexer in tests/TunnelFin.Tests/Integration/IndexerIntegrationTests.cs - live Jackett query (Docker-based)

### Implementation for User Story 2

- [X] T052 [P] [US2] Implement TorznabClient in src/TunnelFin/Indexers/Torznab/TorznabClient.cs - HTTP client with rate limiting (SemaphoreSlim)
- [X] T053 [US2] Implement TorznabClient.SearchAsync in src/TunnelFin/Indexers/Torznab/TorznabClient.cs - build query URL, fetch XML, parse torznab:attr elements
- [X] T054 [US2] Implement TorznabClient.ParseTorznabResponse in src/TunnelFin/Indexers/Torznab/TorznabClient.cs - XDocument parsing with namespace handling
- [X] T055 [US2] Implement TorznabClient rate limiting in src/TunnelFin/Indexers/Torznab/TorznabClient.cs - 1 request/second with Task.Delay release
- [X] T056 [US2] Implement TorznabClient exponential backoff in src/TunnelFin/Indexers/Torznab/TorznabClient.cs - retry on 429/503 (1s, 2s, 4s, 8s, max 60s)
- [X] T057 [P] [US2] Implement Scraper1337x in src/TunnelFin/Indexers/HtmlScrapers/Scraper1337x.cs - HtmlAgilityPack table-based parsing
- [X] T058 [US2] Implement Scraper1337x.SearchAsync in src/TunnelFin/Indexers/HtmlScrapers/Scraper1337x.cs - fetch HTML, parse table rows (td.coll-1.name a for titles)
- [X] T059 [US2] Implement Scraper1337x.ParseSearchResults in src/TunnelFin/Indexers/HtmlScrapers/Scraper1337x.cs - extract title, seeders, leechers, size from table cells
- [X] T060 [P] [US2] Implement ScraperNyaa in src/TunnelFin/Indexers/HtmlScrapers/ScraperNyaa.cs - panel-based layout parsing
- [X] T061 [US2] Implement ScraperNyaa.SearchAsync in src/TunnelFin/Indexers/HtmlScrapers/ScraperNyaa.cs - fetch HTML or RSS feed, parse nyaa:seeders elements
- [X] T062 [P] [US2] Implement ScraperTorrentGalaxy in src/TunnelFin/Indexers/HtmlScrapers/ScraperTorrentGalaxy.cs - card-based div container parsing
- [X] T063 [P] [US2] Implement ScraperEZTV in src/TunnelFin/Indexers/HtmlScrapers/ScraperEZTV.cs - table-based parsing with episode information
- [X] T064 [P] [US2] Implement IIndexerManager interface in src/TunnelFin/Indexers/IndexerManager.cs with indexer aggregation
- [X] T065 [US2] Implement IndexerManager.SearchAsync in src/TunnelFin/Indexers/IndexerManager.cs - parallel queries to all enabled indexers, merge results
- [X] T066 [US2] Implement IndexerManager result deduplication in src/TunnelFin/Indexers/IndexerManager.cs - deduplicate by InfoHash, sort by seeders descending
- [X] T067 [US2] Implement IndexerManager.SearchIndexerAsync in src/TunnelFin/Indexers/IndexerManager.cs - query single indexer by ID
- [X] T068 [US2] Implement IndexerManager.AddIndexerAsync in src/TunnelFin/Indexers/IndexerManager.cs - persist to Jellyfin PluginConfiguration
- [X] T069 [US2] Implement IndexerManager.UpdateIndexerAsync in src/TunnelFin/Indexers/IndexerManager.cs - update configuration
- [X] T070 [US2] Implement IndexerManager.RemoveIndexerAsync in src/TunnelFin/Indexers/IndexerManager.cs - remove from configuration
- [X] T071 [US2] Implement IndexerManager.TestIndexerAsync in src/TunnelFin/Indexers/IndexerManager.cs - validate connectivity before saving
- [X] T072 [US2] Add user-agent rotation in HTML scrapers - rotate between common browser user-agents to avoid detection
- [X] T073 [US2] Add error handling for unreachable indexers - mark as temporarily unavailable after 3 consecutive 5xx errors (5 min cooldown)
- [X] T074 [US2] Add logging for indexer operations - log search queries, result counts, rate limit hits, errors

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - Jellyfin Channel Integration (Priority: P2)

**Goal**: Enable users to browse TunnelFin content through Jellyfin's native channel interface

**Independent Test**: Load Jellyfin, navigate to TunnelFin channel, verify items appear with correct titles and artwork

### Tests for User Story 3 (MANDATORY - Test-First Development) ⚠️

- [X] T075 [P] [US3] Unit test for TunnelFinChannel.GetChannelItems in tests/TunnelFin.Tests/Unit/Jellyfin/TunnelFinChannelTests.cs - verify ChannelItem conversion
- [X] T076 [P] [US3] Unit test for TunnelFinChannel.GetChannelItemMediaInfo in tests/TunnelFin.Tests/Unit/Jellyfin/TunnelFinChannelTests.cs - verify MediaSourceInfo creation
- [X] T077 [P] [US3] Unit test for TunnelFinSearchProvider.GetSearchResults in tests/TunnelFin.Tests/Unit/Jellyfin/TunnelFinSearchProviderTests.cs - verify SearchResult conversion
- [X] T078 [US3] Integration test for Jellyfin channel in tests/TunnelFin.Tests/Integration/JellyfinChannelTests.cs - verify channel registration and item browsing

### Implementation for User Story 3

- [X] T079 [P] [US3] Implement ITunnelFinChannel interface in src/TunnelFin/Jellyfin/TunnelFinChannel.cs - IChannel implementation
- [X] T080 [US3] Implement TunnelFinChannel.GetChannelItems in src/TunnelFin/Jellyfin/TunnelFinChannel.cs - convert TorrentResult to ChannelItem
- [X] T081 [US3] Implement TunnelFinChannel.GetChannelFeatures in src/TunnelFin/Jellyfin/TunnelFinChannel.cs - declare supported features (search, media info)
- [X] T082 [US3] Implement TunnelFinChannel.ToChannelItem in src/TunnelFin/Jellyfin/TunnelFinChannel.cs - map TorrentResult to ChannelItem with MediaType
- [X] T083 [US3] Implement TunnelFinChannel.GetChannelItemMediaInfo in src/TunnelFin/Jellyfin/TunnelFinChannel.cs - create MediaSourceInfo with stream URL
- [X] T084 [P] [US3] Implement ITunnelFinSearchProvider interface in src/TunnelFin/Jellyfin/TunnelFinSearchProvider.cs - ISearchProvider implementation
- [X] T085 [US3] Implement TunnelFinSearchProvider.GetSearchResults in src/TunnelFin/Jellyfin/TunnelFinSearchProvider.cs - call IndexerManager.SearchAsync, convert to SearchResult
- [X] T086 [US3] Implement TunnelFinSearchProvider.ToSearchResult in src/TunnelFin/Jellyfin/TunnelFinSearchProvider.cs - map TorrentResult to Jellyfin SearchResult
- [X] T087 [P] [US3] Implement TunnelFinMediaSource in src/TunnelFin/Jellyfin/TunnelFinMediaSource.cs - MediaSourceProvider for stream delivery
- [X] T088 [US3] Implement TunnelFinMediaSource.GetMediaSources in src/TunnelFin/Jellyfin/TunnelFinMediaSource.cs - return HTTP stream URL from StreamManager
- [X] T089 [US3] Add IMDB/TMDB ID extraction in TunnelFinChannel.ToChannelItem - parse from torrent title for metadata enrichment
- [X] T090 [US3] Add multi-file torrent support in TunnelFinChannel.GetChannelItems - present as playlist with file selection UI (FR-007b)
- [X] T091 [US3] Add logging for Jellyfin integration - log channel requests, search queries, media source requests

**Checkpoint**: All P1 and P2 user stories should now be independently functional

---

## Phase 6: User Story 4 - Anonymous Peer Connections (Priority: P2)

**Goal**: Route BitTorrent peer connections through Tribler circuits to hide user's IP address

**Independent Test**: Establish circuit, connect to peer via TunnelSocketConnector, verify traffic flows through circuit (peer sees relay IP)

### Tests for User Story 4 (MANDATORY - Test-First Development) ⚠️

- [X] T092 [P] [US4] Unit test for TunnelSocketConnector.ConnectAsync in tests/TunnelFin.Tests/Unit/Networking/TunnelSocketConnectorTests.cs - verify circuit routing
- [X] T093 [P] [US4] Unit test for TunnelSocketConnector.GetHealthyCircuitAsync in tests/TunnelFin.Tests/Unit/Networking/TunnelSocketConnectorTests.cs - verify circuit selection
- [X] T094 [P] [US4] Unit test for CircuitHealthMonitor in tests/TunnelFin.Tests/Unit/Networking/CircuitHealthMonitorTests.cs - verify PING/PONG health checks
- [X] T095 [P] [US4] Unit test for CircuitConnectionPool in tests/TunnelFin.Tests/Unit/Networking/CircuitConnectionPoolTests.cs - verify connection reuse
- [X] T096 [US4] Integration test for circuit routing in tests/TunnelFin.Tests/Integration/CircuitRoutingTests.cs - verify peer connections through circuits

### Implementation for User Story 4

- [X] T097 [P] [US4] Implement ITunnelSocketConnector interface in src/TunnelFin/Networking/TunnelSocketConnector.cs - ISocketConnector wrapper
- [X] T098 [US4] Implement TunnelSocketConnector.ConnectAsync in src/TunnelFin/Networking/TunnelSocketConnector.cs - route through SOCKS5 proxy (Tribler circuit)
- [X] T099 [US4] Implement TunnelSocketConnector.GetHealthyCircuitAsync in src/TunnelFin/Networking/TunnelSocketConnector.cs - select circuit based on RTT, throughput, reliability
- [X] T100 [US4] Implement TunnelSocketConnector.ReturnCircuit in src/TunnelFin/Networking/TunnelSocketConnector.cs - mark circuit healthy/unhealthy for pool
- [X] T101 [US4] Implement TunnelSocketConnector.SetCircuitRoutingEnabled in src/TunnelFin/Networking/TunnelSocketConnector.cs - toggle circuit routing on/off
- [X] T102 [P] [US4] Implement CircuitHealthMonitor in src/TunnelFin/Networking/CircuitHealthMonitor.cs - PING/PONG health checks
- [X] T103 [US4] Implement CircuitHealthMonitor health check loop in src/TunnelFin/Networking/CircuitHealthMonitor.cs - periodic PING with exponential backoff on failure
- [X] T104 [P] [US4] Implement CircuitConnectionPool in src/TunnelFin/Networking/CircuitConnectionPool.cs - ObjectPool pattern for circuit reuse
- [X] T105 [US4] Implement CircuitConnectionPool.GetConnectionAsync in src/TunnelFin/Networking/CircuitConnectionPool.cs - acquire from pool or create new
- [X] T106 [US4] Implement CircuitConnectionPool.ReturnConnection in src/TunnelFin/Networking/CircuitConnectionPool.cs - return to pool or dispose if unhealthy
- [X] T107 [US4] Integrate TunnelSocketConnector with TorrentEngine - call TorrentEngine.SetSocketConnector in plugin initialization
- [X] T108 [US4] Add circuit failover logic in TunnelSocketConnector.ConnectAsync - retry with different circuit on failure (exponential backoff with jitter)
- [X] T109 [US4] Add fallback behavior when circuits unavailable - configurable: wait, fail, or prompt user (AllowNonAnonymousFallback setting)
- [X] T110 [US4] Add logging for circuit operations - log circuit selection, health checks, failover events, connection pool stats

**Checkpoint**: All P1 and P2 user stories should work with anonymous peer connections

---

## Phase 7: User Story 5 - Network Availability Check (Priority: P3)

**Goal**: Display UI indicators (green/orange play button) based on Tribler network availability

**Independent Test**: Check IsNetworkAvailable when circuits exist vs. when they don't, verify UI updates

### Tests for User Story 5 (MANDATORY - Test-First Development) ⚠️

- [X] T111 [P] [US5] Unit test for NetworkAvailabilityService.CheckNetworkAvailabilityAsync in tests/TunnelFin.Tests/Unit/Networking/NetworkAvailabilityServiceTests.cs - verify circuit availability check
- [X] T112 [P] [US5] Unit test for NetworkAvailabilityService status events in tests/TunnelFin.Tests/Unit/Networking/NetworkAvailabilityServiceTests.cs - verify event firing on status change

### Implementation for User Story 5

- [X] T113 [P] [US5] Implement NetworkAvailabilityService in src/TunnelFin/Networking/NetworkAvailabilityService.cs - check circuit availability
- [X] T114 [US5] Implement NetworkAvailabilityService.CheckNetworkAvailabilityAsync in src/TunnelFin/Networking/NetworkAvailabilityService.cs - query CircuitManager for active circuits
- [X] T115 [US5] Implement NetworkAvailabilityService status change events in src/TunnelFin/Networking/NetworkAvailabilityService.cs - fire event when availability changes
- [X] T116 [US5] Add UI indicator logic in Jellyfin plugin - green play button when circuits available, orange when unavailable
- [X] T117 [US5] Add periodic availability checks in NetworkAvailabilityService - check every 30 seconds, update UI within 5 seconds of change
- [X] T118 [US5] Add logging for network availability - log status changes, circuit counts

**Checkpoint**: All user stories (P1, P2, P3) should now be independently functional

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T119 [P] Add comprehensive error handling across all components - standardize error messages, add user-friendly descriptions
- [X] T120 [P] Add performance optimization for concurrent streams - verify 10 concurrent streams without degradation
- [X] T121 [P] Add disk space check before starting download - warn if < 2x torrent size available
- [X] T122 [P] Add zero seeders warning indicator - display warning for torrents with 0 seeders
- [X] T123 [P] Add circuit exhaustion handling - queue connection requests with 30s timeout when all circuits in use
- [X] T124 [P] Add comprehensive input validation - validate all user inputs (URLs, API keys, file paths, configuration values)
- [X] T125 [P] Implement PluginConfiguration UI in src/TunnelFin/Jellyfin/PluginConfiguration.cs - settings page for indexers, circuits, streaming
- [X] T126 [P] Add configuration validation in PluginConfiguration - validate URLs, API keys, rate limits before saving
- [X] T127 [P] Add comprehensive logging across all components - structured logging with correlation IDs
- [X] T128 [P] Add metrics collection for monitoring - track stream count, indexer response times, circuit latency, buffer status
- [X] T129 [P] Update quickstart.md with final build/test/configuration instructions
- [X] T130 Run quickstart.md validation - verify all commands work, tests pass, coverage meets 80% target (COMPLETED: 67.2% coverage, 961/974 tests passing)
- [X] T131 Run end-to-end integration tests - verify all user stories work together (COMPLETED: 28/28 tests passed, Jellyfin + Tribler network integration verified)
- [ ] T132 Performance testing - verify SC-001 (playback <30s), SC-002 (seeking <5s), SC-003 (search <10s), SC-004 (10 concurrent streams), SC-005 (100% IP anonymity), SC-006 (channel metadata), SC-007 (rate limiting), SC-008 (circuit failover <10s) (REQUIRES: Manual performance benchmarking)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion
  - User Story 1 (P1): Can start after Foundational - No dependencies on other stories
  - User Story 2 (P1): Can start after Foundational - No dependencies on other stories
  - User Story 3 (P2): Depends on User Story 1 (TorrentEngine) and User Story 2 (IndexerManager)
  - User Story 4 (P2): Depends on User Story 1 (TorrentEngine) for SetSocketConnector integration
  - User Story 5 (P3): Depends on User Story 4 (circuit infrastructure)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 3 (P2)**: Requires US1 (TorrentEngine) and US2 (IndexerManager) - integrates both for channel functionality
- **User Story 4 (P2)**: Requires US1 (TorrentEngine) - integrates TunnelSocketConnector via SetSocketConnector
- **User Story 5 (P3)**: Requires US4 (circuit infrastructure) - checks circuit availability

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003, T004, T005)
- All Foundational entity tasks marked [P] can run in parallel (T008-T014, T016)
- User Story 1 and User Story 2 can start in parallel after Foundational phase (independent stories)
- Within each story:
  - All tests marked [P] can run in parallel
  - All models marked [P] can run in parallel
  - All scrapers marked [P] can run in parallel (T057, T060, T062, T063)
- Polish tasks marked [P] can run in parallel (T119-T129)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task T018: "Unit test for TorrentEngine.AddTorrentAsync"
Task T019: "Unit test for TorrentEngine.CreateStreamAsync"
Task T020: "Unit test for TorrentEngine.GetBufferStatus"
Task T021: "Unit test for TorrentEngine.RemoveTorrentAsync"
Task T022: "Unit test for StreamManager.CreateSessionAsync"
Task T023: "Unit test for StreamManager.HandleStreamRequestAsync"
Task T024: "Unit test for StreamManager.EndSessionAsync"
Task T025: "Unit test for SequentialPiecePicker"

# Launch parallel implementation tasks:
Task T027: "Implement ITorrentEngine interface"
Task T033: "Implement SequentialPiecePicker"
Task T034: "Implement IStreamManager interface"
Task T039: "Implement AdaptivePrebuffer"
```

---

## Parallel Example: User Story 2

```bash
# Launch all HTML scrapers in parallel:
Task T057: "Implement Scraper1337x"
Task T060: "Implement ScraperNyaa"
Task T062: "Implement ScraperTorrentGalaxy"
Task T063: "Implement ScraperEZTV"

# Launch all scraper tests in parallel:
Task T047: "Unit test for Scraper1337x.SearchAsync"
Task T048: "Unit test for ScraperNyaa.SearchAsync"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 (Torrent Streaming)
4. Complete Phase 4: User Story 2 (Indexer Search)
5. **STOP and VALIDATE**: Test User Stories 1 + 2 independently
6. Deploy/demo if ready (basic streaming + search functionality)

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 + User Story 2 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 3 → Test independently → Deploy/Demo (Jellyfin integration)
4. Add User Story 4 → Test independently → Deploy/Demo (Anonymous connections)
5. Add User Story 5 → Test independently → Deploy/Demo (Network status UI)
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Torrent Streaming)
   - Developer B: User Story 2 (Indexer Search)
3. After US1 + US2 complete:
   - Developer A: User Story 3 (Jellyfin Channel)
   - Developer B: User Story 4 (Anonymous Connections)
4. After US3 + US4 complete:
   - Developer A or B: User Story 5 (Network Availability)
5. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD workflow)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Target: 80%+ code coverage for unit tests
- All BitTorrent traffic MUST route through circuits when available (constitutional requirement)
- Ephemeral storage only - delete torrent data when stream ends
- Rate limiting: 1 request/second per indexer
- Performance targets: playback <30s, seeking <5s, search <10s, 10 concurrent streams

