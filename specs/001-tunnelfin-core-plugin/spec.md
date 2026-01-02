# Feature Specification: TunnelFin Core Plugin

**Feature Branch**: `001-tunnelfin-core-plugin`
**Created**: January 1, 2026
**Status**: Draft
**Input**: User description: "PRD.md - TunnelFin native Jellyfin plugin for privacy-respecting torrent streaming"

## Clarifications

### Session 2026-01-01

- Q: When the Tribler anonymity network is unavailable or the user cannot establish circuits (e.g., insufficient peers, network connectivity issues), how should the system behave? → A: Retry circuit establishment for configurable duration (default 30 seconds) with progress indicator, then offer non-anonymous fallback with warning
- Q: What are the maximum resource limits for concurrent operations to prevent system exhaustion? → A: User-configurable limits with recommended defaults (3 streams, 10GB cache, 5 searches)
- Q: When external metadata services (TMDB, AniList/MyAnimeList) are unreachable or rate-limited, how should the system handle failures? → A: Retry with exponential backoff (1s, 2s, 4s), cache failures for 5 minutes, display basic metadata from torrent filename if all retries fail
- Q: What is the maximum time users should wait before the system times out during stream initialization (from play button click to playback start)? → A: 60 seconds
- Q: What critical metrics and logs should the system expose for operational monitoring and troubleshooting? → A: Comprehensive: Error logs, health status, active streams count, bandwidth usage (upload/download), circuit health metrics, indexer response times, cache hit rates (all without content titles or user identifiers)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Anonymous Torrent Streaming (Priority: P1)

A Jellyfin user wants to stream torrent content directly within their media server while protecting their IP address from the BitTorrent swarm through anonymous onion routing.

**Why this priority**: This is the core value proposition - privacy-first streaming is the primary differentiator and fundamental requirement for the entire plugin.

**Independent Test**: Can be fully tested by searching for content, selecting a torrent, and verifying that playback occurs through the Tribler anonymity network (green play button) with IP address obscured from peers.

**Acceptance Scenarios**:

1. **Given** user has TunnelFin plugin installed, **When** user searches for a movie in Jellyfin, **Then** torrent results appear with green play buttons indicating anonymous streaming availability
2. **Given** user selects a torrent with green play button, **When** user initiates playback, **Then** stream begins with traffic routed through multi-hop Tribler network
3. **Given** stream is active, **When** user views stream health overlay, **Then** overlay shows peer count, download speed, and confirms anonymous routing is active
4. **Given** content is only available on standard BitTorrent, **When** user attempts to play, **Then** system displays orange play button and warning about IP exposure requiring explicit consent

---

### User Story 2 - Integrated Content Discovery (Priority: P2)

A Jellyfin user wants to discover and search for torrent content directly from Jellyfin's native search interface without managing separate torrent applications or indexers.

**Why this priority**: Seamless integration is critical for user experience, but depends on the core streaming functionality being operational first.

**Independent Test**: Can be tested by using Jellyfin's search bar to find content, verifying results appear with metadata, and confirming results are filtered/sorted according to user preferences.

**Acceptance Scenarios**:

1. **Given** user is in Jellyfin interface, **When** user types movie title in search bar, **Then** TunnelFin returns matching torrents with TMDB metadata (poster, description, year)
2. **Given** multiple torrent sources exist, **When** search completes, **Then** best match appears prominently with alternatives in expandable section
3. **Given** user has configured quality preferences, **When** search results appear, **Then** results are filtered and sorted according to user's resolution, codec, and seeder preferences
4. **Given** user searches for anime, **When** results appear, **Then** metadata is fetched from AniList/MyAnimeList with correct season/episode matching

---

### User Story 3 - Advanced Content Filtering (Priority: P3)

A power user wants granular control over torrent search results through custom filters for resolution, quality, codecs, release groups, and other attributes to ensure they get exactly the content quality they prefer.

**Why this priority**: Enhances user experience but is not essential for basic functionality - users can manually select from unfiltered results.

**Independent Test**: Can be tested by configuring filter rules (e.g., "exclude 720p", "prefer BluRay"), performing searches, and verifying only matching torrents appear in results.

**Acceptance Scenarios**:

1. **Given** user configures filter rule "Required: 1080p or higher", **When** user searches for content, **Then** only torrents with 1080p, 2160p, or 4K resolution appear
2. **Given** user sets "Excluded: CAM, TS quality", **When** search results load, **Then** no camera or telesync releases appear in results
3. **Given** user creates content-type profile for anime with "Preferred: Japanese audio", **When** searching anime, **Then** torrents with Japanese audio are ranked higher
4. **Given** user enables conditional filter "Exclude 720p if >5 results at 1080p", **When** search returns 10 results (6 at 1080p, 4 at 720p), **Then** only 1080p results are shown

---

### User Story 4 - Privacy Configuration Control (Priority: P2)

A privacy-conscious user wants to configure their anonymity settings including hop count, network contribution, and consent preferences to balance privacy with performance.

