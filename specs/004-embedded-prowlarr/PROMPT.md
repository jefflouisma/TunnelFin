# Specify Prompt: 004-embedded-prowlarr

## Status: ✅ spec.md Generated

The `/speckit.specify` workflow has been completed. See `spec.md` in this directory.

## Next Steps

```bash
# Continue the Specify workflow:
# 1. Run /speckit.plan with tech choices to generate plan.md
# 2. Run /speckit.tasks to generate tasks.md
# 3. Run /speckit.implement to execute
```

---

## Original Feature Description (used to generate spec.md)

Embed Prowlarr's Cardigann indexer engine directly into TunnelFin, making it completely self-contained without requiring external Jackett/Prowlarr installations.

**Current State**: TunnelFin has a `TorznabClient` that communicates with external Torznab endpoints (Jackett, Prowlarr) and built-in HTML scrapers (1337x, Nyaa, TorrentGalaxy, EZTV).

**Target State**: TunnelFin embeds Prowlarr's Cardigann engine to parse 522+ YAML indexer definitions, auto-update definitions from Prowlarr/Indexers GitHub repository, and expose an internal Torznab API that the existing `TorznabClient` consumes.

### Why This Feature?

1. **Self-contained**: No external Jackett/Prowlarr installation required - users just install the plugin
2. **522+ indexers**: Access to all Prowlarr Cardigann definitions (1337x, Nyaa, TorrentGalaxy, EZTV, private trackers, etc.)
3. **Auto-updates**: Definitions sync from `https://indexers.prowlarr.com/master/11/` without plugin updates
4. **Constitution compliance**: No external services required (Principle IV - Decentralized Architecture)

### User Scenarios

**P1: Browse and Enable Indexers**
- User opens TunnelFin settings → Indexers tab
- System shows 522+ indexers organized by category (Movies, TV, Anime, etc.)
- User enables desired indexers, enters credentials for private trackers
- Indexers appear in search results immediately

**P2: Search Across Multiple Indexers**
- User searches "Big Buck Bunny" in Jellyfin channel
- IndexerManager queries enabled Cardigann indexers in parallel
- Results aggregated, deduplicated by infohash, sorted by seeders
- User sees unified results from multiple sources

**P3: Auto-Update Definitions**
- On plugin startup, check `https://indexers.prowlarr.com/master/11/` for new version
- Download updated `package.zip` if version changed
- Extract YAML definitions to `{PluginDataPath}/Definitions/`
- New/updated indexers available without plugin update

**P4: Custom Definitions**
- User places custom `.yml` file in `{PluginDataPath}/Definitions/Custom/`
- System loads custom definitions alongside official ones
- Enables private/niche indexers not in official repo

### Technical Context

Port the following Prowlarr components from `reference_repos/Prowlarr/`:

1. **Cardigann Engine** (`src/NzbDrone.Core/Indexers/Definitions/Cardigann/`)
   - `CardigannDefinition.cs` - YAML definition model
   - `CardigannParser.cs` - HTML/JSON selector engine
   - `CardigannRequestGenerator.cs` - HTTP request builder
   - `CardigannSettings.cs` - Per-indexer settings

2. **Definition Update Service** (`src/NzbDrone.Core/IndexerVersions/`)
   - Downloads definitions from Prowlarr CDN
   - Version tracking with SHA256 verification

3. **Internal Torznab Endpoint**
   - Expose `/api/v1/indexer/{id}/newznab` internally
   - Wire existing `TorznabClient` to consume internal endpoint

### Dependencies

- Feature 003-core-integration (existing TorznabClient)
- New packages: YamlDotNet (YAML parsing), AngleSharp (HTML parsing)

### Constitution Alignment

- **I. Privacy-First**: No telemetry sent to Prowlarr; definition sync is one-way download
- **II. Seamless Integration**: Indexers appear in existing TunnelFin UI
- **III. Test-First**: Unit tests for YAML parsing, integration tests for live searches
- **IV. Decentralized**: No central service required; works offline with cached definitions
- **V. User Empowerment**: Users choose which indexers to enable

