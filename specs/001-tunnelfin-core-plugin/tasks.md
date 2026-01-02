# Tasks: TunnelFin Core Plugin

**Input**: Design documents from `/specs/001-tunnelfin-core-plugin/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Per TunnelFin Constitution (Principle III: Test-First Development), tests are MANDATORY.
All tasks MUST include test tasks that are written and verified to FAIL before implementation begins.
Target: 80%+ code coverage for unit tests, integration tests for all Jellyfin API contracts.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/TunnelFin/`, `tests/` at repository root
- Paths shown below follow the structure defined in plan.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create project directory structure per plan.md (src/TunnelFin with Core, Networking, BitTorrent, Streaming, Indexers, Discovery, Models, Configuration, Jellyfin subdirectories)
- [X] T002 Initialize C# .NET 10.0 class library project in src/TunnelFin/TunnelFin.csproj
- [X] T003 [P] Add NuGet dependencies: MonoTorrent 3.0.2, NSec.Cryptography 25.4.0, Jellyfin.Controller 10.11.5, Jellyfin.Model 10.11.5, Microsoft.Extensions.Http 10.0.1
- [X] T004 [P] Initialize test projects: tests/TunnelFin.Tests/TunnelFin.Tests.csproj (unit tests) and tests/TunnelFin.Integration/TunnelFin.Integration.csproj (integration tests)
- [X] T005 [P] Add test dependencies: xUnit 2.9.3, FluentAssertions 8.8.0, Moq 4.20.72, Coverlet 6.0.4 to both test projects
- [X] T006 [P] Configure .editorconfig for C# code style and formatting
- [X] T007 [P] Create test fixtures directory: tests/TunnelFin.Tests/Fixtures/ with sample torrent data and mock indexer responses
- [X] T008 Create .gitignore for .NET projects (bin/, obj/, *.user, .vs/)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Implement Plugin base class in src/TunnelFin/Core/Plugin.cs (inherits BasePlugin<PluginConfiguration>)
- [X] T010 [P] Create PluginConfiguration class in src/TunnelFin/Core/PluginConfiguration.cs (inherits BasePluginConfiguration)
- [X] T011 [P] Implement ServiceRegistration in src/TunnelFin/Core/ServiceRegistration.cs for dependency injection setup
- [X] T012 [P] Create base entity models in src/TunnelFin/Models/: TorrentStream.cs, AnonymityCircuit.cs, SearchResult.cs, FilterProfile.cs, IndexerConfiguration.cs, NetworkIdentity.cs
- [X] T013 [P] Create enumerations in src/TunnelFin/Models/Enums.cs: TorrentStreamState, CircuitState, ContentType, IndexerType, FilterAttribute, FilterOperator, SortAttribute, SortDirection
- [X] T014 [P] Implement configuration models in src/TunnelFin/Configuration/: AnonymitySettings.cs, ResourceLimits.cs, FilterSettings.cs
- [X] T015 [P] Create error handling infrastructure in src/TunnelFin/Core/Exceptions.cs (custom exception types: CircuitEstablishmentException, StreamInitializationException, ResourceLimitException)
- [X] T016 [P] Implement logging infrastructure in src/TunnelFin/Core/Logging.cs (privacy-aware logger that excludes PII and content titles per FR-037, FR-041)
- [X] T017 Write unit tests for Plugin initialization in tests/TunnelFin.Tests/Core/PluginTests.cs (verify GUID, Name, service registration)
- [X] T018 [P] Write unit tests for configuration models in tests/TunnelFin.Tests/Configuration/ConfigurationTests.cs (verify defaults, validation rules)
- [X] T019 [P] Write unit tests for logging infrastructure in tests/TunnelFin.Tests/Core/LoggingTests.cs (verify PII exclusion, content title redaction)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Anonymous Torrent Streaming (Priority: P1) 🎯 MVP

**Goal**: Enable users to stream torrent content with IP address protection through Tribler anonymity network

**Independent Test**: Search for content, select a torrent, verify playback occurs through Tribler network (green play button) with IP obscured from peers

### Tests for User Story 1 (MANDATORY - Test-First Development) ⚠️