**Why this priority**: Essential for user empowerment and privacy control, but core anonymous streaming must work first.

**Independent Test**: Can be tested by adjusting hop count settings (1-3 hops), verifying network relay contribution matches usage, and confirming consent prompts appear for non-anonymous streams.

**Acceptance Scenarios**:

1. **Given** user accesses plugin settings, **When** user sets hop count to 3, **Then** all subsequent streams route through 3-hop circuits for maximum anonymity
2. **Given** user has downloaded 1GB via anonymous network, **When** checking network contribution, **Then** system has relayed proportional traffic for other peers
3. **Given** user disables "allow non-anonymous fallback", **When** content is only available on standard BitTorrent, **Then** playback is blocked entirely with explanation
4. **Given** user enables verbose logging, **When** streaming occurs, **Then** detailed network and torrent logs are available for troubleshooting (without exposing content titles)

---

### Edge Cases

- **Tribler network unavailable or circuit establishment fails**: System retries for configurable duration (default 30 seconds) with progress indicator, then offers non-anonymous fallback with explicit warning
- **Resource limits reached**: System rejects new operations (streams, searches) when configured limits are reached and displays clear message indicating current usage and limits
- **Metadata services unreachable**: System retries with exponential backoff (1s, 2s, 4s), caches failures for 5 minutes, displays basic metadata parsed from torrent filename if all retries fail
- **No seeders or low availability**: System displays warning "No active seeders found" and allows user to queue torrent for later retry. Stream initialization fails gracefully after 60s timeout with retry option.
- **Mid-playback privacy mode switch**: Switching between anonymous and non-anonymous modes mid-playback triggers graceful stream transition - buffer current data, establish new connection type, resume from buffered position with <5s interruption.
- **Insufficient disk space**: System checks available disk space before stream initialization. If insufficient (<2GB free or less than 2x torrent size), reject with clear error: "Insufficient disk space for buffering. Free at least X GB."
- **Malformed or corrupted torrent files**: System validates torrent file structure before adding to engine. If validation fails, skip torrent with error log entry and display "Invalid torrent file" to user. Continue processing other search results.

## Requirements *(mandatory)*

### Functional Requirements

**Core Anonymity Layer**

- **FR-001**: System MUST implement IPv8 protocol in C# to participate in existing Tribler network
- **FR-002**: System MUST route all BitTorrent traffic through multi-hop onion circuits by default
- **FR-003**: Users MUST be able to configure hop count between 1-3 hops for anonymity circuits
- **FR-004**: System MUST generate and securely store persistent Ed25519 cryptographic identity for network participation
- **FR-005**: System MUST contribute relay bandwidth proportional to user's download usage to ensure network fairness
- **FR-006**: System MUST default to maximum privacy settings (3-hop routing, anonymous-only mode)

**Torrent Streaming Engine**

