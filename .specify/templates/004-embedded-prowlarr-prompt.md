# Specify Prompt: Embedded Prowlarr Integration

**Feature ID**: 004-embedded-prowlarr  
**Dependencies**: 003-core-integration  
**Reference**: `reference_repos/Prowlarr/`  

## Context

TunnelFin currently has a `TorznabClient` that communicates with external Torznab endpoints (Jackett, Prowlarr instances) and built-in HTML scrapers (1337x, Nyaa, TorrentGalaxy, EZTV). This feature embeds Prowlarr's Cardigann indexer engine directly into TunnelFin, making it self-contained.

**Why Embed Prowlarr?**
- **Self-contained**: No external Jackett/Prowlarr installation required
- **522+ indexers**: Access to all Prowlarr Cardigann definitions via YAML
- **Auto-updates**: Definitions sync from Prowlarr/Indexers GitHub repo
- **Constitution compliance**: No external services (Principle IV - Decentralized Architecture)

## Technical Architecture

### Core Prowlarr Components to Port

From `reference_repos/Prowlarr/src/NzbDrone.Core/`:

1. **Cardigann Engine** (`Indexers/Definitions/Cardigann/`)
   - `CardigannDefinition.cs` - YAML definition model (parse caps, login, search, download blocks)
   - `CardigannParser.cs` - HTML/JSON response parser with selector logic
   - `CardigannRequestGenerator.cs` - Request builder from definition
   - `CardigannSettings.cs` - Per-indexer settings (credentials, cookies)
   - `CardigannBase.cs` - Base indexer implementation

2. **Definition Update Service** (`IndexerVersions/`)
   - `IndexerDefinitionUpdateService.cs` - Downloads definitions from `https://indexers.prowlarr.com/master/11/package.zip`
   - `IndexerDefinitionVersionService.cs` - Tracks definition versions
   - YAML deserialization using `YamlDotNet`

3. **Indexer Factory** (`Indexers/`)
   - `IndexerFactory.cs` - Creates indexer instances from definitions
   - `IndexerCapabilities.cs` - Category mappings, search modes
   - `NewznabStandardCategory.cs` - Standard category definitions (Movies, TV, Anime)

4. **Torznab API Endpoint** (`Prowlarr.Api.V1/Indexers/NewznabController.cs`)
   - Exposes `/api/v1/indexer/{id}/newznab` for Torznab compatibility
   - TunnelFin's `TorznabClient` connects to this internal endpoint

### Integration Points with TunnelFin

```
TunnelFin Architecture:
┌─────────────────────────────────────────────────────────┐
│                   TunnelFinPlugin                        │
│  ┌─────────────────────────────────────────────────┐   │
│  │              IndexerManager                      │   │
│  │  ┌─────────────┐  ┌──────────────────────────┐  │   │
│  │  │TorznabClient│──│ EmbeddedProwlarr         │  │   │
│  │  └─────────────┘  │  ┌────────────────────┐  │  │   │
│  │                   │  │ CardigannEngine    │  │  │   │
│  │                   │  │ (YAML definitions) │  │  │   │
│  │                   │  └────────────────────┘  │  │   │
│  │                   │  ┌────────────────────┐  │  │   │
│  │                   │  │ DefinitionUpdater  │  │  │   │
│  │                   │  │ (GitHub sync)      │  │  │   │
│  │                   │  └────────────────────┘  │  │   │
│  │                   │  ┌────────────────────┐  │  │   │
│  │                   │  │ TorznabEndpoint    │  │  │   │
│  │                   │  │ (internal HTTP)    │  │  │   │
│  │                   │  └────────────────────┘  │  │   │
│  │                   └──────────────────────────┘  │   │
│  └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## User Scenarios

### P1: Browse and Enable Indexers
- User opens TunnelFin settings → Indexers tab
- System shows 522+ indexers organized by category (Movies, TV, Anime, etc.)
- User enables desired indexers, enters credentials for private trackers
- Indexers appear in search results immediately

### P2: Search Across Multiple Indexers  
- User searches "Big Buck Bunny" in Jellyfin channel
- IndexerManager queries enabled Cardigann indexers in parallel
- Results aggregated, deduplicated by infohash, sorted by seeders
- User sees unified results from multiple sources

### P3: Auto-Update Definitions
- On plugin startup, `DefinitionUpdater` checks `https://indexers.prowlarr.com/master/11/`
- Downloads updated `package.zip` if version changed
- Extracts YAML definitions to `{PluginDataPath}/Definitions/`
- New/updated indexers available without plugin update

### P4: Custom Definitions
- User places custom `.yml` file in `{PluginDataPath}/Definitions/Custom/`
- System loads custom definitions alongside official ones
- Enables private/niche indexers not in official repo

## Requirements

### Functional Requirements