> **CONSTITUTION REQUIREMENT: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T020 [P] [US1] Write unit tests for IPv8 protocol implementation in tests/TunnelFin.Tests/Networking/IPv8ProtocolTests.cs (peer discovery, handshake, message serialization)
- [X] T020a [P] [US1] Write byte-level IPv8 message verification tests in tests/TunnelFin.Tests/Networking/IPv8WireFormatTests.cs (verify C# serialization produces byte-identical output to Python struct.pack for introduction-request, puncture-request, CREATE messages using hex test vectors from py-ipv8)
- [X] T020b [P] [US1] Create Python test vector generation script in tests/fixtures/generate_ipv8_vectors.py (generates hex dumps of IPv8 messages using py-ipv8 for C# test verification per FR-048)
- [X] T021 [P] [US1] Write unit tests for circuit creation in tests/TunnelFin.Tests/Networking/CircuitManagerTests.cs (CREATE/CREATED/EXTEND/EXTENDED messages, hop selection, key exchange)
- [X] T021a [P] [US1] Write circuit message byte-level verification tests in tests/TunnelFin.Tests/Networking/CircuitMessageTests.cs (verify CREATE/CREATED/EXTEND/EXTENDED messages serialize with big-endian circuit IDs and correct field ordering)
- [X] T021b [P] [US1] Write circuit message parsing tests in tests/TunnelFin.Tests/Networking/CircuitMessageParsingTests.cs (verify C# can parse Python-generated circuit messages from test vectors)
- [X] T022 [P] [US1] Write unit tests for Ed25519 identity in tests/TunnelFin.Tests/Networking/Ed25519KeyPairTests.cs (key generation, signing, verification)
- [X] T022a [P] [US1] Write Ed25519 cross-language key format tests in tests/TunnelFin.Tests/Networking/Ed25519KeyFormatTests.cs (verify NSec RawPrivateKey import of PyNaCl to_seed() produces identical public key)
- [X] T022b [P] [US1] Write Ed25519 signature compatibility tests in tests/TunnelFin.Tests/Networking/Ed25519SignatureTests.cs (verify C# signatures verify in Python and vice versa using test vectors per FR-049)
- [X] T022c [P] [US1] Create Python Ed25519 test vector script in tests/fixtures/generate_ed25519_vectors.py (generates keypairs, signatures using PyNaCl for C# cross-validation)
- [X] T022d [P] [US1] Write Ed25519 signature determinism tests in tests/TunnelFin.Tests/Networking/Ed25519DeterminismTests.cs (verify same seed + message produces identical signature in C# and Python)
- [X] T023 [P] [US1] Write unit tests for TorrentEngine in tests/TunnelFin.Tests/BitTorrent/TorrentEngineTests.cs (torrent initialization, piece prioritization, stream creation)
- [X] T024 [P] [US1] Write unit tests for StreamManager in tests/TunnelFin.Tests/Streaming/StreamManagerTests.cs (HTTP endpoint creation, range requests, concurrent stream limits per FR-013)
- [X] T025 [P] [US1] Write unit tests for BufferManager in tests/TunnelFin.Tests/Streaming/BufferManagerTests.cs (buffer status tracking, >10s buffer requirement per SC-003)
- [X] T026 [P] [US1] Write integration test for anonymous stream initialization in tests/TunnelFin.Integration/AnonymousStreamingTests.cs (end-to-end: circuit establishment → torrent download → HTTP stream)

### Implementation for User Story 1

**IPv8 Protocol & Anonymity Layer (FR-001 to FR-006, FR-048)**

- [X] T027 [P] [US1] Implement IPv8 protocol base in src/TunnelFin/Networking/IPv8/Protocol.cs (message types, serialization, peer discovery per research.md)
- [X] T027a [P] [US1] Implement IPv8 message serialization in src/TunnelFin/Networking/IPv8/MessageSerializer.cs (use BinaryPrimitives for big-endian byte order per FR-048, ipv8-wire-format.md - circuit IDs as uint, ports as ushort, all big-endian)
- [X] T027b [P] [US1] Implement IPv8 message deserialization in src/TunnelFin/Networking/IPv8/MessageDeserializer.cs (parse big-endian messages from Python peers per ipv8-wire-format.md)
- [X] T028 [P] [US1] Implement Peer class in src/TunnelFin/Networking/IPv8/Peer.cs (peer info, connection state, handshake)
- [X] T029 [P] [US1] Implement Handshake protocol in src/TunnelFin/Networking/IPv8/Handshake.cs (four-message discovery: introduction-request/response, puncture-request/puncture)
- [X] T030 [US1] Implement CircuitManager in src/TunnelFin/Networking/Circuits/CircuitManager.cs (circuit creation, extension, relay selection per FR-001, FR-002, FR-003)
- [X] T031 [P] [US1] Implement Circuit class in src/TunnelFin/Networking/Circuits/Circuit.cs (state management, hop tracking, encryption keys per data-model.md)
- [X] T032 [P] [US1] Implement HopNode class in src/TunnelFin/Networking/Circuits/HopNode.cs (relay peer info, shared secret, encryption/decryption)
- [X] T033 [US1] Implement NetworkIdentity in src/TunnelFin/Networking/Identity/NetworkIdentity.cs (Ed25519 keypair generation, peer ID derivation per FR-004, FR-049)
- [X] T034 [P] [US1] Implement Ed25519KeyPair in src/TunnelFin/Networking/Identity/Ed25519KeyPair.cs (NSec.Cryptography wrapper using RawPrivateKey format for 32-byte seed compatibility with PyNaCl per FR-049, ipv8-wire-format.md, key storage per FR-038)
- [X] T035 [US1] Implement bandwidth contribution tracking in src/TunnelFin/Networking/BandwidthTracker.cs (proportional relay per FR-005, SC-010)
- [X] T035a [P] [US1] Implement TrustChain block serialization in src/TunnelFin/Networking/TrustChain/BlockSerializer.cs (exact field ordering per FR-050, ipv8-wire-format.md: creator key, link key, sequence, hash, timestamp, message length, message, signature)
- [X] T036 [US1] Implement circuit retry logic with configurable timeout in CircuitManager (default 30s per FR-040)

**BitTorrent Streaming Engine (FR-007 to FR-015)**

- [X] T037 [P] [US1] Implement TorrentEngine in src/TunnelFin/BitTorrent/TorrentEngine.cs (MonoTorrent integration, engine initialization per FR-007)
- [X] T038 [P] [US1] Implement TorrentStream in src/TunnelFin/BitTorrent/TorrentStream.cs (wraps MonoTorrent.Client.TorrentManager with streaming-specific logic, uses MonoTorrent.Streaming.StreamProvider for HTTP endpoint creation, manages torrent state per data-model.md)
- [X] T039 [P] [US1] Implement PiecePrioritizer in src/TunnelFin/BitTorrent/PiecePrioritizer.cs (sequential piece selection per FR-008, research.md custom prioritizer pattern)
- [X] T040 [P] [US1] Implement PeerManager in src/TunnelFin/BitTorrent/PeerManager.cs (peer connection management, routing through circuits)
- [X] T041 [US1] Implement StreamManager in src/TunnelFin/Streaming/StreamManager.cs (HTTP endpoint management, concurrent stream limits per FR-013, FR-015)
- [X] T042 [P] [US1] Implement HttpStreamEndpoint in src/TunnelFin/Streaming/HttpStreamEndpoint.cs (HTTP range request handling per FR-009, contracts/streaming-api.yaml)
- [X] T043 [P] [US1] Implement BufferManager in src/TunnelFin/Streaming/BufferManager.cs (buffer status tracking, >10s buffer per FR-010, SC-003)
- [X] T044 [US1] Implement stream initialization timeout (60s per FR-012) in StreamManager
- [X] T045 [US1] Implement resource limit enforcement in StreamManager (max concurrent streams, cache size per FR-013, FR-014, FR-015)

**Jellyfin Integration for Streaming (FR-027, FR-034, FR-039)**

- [X] T046 [US1] Implement TunnelFinSearchProvider skeleton in src/TunnelFin/Jellyfin/SearchProvider.cs (ISearchProvider interface, basic search integration per FR-027 - enhanced with metadata in T070)
- [X] T047 [US1] Implement play button color coding in SearchProvider (green=anonymous, orange=non-anonymous per FR-034)
- [X] T048 [US1] Implement Tribler network availability check in SearchProvider (per FR-039)

**Privacy & Consent (FR-035, FR-036)**

- [X] T049 [US1] Implement anonymous-first routing logic in StreamManager (attempt Tribler first per FR-035)
- [X] T050 [US1] Implement non-anonymous consent workflow in StreamManager (warning dialog, explicit consent per FR-036, contracts/search-provider.yaml ConsentRequired)

**Checkpoint**: At this point, User Story 1 should be fully functional - users can search, select torrents, and stream anonymously through Tribler network

---

## Phase 4: User Story 2 - Integrated Content Discovery (Priority: P2)

**Goal**: Enable users to discover and search torrent content directly from Jellyfin's native search interface with rich metadata

**Independent Test**: Use Jellyfin's search bar to find content, verify results appear with TMDB/AniList metadata, confirm filtering/sorting works

### Tests for User Story 2 (MANDATORY - Test-First Development) ⚠️

- [X] T051 [P] [US2] Write unit tests for IndexerManager in tests/TunnelFin.Tests/Indexers/IndexerManagerTests.cs (concurrent searches, timeout handling, response aggregation per FR-016, FR-018)
- [X] T052 [P] [US2] Write unit tests for built-in indexers in tests/TunnelFin.Tests/Indexers/BuiltInIndexersTests.cs (1337x, Nyaa, RARBG query parsing, result extraction)
- [X] T053 [P] [US2] Write unit tests for Torznab indexer in tests/TunnelFin.Tests/Indexers/TorznabIndexerTests.cs (custom endpoint support per FR-017)
- [X] T054 [P] [US2] Write unit tests for MetadataFetcher in tests/TunnelFin.Tests/Discovery/MetadataFetcherTests.cs (TMDB/AniList integration, exponential backoff per FR-030, failure caching per FR-031)
- [X] T055 [P] [US2] Write unit tests for Deduplicator in tests/TunnelFin.Tests/Discovery/DeduplicatorTests.cs (infohash, filename, smart hash deduplication per FR-025, SC-007)
- [X] T056 [P] [US2] Write integration test for search workflow in tests/TunnelFin.Integration/SearchWorkflowTests.cs (end-to-end: query → indexers → deduplication → metadata → results)

### Implementation for User Story 2

**Indexer Infrastructure (FR-016, FR-017, FR-018)**

- [X] T057 [P] [US2] Create IIndexer interface in src/TunnelFin/Indexers/IIndexer.cs (search method, capabilities, timeout)
- [X] T058 [P] [US2] Implement Indexer1337x in src/TunnelFin/Indexers/BuiltIn/Indexer1337x.cs (HTML parsing, result extraction per FR-016)
- [X] T059 [P] [US2] Implement IndexerNyaa in src/TunnelFin/Indexers/BuiltIn/IndexerNyaa.cs (anime-specific indexer per FR-016)
- [X] T060 [P] [US2] Implement IndexerRARBG in src/TunnelFin/Indexers/BuiltIn/IndexerRARBG.cs (general indexer per FR-016)
- [X] T061 [P] [US2] Implement TorznabIndexer in src/TunnelFin/Indexers/Torznab/TorznabIndexer.cs (custom indexer support per FR-017)
- [X] T062 [US2] Implement IndexerManager in src/TunnelFin/Indexers/IndexerManager.cs (concurrent search orchestration, max 5 concurrent per FR-018, SC-004)

**Content Discovery & Aggregation (FR-025, FR-026, FR-029 to FR-032)**

- [X] T063 [P] [US2] Implement SearchEngine in src/TunnelFin/Discovery/SearchEngine.cs (query distribution, result aggregation, <5s timeout per SC-004)
- [X] T064 [P] [US2] Implement Deduplicator in src/TunnelFin/Discovery/Deduplicator.cs (infohash, filename, smart hash deduplication per FR-025, 90% success rate per SC-007)
- [X] T065 [P] [US2] Implement MetadataFetcher in src/TunnelFin/Discovery/MetadataFetcher.cs (TMDB/AniList integration per FR-029)
- [X] T066 [US2] Implement exponential backoff retry in MetadataFetcher (1s, 2s, 4s per FR-030)
- [X] T067 [US2] Implement failure caching in MetadataFetcher (5-minute cache per FR-031)
- [X] T068 [US2] Implement filename parsing fallback in MetadataFetcher (basic metadata extraction per FR-032)
- [ ] T069 [US2] Implement title/year/episode matching in MetadataFetcher (verification against TMDB/AniList per FR-026, 95% success rate per SC-008)

**Jellyfin Integration for Discovery (FR-027, FR-028)**

- [X] T070 [US2] Enhance TunnelFinSearchProvider in src/TunnelFin/Jellyfin/SearchProvider.cs (full search implementation with metadata)
- [X] T071 [US2] Implement ChannelProvider in src/TunnelFin/Jellyfin/ChannelProvider.cs (IChannel interface, present torrents as library items per FR-028)
- [X] T072 [US2] Implement MetadataProvider in src/TunnelFin/Jellyfin/MetadataProvider.cs (metadata integration with Jellyfin's metadata system)

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently - users can search, discover content with metadata, and stream

---

## Phase 5: User Story 3 - Advanced Content Filtering (Priority: P3)

**Goal**: Provide power users with granular control over search results through custom filters for resolution, quality, codecs, and other attributes

**Independent Test**: Configure filter rules (e.g., "exclude 720p", "prefer BluRay"), perform searches, verify only matching torrents appear

### Tests for User Story 3 (MANDATORY - Test-First Development) ⚠️

- [X] T073 [P] [US3] Write unit tests for FilterEngine in tests/TunnelFin.Tests/Discovery/FilterEngineTests.cs (Required, Preferred, Excluded, Include filters per FR-019, FR-020, FR-021, FR-022)
- [X] T074 [P] [US3] Write unit tests for SortEngine in tests/TunnelFin.Tests/Discovery/SortEngineTests.cs (multi-criteria sorting per FR-023, <1s for 100+ results per SC-005)
- [X] T075 [P] [US3] Write unit tests for attribute parsing in tests/TunnelFin.Tests/Discovery/AttributeParserTests.cs (resolution, quality, codecs from filenames)
- [X] T076 [P] [US3] Write integration test for filter profiles in tests/TunnelFin.Integration/FilterProfileTests.cs (create profile, apply to search, verify results)

### Implementation for User Story 3

**Filtering Engine (FR-019 to FR-024)**

- [X] T077 [P] [US3] Implement FilterEngine in src/TunnelFin/Discovery/FilterEngine.cs (Required, Preferred, Excluded, Include filter types per FR-019)
- [X] T078 [P] [US3] Implement attribute extraction in AttributeParser (resolution, quality, codecs, audio, HDR, language, release group per FR-020)
- [X] T079 [P] [US3] Implement keyword/regex matching in FilterEngine (filename pattern matching per FR-021)
- [X] T080 [US3] Implement conditional filtering in FilterEngine (expression language support per FR-022, e.g., "exclude 720p if >5 results at 1080p")
- [X] T081 [P] [US3] Implement SortEngine in src/TunnelFin/Discovery/SortEngine.cs (multi-criteria sorting per FR-023, <1s for 100+ results per SC-005)
- [X] T082 [US3] Implement filter profile management in FilterProfileManager (separate profiles for Movies, TV Shows, Anime per FR-024)
- [X] T083 [US3] Implement profile configuration UI integration (configure profile in <2 minutes per SC-006)

**Checkpoint**: All three user stories should now be independently functional - search, discovery, filtering all work

---

## Phase 6: User Story 4 - Privacy Configuration Control (Priority: P2)

**Goal**: Enable privacy-conscious users to configure anonymity settings including hop count, network contribution, and consent preferences

**Independent Test**: Adjust hop count settings (1-3 hops), verify network relay contribution matches usage, confirm consent prompts appear

### Tests for User Story 4 (MANDATORY - Test-First Development) ⚠️

- [X] T084 [P] [US4] Write unit tests for AnonymitySettings in tests/TunnelFin.Tests/Configuration/AnonymitySettingsTests.cs (hop count validation 1-3, defaults per FR-003, FR-006)
- [X] T085 [P] [US4] Write unit tests for bandwidth contribution in tests/TunnelFin.Tests/Networking/BandwidthTrackerTests.cs (proportional relay per FR-005, 5% accuracy per SC-010)
- [X] T086 [P] [US4] Write integration test for privacy settings in tests/TunnelFin.Integration/PrivacySettingsTests.cs (change hop count, verify circuit creation uses new setting)

### Implementation for User Story 4

**Privacy Configuration (FR-003, FR-005, FR-006, FR-033, FR-037, FR-038)**

- [X] T087 [US4] Implement hop count configuration in src/TunnelFin/Configuration/AnonymitySettings.cs (1-3 hops, default 3 per FR-003, FR-006)
- [X] T088 [US4] Implement bandwidth contribution settings in AnonymitySettings (enable/disable relay per FR-005)
- [X] T089 [US4] Implement non-anonymous fallback settings in AnonymitySettings (allow/block per FR-040)
- [ ] T090 [US4] Implement logging level configuration in src/TunnelFin/Configuration/PluginSettings.cs (Minimal/Verbose per FR-037)
- [ ] T091 [US4] Implement scheduled catalog sync in src/TunnelFin/Configuration/ScheduledTasks.cs (automatic catalog syncing per FR-033)
- [ ] T092 [US4] Implement secure key storage in src/TunnelFin/Core/SecureStorage.cs (Jellyfin encrypted config for Ed25519 private key per FR-038)

**Checkpoint**: All four user stories complete - full feature set operational

---

## Phase 7: Observability & Monitoring (Cross-Cutting)

**Purpose**: Implement comprehensive metrics and monitoring without exposing PII

**Observability Requirements (FR-041 to FR-047)**

- [ ] T093 [P] Implement error logging in src/TunnelFin/Core/ErrorLogger.cs (no content titles or user identifiers per FR-041)
- [ ] T094 [P] Implement health status metrics in src/TunnelFin/Core/HealthMonitor.cs (plugin running/stopped, component health per FR-042)
- [ ] T095 [P] Implement active streams counter in src/TunnelFin/Streaming/StreamMetrics.cs (track active streams per FR-043)
- [ ] T096 [P] Implement bandwidth metrics in src/TunnelFin/Networking/BandwidthMetrics.cs (upload/download rates per FR-044)
- [ ] T097 [P] Implement circuit health metrics in src/TunnelFin/Networking/CircuitMetrics.cs (active circuits, hop distribution, failures per FR-045)
- [ ] T098 [P] Implement indexer response time tracking in src/TunnelFin/Indexers/IndexerMetrics.cs (per-indexer response times per FR-046)
- [ ] T099 [P] Implement cache hit rate tracking in src/TunnelFin/Discovery/CacheMetrics.cs (metadata cache, torrent data cache per FR-047)
- [ ] T100 Write unit tests for all metrics in tests/TunnelFin.Tests/Metrics/ (verify <1s latency per SC-009, verify no PII exposure per SC-013)

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T101 [P] Update quickstart.md with final deployment instructions
- [ ] T102 [P] Add XML documentation comments to all public APIs in src/TunnelFin/
- [ ] T103 Code cleanup and refactoring across all modules
- [ ] T104 Performance optimization: profile and optimize hot paths (piece prioritization, filtering, circuit encryption)
- [ ] T105 [P] Add additional unit tests to reach 80%+ coverage target (run Coverlet to identify gaps)
- [ ] T106 Security audit: review cryptographic implementations, key storage, PII handling
- [ ] T107 Run quickstart.md validation: verify all setup steps work on clean environment
- [ ] T108 Create plugin manifest JSON for Jellyfin plugin repository per plan.md section on plugin distribution
- [ ] T109 Final integration test: full user journey from plugin installation → search → filter → stream → metrics

### Success Criteria Validation (SC-001 to SC-013)

- [ ] T110 [P] Validate SC-001: Stream initialization <30s (measure with stopwatch in integration test)
- [ ] T111 [P] Validate SC-002: Anonymous routing success rate ≥95% (track circuit establishment success over 100 attempts)
- [ ] T112 [P] Validate SC-003: Buffer >10s during playback (monitor BufferManager during streaming)
- [ ] T113 [P] Validate SC-004: Search results <5s from 5 indexers (measure SearchEngine performance)
- [ ] T114 [P] Validate SC-005: Filter/sort <1s for 100+ results (benchmark FilterEngine)
- [ ] T115 [P] Validate SC-006: Metrics latency <1s (measure observability endpoint response time)
- [ ] T116 [P] Validate SC-007: Deduplication 90% success rate (test with known duplicate torrents)
- [ ] T117 [P] Validate SC-008: Metadata matching 95% accuracy (test with TMDB/AniList known titles)
- [ ] T118 [P] Validate SC-009: Test coverage ≥80% (run Coverlet and verify coverage report)
- [ ] T119 [P] Validate SC-010: Bandwidth contribution ±5% accuracy (compare relay vs download over time)
- [ ] T120 [P] Validate SC-011: 100% privacy warnings (verify consent prompts appear for all non-anonymous operations)
- [ ] T121 [P] Validate SC-012: Self-contained C# plugin (verify no external dependencies beyond NuGet)
- [ ] T122 [P] Validate SC-013: No PII in metrics (audit all observability data for privacy compliance)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational phase completion - Core streaming functionality
- **User Story 2 (Phase 4)**: Depends on Foundational phase completion - Can run in parallel with US1 if staffed, but typically follows US1 for MVP
- **User Story 3 (Phase 5)**: Depends on Foundational phase completion - Can run in parallel with US1/US2 if staffed
- **User Story 4 (Phase 6)**: Depends on Foundational phase completion - Can run in parallel with other stories if staffed
- **Observability (Phase 7)**: Can start after Foundational, runs in parallel with user stories
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories - **MVP CANDIDATE**
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Integrates with US1 (uses StreamManager) but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Integrates with US2 (filters search results) but independently testable
- **User Story 4 (P2)**: Can start after Foundational (Phase 2) - Configures US1 behavior but independently testable

### Within Each User Story

- **Tests MUST be written and FAIL before implementation** (Constitution: Test-First Development)
- Models before services (data structures before logic)
- Services before endpoints (business logic before API)
- Core implementation before integration (standalone functionality before cross-component integration)
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**: All tasks marked [P] can run in parallel
- T003, T004, T005, T006, T007 can all run simultaneously

**Phase 2 (Foundational)**: All tasks marked [P] can run in parallel
- T010-T016, T018-T019 can run simultaneously after T009 completes

**Phase 3 (User Story 1)**: Tests and models can run in parallel
- T020-T026 (all tests) can run in parallel
- T027-T029, T031-T032, T034, T037-T040, T042-T043 (models/independent components) can run in parallel
- Sequential dependencies: T030 depends on T027-T029, T036 depends on T030, T041 depends on T037-T040

**Phase 4 (User Story 2)**: Tests and indexers can run in parallel
- T051-T056 (all tests) can run in parallel
- T057-T061, T063-T065 (indexers and discovery components) can run in parallel
- Sequential dependencies: T062 depends on T057-T061, T070-T072 depend on T063-T069

**Phase 5 (User Story 3)**: Tests and engines can run in parallel
- T073-T076 (all tests) can run in parallel
- T077-T079, T081 (filter and sort engines) can run in parallel
- Sequential dependencies: T080 depends on T077-T079, T082-T083 depend on T077-T081

**Phase 6 (User Story 4)**: Tests can run in parallel
- T084-T086 (all tests) can run in parallel
- Implementation tasks T087-T092 are mostly independent

**Phase 7 (Observability)**: All metrics tasks can run in parallel
- T093-T099 can all run simultaneously

**Phase 8 (Polish)**: Documentation and testing tasks can run in parallel
- T101, T102, T105 can run in parallel

**Cross-Phase Parallelism**: Once Foundational phase completes, all user stories can start in parallel if team capacity allows

---

## Parallel Example: User Story 1

```bash
# Step 1: Launch all tests for User Story 1 together (MUST FAIL initially):
Task T020: "Write unit tests for IPv8 protocol implementation in tests/TunnelFin.Tests/Networking/IPv8ProtocolTests.cs"
Task T021: "Write unit tests for circuit creation in tests/TunnelFin.Tests/Networking/CircuitManagerTests.cs"
Task T022: "Write unit tests for Ed25519 identity in tests/TunnelFin.Tests/Networking/Ed25519KeyPairTests.cs"
Task T023: "Write unit tests for TorrentEngine in tests/TunnelFin.Tests/BitTorrent/TorrentEngineTests.cs"
Task T024: "Write unit tests for StreamManager in tests/TunnelFin.Tests/Streaming/StreamManagerTests.cs"
Task T025: "Write unit tests for BufferManager in tests/TunnelFin.Tests/Streaming/BufferManagerTests.cs"
Task T026: "Write integration test for anonymous stream initialization in tests/TunnelFin.Integration/AnonymousStreamingTests.cs"

# Step 2: Launch all independent models/components together:
Task T027: "Implement IPv8 protocol base in src/TunnelFin/Networking/IPv8/Protocol.cs"
Task T028: "Implement Peer class in src/TunnelFin/Networking/IPv8/Peer.cs"
Task T029: "Implement Handshake protocol in src/TunnelFin/Networking/IPv8/Handshake.cs"
Task T031: "Implement Circuit class in src/TunnelFin/Networking/Circuits/Circuit.cs"
Task T032: "Implement HopNode class in src/TunnelFin/Networking/Circuits/HopNode.cs"
Task T034: "Implement Ed25519KeyPair in src/TunnelFin/Networking/Identity/Ed25519KeyPair.cs"
Task T037: "Implement TorrentEngine in src/TunnelFin/BitTorrent/TorrentEngine.cs"
Task T038: "Implement StreamingTorrent in src/TunnelFin/BitTorrent/StreamingTorrent.cs"
Task T039: "Implement PiecePrioritizer in src/TunnelFin/BitTorrent/PiecePrioritizer.cs"
Task T040: "Implement PeerManager in src/TunnelFin/BitTorrent/PeerManager.cs"
Task T042: "Implement HttpStreamEndpoint in src/TunnelFin/Streaming/HttpStreamEndpoint.cs"
Task T043: "Implement BufferManager in src/TunnelFin/Streaming/BufferManager.cs"

# Step 3: Sequential tasks that depend on Step 2:
Task T030: "Implement CircuitManager in src/TunnelFin/Networking/Circuits/CircuitManager.cs" (depends on T027-T029)
Task T033: "Implement NetworkIdentity in src/TunnelFin/Networking/Identity/NetworkIdentity.cs" (depends on T034)
Task T041: "Implement StreamManager in src/TunnelFin/Streaming/StreamManager.cs" (depends on T037-T040)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) - RECOMMENDED

**Goal**: Deliver core anonymous streaming functionality as quickly as possible

1. **Complete Phase 1: Setup** (T001-T008)
   - Estimated time: 1-2 days
   - Deliverable: Project structure, dependencies configured

2. **Complete Phase 2: Foundational** (T009-T019)
   - Estimated time: 3-5 days
   - Deliverable: Plugin base, models, configuration, logging infrastructure
   - **CRITICAL GATE**: No user story work until this completes

3. **Complete Phase 3: User Story 1** (T020-T050)
   - Estimated time: 15-20 days
   - Deliverable: Anonymous torrent streaming through Tribler network
   - **STOP and VALIDATE**: Test User Story 1 independently
   - Verify: Search → Select torrent → Stream anonymously → View metrics

4. **Deploy/Demo MVP**
   - User Story 1 is a complete, valuable feature
   - Users can stream torrents anonymously
   - Gather feedback before building more features

### Incremental Delivery (Recommended After MVP)

**After MVP is validated, add features incrementally:**

1. **Foundation + US1** → Test independently → Deploy/Demo (MVP!)
2. **Add User Story 2** (T051-T072) → Test independently → Deploy/Demo
   - Estimated time: 10-12 days
   - Deliverable: Integrated content discovery with metadata
3. **Add User Story 4** (T084-T092) → Test independently → Deploy/Demo
   - Estimated time: 5-7 days
   - Deliverable: Privacy configuration controls
4. **Add User Story 3** (T073-T083) → Test independently → Deploy/Demo
   - Estimated time: 8-10 days
   - Deliverable: Advanced content filtering
5. **Add Observability** (T093-T100) → Test → Deploy/Demo
   - Estimated time: 5-7 days
   - Deliverable: Comprehensive monitoring
6. **Polish** (T101-T109) → Final release
   - Estimated time: 3-5 days
   - Deliverable: Production-ready plugin

**Total Estimated Time**: 50-65 days for full feature set

### Parallel Team Strategy

**With multiple developers, maximize parallelism:**

1. **Team completes Setup + Foundational together** (1 week)
2. **Once Foundational is done, split work:**
   - **Developer A**: User Story 1 (anonymous streaming) - 3 weeks
   - **Developer B**: User Story 2 (content discovery) - 2 weeks
   - **Developer C**: User Story 4 (privacy config) - 1 week
   - **Developer D**: Observability (metrics) - 1 week
3. **After 3 weeks**: US1, US2, US4, Observability all complete
4. **Team collaborates on User Story 3** (filtering) - 1.5 weeks
5. **Team collaborates on Polish** - 1 week

**Total Estimated Time with 4 developers**: 6-7 weeks

---

## Task Summary

**Total Tasks**: 122
- **Phase 1 (Setup)**: 8 tasks
- **Phase 2 (Foundational)**: 11 tasks (BLOCKS all user stories)
- **Phase 3 (User Story 1 - P1)**: 31 tasks (MVP candidate)
- **Phase 4 (User Story 2 - P2)**: 22 tasks
- **Phase 5 (User Story 3 - P3)**: 11 tasks
- **Phase 6 (User Story 4 - P2)**: 9 tasks
- **Phase 7 (Observability)**: 8 tasks
- **Phase 8 (Polish)**: 9 tasks + 13 success criteria validation tasks

**Parallel Opportunities**: 80 tasks marked [P] can run in parallel within their phase

**Independent Test Criteria**:
- **US1**: Search → Select torrent → Stream anonymously → Verify IP obscured
- **US2**: Search → Verify metadata → Verify filtering/sorting
- **US3**: Configure filters → Search → Verify only matching results
- **US4**: Adjust hop count → Verify circuits use new setting → Verify contribution tracking

**Suggested MVP Scope**: Phase 1 + Phase 2 + Phase 3 (User Story 1 only)
- **61 tasks total for MVP** (includes 11 new binary protocol compatibility tasks)
- **Estimated time**: 25-35 days single developer, 12-18 days with 2 developers
- **Deliverable**: Core anonymous torrent streaming functionality with verified byte-level IPv8/Ed25519 compatibility

---

## Format Validation

✅ **All tasks follow checklist format**: `- [ ] [ID] [P?] [Story?] Description with file path`
✅ **All user story tasks have [Story] labels**: [US1], [US2], [US3], [US4]
✅ **All tasks include exact file paths**: src/TunnelFin/..., tests/TunnelFin.Tests/...
✅ **All parallelizable tasks marked [P]**: 78 tasks can run in parallel
✅ **Tests are MANDATORY**: 37 test tasks across all user stories (Constitution compliance)
✅ **Independent test criteria defined**: Each user story has clear validation steps
✅ **Binary protocol compatibility**: 11 new tasks (T020a-T022d, T027a-T027b, T035a) ensure byte-level IPv8/Ed25519 compatibility with Python

---

## Notes

- **[P] tasks** = different files, no dependencies, can run in parallel
- **[Story] label** maps task to specific user story for traceability
- **Each user story** should be independently completable and testable
- **Verify tests fail** before implementing (Test-First Development)
- **Commit after each task** or logical group
- **Stop at any checkpoint** to validate story independently
- **Avoid**: vague tasks, same file conflicts, cross-story dependencies that break independence
- **Constitution compliance**: 80%+ coverage target, privacy-first defaults, test-first workflow