- **FR-007**: System MUST use MonoTorrent library for BitTorrent protocol implementation
- **FR-008**: System MUST prioritize downloading torrent pieces sequentially to enable immediate playback using a sliding window approach (prioritize next 10-20 pieces ahead of playback position, similar to TorrServer's approach)
- **FR-009**: System MUST expose active torrents as local HTTP streams consumable by Jellyfin player
- **FR-010**: System MUST buffer minimum 10 seconds of playback data before starting stream to prevent stuttering (per SC-003)
- **FR-011**: System MUST display real-time stream health metrics (peer count, download speed, buffer status)
- **FR-012**: System MUST timeout stream initialization after 60 seconds and display error message with retry option
- **FR-013**: Users MUST be able to configure maximum concurrent streams (default: 3)
- **FR-014**: Users MUST be able to configure maximum torrent cache size (default: 10GB)
- **FR-015**: System MUST enforce configured resource limits and reject new operations when limits are reached

**Content Discovery & Aggregation**

- **FR-016**: System MUST search multiple torrent indexers simultaneously (1337x, Nyaa.si, RARBG built-in)
- **FR-017**: Users MUST be able to add custom indexers via Torznab-compatible API endpoints
- **FR-018**: Users MUST be able to configure maximum concurrent indexer searches (default: 5)
- **FR-019**: System MUST support filter rules as Required, Preferred, Excluded, or Include (whitelist)
- **FR-020**: System MUST allow filtering by resolution, quality, codecs, audio channels, HDR, language, file size, seeders, release group
- **FR-021**: System MUST support keyword inclusion/exclusion and regex pattern matching on filenames
- **FR-022**: System MUST support conditional filtering with expression language using simple comparison operators (==, !=, >, <, >=, <=, contains) and logical operators (AND, OR, NOT) - e.g., "exclude 720p if >5 results at 1080p"
- **FR-023**: System MUST allow custom multi-criteria sort orders (e.g., "resolution desc, seeders desc, size asc")
- **FR-024**: System MUST support separate filter/sort profiles for Movies, TV Shows, and Anime
- **FR-025**: System MUST deduplicate identical torrents by infohash, filename, or smart detection hash
- **FR-026**: System MUST verify search results against TMDB/AniList data for accurate title/year/episode matching

**Jellyfin Integration**

- **FR-027**: System MUST register as Jellyfin search provider for native search bar integration
- **FR-028**: System MUST present discovered content as native Jellyfin library items with rich metadata
- **FR-029**: System MUST fetch metadata from TMDB for movies/TV and AniList/MyAnimeList for anime
- **FR-030**: System MUST retry metadata fetching with exponential backoff (1s, 2s, 4s) when external services are unreachable
- **FR-031**: System MUST cache metadata service failures for 5 minutes to avoid repeated failed requests
- **FR-032**: System MUST parse and display basic metadata from torrent filename when all metadata service retries fail
- **FR-033**: Users MUST be able to configure scheduled tasks for automatic catalog syncing from torrent sources
- **FR-034**: System MUST display color-coded play buttons (green=anonymous, orange=non-anonymous)

**Privacy & Security**

- **FR-035**: System MUST attempt to source content through Tribler network first before standard BitTorrent
- **FR-036**: System MUST display clear warning and require explicit consent before streaming via non-anonymous BitTorrent
- **FR-037**: System MUST default to minimal logging with no personally identifiable information or content titles
- **FR-038**: System MUST store Tribler network private key in Jellyfin's encrypted configuration storage
- **FR-039**: System MUST check Tribler network availability before presenting play options to prevent dead links
- **FR-040**: System MUST retry circuit establishment for configurable duration (default 30 seconds) with progress indicator when Tribler network is unavailable, then offer non-anonymous fallback with warning

**Observability & Monitoring**

- **FR-041**: System MUST expose error logs for troubleshooting without including content titles or user identifiers
- **FR-042**: System MUST provide health status metrics (plugin running/stopped, component health)
- **FR-043**: System MUST track and expose active streams count metric
- **FR-044**: System MUST track and expose bandwidth usage metrics (upload and download rates)
- **FR-045**: System MUST track and expose circuit health metrics (active circuits, hop count distribution, circuit failures)
- **FR-046**: System MUST track and expose indexer response time metrics for each configured indexer
- **FR-047**: System MUST track and expose cache hit rate metrics (metadata cache, torrent data cache)

**Binary Protocol Compatibility**

- **FR-048**: IPv8 protocol implementation MUST serialize all messages using big-endian byte order (BinaryPrimitives) to ensure byte-identical wire format compatibility with Python py-ipv8 implementation
- **FR-049**: Ed25519 cryptographic keys MUST use 32-byte raw seed format (NSec RawPrivateKey) compatible with PyNaCl to_seed() for cross-platform signature verification
- **FR-050**: TrustChain block serialization MUST follow exact field ordering (creator key, link key, sequence, hash, timestamp, message length, message, signature) to ensure signature verification succeeds across Python and C# implementations

### Key Entities

- **Torrent Stream**: Represents an active torrent being downloaded and streamed, including torrent metadata, download progress, peer connections, and HTTP stream endpoint
- **Anonymity Circuit**: Represents a multi-hop onion routing path through Tribler network peers, including hop count, relay nodes, and circuit health status
- **Search Result**: Represents a discovered torrent with metadata including title, resolution, quality, codecs, file size, seeders, source indexer, and match confidence
- **Filter Profile**: Represents user-defined filtering and sorting rules for a content type (Movies/TV/Anime), including required/preferred/excluded attributes and sort criteria
- **Indexer Configuration**: Represents a torrent indexer source (built-in or custom Torznab), including endpoint URL, API key, enabled status, and search capabilities
- **Network Identity**: Represents user's persistent Tribler network identity including Ed25519 keypair, peer ID, and network contribution statistics

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can discover and begin streaming torrent content within 30 seconds of searching in Jellyfin (or receive timeout error within 60 seconds if stream cannot initialize)
- **SC-002**: 95% of streams successfully route through Tribler anonymity network without exposing user IP address
- **SC-003**: System maintains playback buffer above 10 seconds during streaming to prevent interruptions
- **SC-004**: Search results return within 5 seconds when querying up to 5 indexers simultaneously
- **SC-005**: Filter and sort operations process 100+ search results in under 1 second
- **SC-006**: Users can configure and save custom filter profiles in under 2 minutes
- **SC-007**: System successfully deduplicates 90% of identical torrents from multiple indexers
- **SC-008**: Metadata fetching completes for 95% of search results with accurate title/year/episode matching
- **SC-009**: Stream health overlay updates in real-time with <1 second latency
- **SC-010**: System contributes relay bandwidth proportional to download usage within 5% accuracy
- **SC-011**: Privacy warnings appear 100% of the time before non-anonymous streaming with clear consent requirement
- **SC-012**: Plugin operates as self-contained C# component without requiring external Docker services or dependencies beyond .NET runtime
- **SC-013**: All operational metrics are exposed and accessible without revealing content titles or user identifiers