- **FR-001**: System MUST parse Cardigann v11 YAML definitions (current Prowlarr version)
- **FR-002**: System MUST support login methods: post, form, cookie, get, redirect, redirect_headless
- **FR-003**: System MUST execute search blocks (html, json, xml selectors)
- **FR-004**: System MUST support all Cardigann filters (querystring, prepend, append, replace, split, etc.)
- **FR-005**: System MUST periodically sync definitions from Prowlarr/Indexers repo
- **FR-006**: System MUST cache definition updates (check once per startup, manual refresh available)
- **FR-007**: System MUST expose internal Torznab endpoint for existing `TorznabClient` compatibility
- **FR-008**: System MUST store indexer credentials encrypted using Jellyfin's config encryption
- **FR-009**: System MUST support per-indexer rate limiting (configurable, default 1 req/sec)
- **FR-010**: System MUST load custom definitions from user-specified directory
- **FR-011**: System MUST provide indexer health status (last successful search, error counts)
- **FR-012**: System MUST map Prowlarr categories to Jellyfin content types (Movie → Movies, TV → Series)

### Non-Functional Requirements

- **NFR-001**: Definition sync MUST complete within 30 seconds on typical connections
- **NFR-002**: YAML parsing MUST handle 522+ definitions without UI blocking
- **NFR-003**: Memory footprint MUST not exceed 50MB for all loaded definitions
- **NFR-004**: Failed indexers MUST NOT block searches on other indexers

### Key Entities

- **CardigannDefinition**: Parsed YAML definition (id, name, caps, login, search, download)
- **IndexerInstance**: Configured indexer with credentials and settings
- **SearchResult**: Normalized result with title, size, seeders, leechers, infohash, magnetUri
- **DefinitionVersion**: Version tracking for auto-update (lastUpdated, sha256)

## Implementation Strategy

### Phase 1: Cardigann Engine Port (Core)
1. Port `CardigannDefinition.cs` model classes
2. Implement YAML deserializer using `YamlDotNet`
3. Port `CardigannParser.cs` for HTML/JSON/XML selector logic
4. Port `CardigannRequestGenerator.cs` for building HTTP requests
5. Create `ICardigannIndexer` interface matching TunnelFin's `IIndexer`

### Phase 2: Definition Management
1. Implement `DefinitionUpdateService` to fetch from Prowlarr CDN
2. Create definition cache in `{PluginDataPath}/Definitions/`
3. Implement version tracking with SHA256 verification
4. Add custom definition folder support

### Phase 3: Integration Layer
1. Create `EmbeddedProwlarrService` as singleton
2. Implement `IndexerConfigurationStore` for credentials
3. Create internal Torznab HTTP endpoint (loopback only)
4. Wire `TorznabClient` to internal endpoint
5. Add UI for indexer selection and configuration

### Phase 4: Testing & Validation
1. Unit tests for YAML parsing (all Cardigann v11 features)
2. Integration tests against live indexers (1337x, Nyaa, etc.)
3. Performance tests for parallel indexer queries
4. Migration tests from existing TunnelFin indexer config

## Dependencies

### New NuGet Packages
- `YamlDotNet` (YAML parsing for Cardigann definitions)
- `AngleSharp` (HTML parsing - already used by Prowlarr)

### Files to Reference
```
reference_repos/Prowlarr/src/NzbDrone.Core/
├── Indexers/
│   ├── Definitions/Cardigann/      # Core engine
│   │   ├── CardigannDefinition.cs  # YAML model
│   │   ├── CardigannParser.cs      # Selector engine
│   │   ├── CardigannRequestGenerator.cs
│   │   ├── CardigannSettings.cs
│   │   └── Cardigann.cs            # Base implementation
│   ├── IndexerFactory.cs           # Instance creation
│   └── IndexerCapabilities.cs      # Category mappings
├── IndexerVersions/
│   ├── IndexerDefinitionUpdateService.cs  # GitHub sync
│   └── IndexerDefinition.cs        # Version tracking
└── Prowlarr.Api.V1/Indexers/
    └── NewznabController.cs        # Torznab endpoint

reference_repos/Prowlarr/Indexers/  # (External repo structure)
└── definitions/v11/                # Current YAML definitions
    ├── 1337x.yml
    ├── nyaasi.yml
    ├── torrentgalaxy.yml
    └── ... (522+ files)
```

## Success Criteria

- **SC-001**: All 522+ Prowlarr indexers loadable without errors
- **SC-002**: Search returns results within 5 seconds for typical queries
- **SC-003**: Definition updates detect and download new versions automatically
- **SC-004**: Existing `TorznabClient` works unchanged via internal endpoint
- **SC-005**: Memory usage under 50MB with all definitions loaded
- **SC-006**: 80%+ unit test coverage on Cardigann parsing logic

## Edge Cases

- What if definition YAML is malformed? → Log error, skip indexer, continue with others
- What if login fails on private tracker? → Mark indexer unhealthy, show error in UI
- What if Prowlarr CDN is unreachable? → Use cached definitions, retry later
- What if custom definition conflicts with official? → Custom takes precedence (by ID)
- What if indexer rate-limits aggressively? → Exponential backoff with max 60s delay
- What if selector returns no results? → Return empty results, don't error

## Constitution Compliance

- **I. Privacy-First**: No telemetry sent to Prowlarr servers; definition sync is one-way download
- **II. Seamless Integration**: Indexers appear in existing TunnelFin UI, no separate config
- **III. Test-First**: Unit tests for YAML parsing, integration tests for live searches
- **IV. Decentralized**: No central service required; works offline with cached definitions
- **V. User Empowerment**: Users choose which indexers to enable, control refresh intervals

