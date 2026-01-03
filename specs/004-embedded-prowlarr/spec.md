# Feature Specification: Embedded Prowlarr Integration

**Feature Branch**: `004-embedded-prowlarr`  
**Created**: January 3, 2026  
**Status**: Draft  
**Input**: User description: "Embed Prowlarr's Cardigann indexer engine directly into TunnelFin, making it completely self-contained without requiring external Jackett/Prowlarr installations. Support 522+ YAML indexer definitions with auto-updates from Prowlarr/Indexers repository."

**Dependency**: This feature extends `003-core-integration` by replacing external Torznab dependency with embedded Cardigann engine that the existing `TorznabClient` consumes via internal endpoint.

## Clarifications

### Session 2026-01-03

- Q: Should we port all Prowlarr code or just Cardigann? → A: Only Cardigann engine + definition update service. Skip Prowlarr's database, UI, API layers.
- Q: How to handle Prowlarr's NzbDrone dependencies? → A: Extract only required classes, adapt to TunnelFin's DI container and logging.
- Q: Definition storage location? → A: `{PluginDataPath}/Definitions/` for official, `{PluginDataPath}/Definitions/Custom/` for user-defined.
- Q: How often to check for definition updates? → A: On plugin startup only (avoid background polling). Manual refresh button in settings.
- Q: Internal Torznab endpoint security? → A: Loopback only (127.0.0.1), no authentication required for internal calls.
- Q: Handle indexers requiring cookies/captchas? → A: Support FlareSolverr proxy configuration for Cloudflare-protected sites.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enable Built-in Indexers (Priority: P1)

A user wants to enable torrent indexers without installing external software. The system shows available indexers from embedded Cardigann definitions and allows enabling them with one click for public indexers.

**Why this priority**: This is the core value proposition - zero-config indexer access. Users get 522+ indexers immediately without Jackett/Prowlarr setup.

**Independent Test**: Can be tested by opening TunnelFin settings, viewing indexer list, enabling 1337x, and verifying search returns results.

**Acceptance Scenarios**:

1. **Given** TunnelFin plugin is installed, **When** user opens Indexers settings, **Then** 522+ indexers are listed organized by category
2. **Given** indexer list is displayed, **When** user enables "1337x" (public), **Then** indexer becomes active immediately without credentials
3. **Given** indexer is enabled, **When** user searches in TunnelFin channel, **Then** results from enabled indexer appear
4. **Given** indexer search fails, **When** error occurs, **Then** error is logged and other indexers continue working

---

### User Story 2 - Configure Private Trackers (Priority: P1)

A user wants to add private tracker credentials. The system allows entering username/password or cookies for private indexers, validates credentials, and stores them securely.

**Why this priority**: Private trackers often have better quality content. Supporting them is essential for power users.

**Independent Test**: Can be tested by enabling a private tracker (e.g., IPTorrents), entering credentials, and verifying successful login and search.

**Acceptance Scenarios**:

1. **Given** private indexer selected, **When** user clicks configure, **Then** credential form shows required fields from YAML definition
2. **Given** credentials entered, **When** user saves, **Then** system validates by attempting test search
3. **Given** validation succeeds, **When** credentials saved, **Then** they are encrypted using Jellyfin's config encryption
4. **Given** validation fails, **When** login rejected, **Then** clear error message shown (invalid credentials, 2FA required, etc.)

---

### User Story 3 - Auto-Update Definitions (Priority: P2)

On plugin startup, the system checks for updated indexer definitions from Prowlarr's CDN and downloads new versions automatically.

**Why this priority**: Indexers change frequently (domains, selectors). Auto-updates ensure continued functionality without plugin updates.

**Independent Test**: Can be tested by checking definition version, manually triggering update, and verifying new definitions are downloaded.

**Acceptance Scenarios**:

1. **Given** plugin starts, **When** definition check runs, **Then** system compares local version with `https://indexers.prowlarr.com/master/11/`
2. **Given** newer version available, **When** download completes, **Then** YAML files extracted to `{PluginDataPath}/Definitions/`
3. **Given** CDN unreachable, **When** timeout occurs, **Then** existing cached definitions used with warning logged
4. **Given** definitions updated, **When** user opens indexer list, **Then** new/updated indexers visible immediately

---

### User Story 4 - Custom Indexer Definitions (Priority: P3)

A user wants to add a custom indexer not in the official repository. The system loads YAML files from custom directory alongside official definitions.

**Why this priority**: Enables niche/private indexers and community contributions without waiting for official inclusion.

**Independent Test**: Can be tested by placing custom `.yml` file in Custom folder and verifying indexer appears in list.

**Acceptance Scenarios**:

1. **Given** custom YAML file placed in `{PluginDataPath}/Definitions/Custom/`, **When** plugin reloads, **Then** custom indexer appears in list
2. **Given** custom definition has same ID as official, **When** both loaded, **Then** custom takes precedence
3. **Given** custom YAML is malformed, **When** parsing fails, **Then** error logged and other indexers load normally

---

### User Story 5 - Search Across Multiple Indexers (Priority: P2)

A user searches and expects results from all enabled indexers aggregated and deduplicated.

**Why this priority**: Parallel search across indexers is the primary use case after setup.

**Independent Test**: Can be tested by enabling 3+ indexers, searching, and verifying results from multiple sources.

**Acceptance Scenarios**:

1. **Given** multiple indexers enabled, **When** search executed, **Then** all indexers queried in parallel
2. **Given** results returned, **When** same infohash from multiple sources, **Then** deduplicated keeping highest seeder count
3. **Given** one indexer times out, **When** 10s timeout reached, **Then** results from other indexers still returned
4. **Given** rate limit hit, **When** 429 received, **Then** exponential backoff applied per indexer

---

### Edge Cases

- **Malformed YAML definition**: Log parsing error with definition ID, skip indexer, continue loading others
- **Definition references missing file**: Log warning, indexer shown as "broken" in UI
- **Login session expires**: Detect 401/403 on search, prompt user to re-authenticate
- **Indexer domain changes**: Definition update includes new domain; old cached responses invalidated
- **Concurrent definition updates**: Mutex prevents race conditions during download/extract
- **Disk full during update**: Rollback to previous definitions, log error
- **FlareSolverr unavailable**: Skip Cloudflare-protected indexers, show warning in UI

## Requirements *(mandatory)*

### Functional Requirements

**Cardigann Engine**

- **FR-001**: System MUST parse Cardigann v11 YAML definitions (current Prowlarr format)
- **FR-002**: System MUST support all Cardigann login methods: post, form, cookie, get, redirect
- **FR-003**: System MUST execute search blocks with html, json, xml selector types
- **FR-004**: System MUST support all Cardigann filters: querystring, prepend, append, replace, split, regexp, dateparse, timeago, etc.
- **FR-005**: System MUST support Cardigann download blocks for magnet/torrent URL extraction
- **FR-006**: System MUST handle indexer capabilities (categories, search modes, limits)

**Definition Management**

- **FR-007**: System MUST download definitions from `https://indexers.prowlarr.com/master/11/package.zip`
- **FR-008**: System MUST verify download integrity using SHA256 checksum
- **FR-009**: System MUST extract and cache definitions in `{PluginDataPath}/Definitions/`
- **FR-010**: System MUST load custom definitions from `{PluginDataPath}/Definitions/Custom/`
- **FR-011**: System MUST track definition versions to detect updates
- **FR-012**: Custom definitions MUST take precedence over official definitions with same ID

**Indexer Configuration**

- **FR-013**: System MUST store indexer credentials encrypted using Jellyfin's configuration encryption
- **FR-014**: System MUST validate credentials by executing test search on save
- **FR-015**: System MUST support per-indexer rate limiting (configurable, default 1 req/sec)
- **FR-016**: System MUST implement exponential backoff on 429/503 errors (1s, 2s, 4s, 8s, max 60s)
- **FR-017**: System MUST track indexer health (last success, error count, average response time)

**Internal Torznab API**

- **FR-018**: System MUST expose internal Torznab endpoint at `http://127.0.0.1:{port}/api/indexer/{id}/torznab`
- **FR-019**: System MUST implement Torznab API spec: caps, search, tvsearch, movie, music
- **FR-020**: Existing `TorznabClient` MUST consume internal endpoint transparently
- **FR-021**: Internal endpoint MUST only accept connections from loopback (127.0.0.1)

**Category Mapping**

- **FR-022**: System MUST map Prowlarr categories to Jellyfin content types (Movies, Series, Music)
- **FR-023**: System MUST support NewznabStandardCategory mappings (2000=Movies, 5000=TV, etc.)

### Key Entities

- **CardigannDefinition**: Parsed YAML definition (id, name, language, type, encoding, caps, login, search, download)
- **IndexerInstance**: Configured indexer with credentials, settings, and health metrics
- **DefinitionVersion**: Version tracking (lastUpdated, sha256, definitionCount)
- **CardigannRequest**: HTTP request built from definition (url, method, headers, body, cookies)
- **CardigannResponse**: Parsed response with extracted fields (title, size, seeders, magnet, etc.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 522+ Prowlarr Cardigann definitions load without parsing errors
- **SC-002**: YAML parsing completes within 5 seconds for all definitions on startup
- **SC-003**: Memory usage for loaded definitions stays under 50MB
- **SC-004**: Search across 10 indexers completes within 15 seconds
- **SC-005**: Definition update check completes within 10 seconds on typical connection
- **SC-006**: Existing `TorznabClient` works unchanged via internal endpoint
- **SC-007**: 80%+ unit test coverage on Cardigann parsing logic
- **SC-008**: Failed indexers do not block or slow down searches on other indexers

## Technical Design *(mandatory)*

### Technology Stack

- **Runtime**: C# / .NET 10.0 (Jellyfin plugin requirement)
- **YAML Parsing**: YamlDotNet 16.x (Cardigann definition parsing)
- **HTML Parsing**: AngleSharp 1.x (selector engine for HTML responses)
- **HTTP Client**: Microsoft.Extensions.Http 10.0.1 (indexer requests with retry/timeout)
- **Cryptography**: Jellyfin's built-in config encryption (credential storage)
- **Compression**: System.IO.Compression (package.zip extraction)

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                      TunnelFin Plugin                                │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    IndexerManager                            │   │
│  │  ┌─────────────────┐     ┌─────────────────────────────┐    │   │
│  │  │ TorznabClient   │────▶│ EmbeddedProwlarrService     │    │   │
│  │  │ (unchanged)     │     │  ┌───────────────────────┐  │    │   │
│  │  └─────────────────┘     │  │ CardigannEngine       │  │    │   │
│  │                          │  │ - YamlParser          │  │    │   │
│  │                          │  │ - RequestGenerator    │  │    │   │
│  │                          │  │ - ResponseParser      │  │    │   │
│  │                          │  │ - SelectorEngine      │  │    │   │
│  │                          │  └───────────────────────┘  │    │   │
│  │                          │  ┌───────────────────────┐  │    │   │
│  │                          │  │ DefinitionManager     │  │    │   │
│  │                          │  │ - UpdateService       │  │    │   │
│  │                          │  │ - VersionTracker      │  │    │   │
│  │                          │  │ - CustomLoader        │  │    │   │
│  │                          │  └───────────────────────┘  │    │   │
│  │                          │  ┌───────────────────────┐  │    │   │
│  │                          │  │ InternalTorznabApi    │  │    │   │
│  │                          │  │ - /api/indexer/{id}   │  │    │   │
│  │                          │  │ - Loopback only       │  │    │   │
│  │                          │  └───────────────────────┘  │    │   │
│  │                          └─────────────────────────────┘    │   │
│  └─────────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                 IndexerConfigurationStore                    │   │
│  │  - Credentials (encrypted)                                   │   │
│  │  - Enabled indexers                                          │   │
│  │  - Per-indexer settings                                      │   │
│  │  - Health metrics                                            │   │
│  └─────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

### Component Specifications

#### 1. CardigannDefinition Model

**File**: `src/TunnelFin/Indexers/Cardigann/Models/CardigannDefinition.cs`

```csharp
public class CardigannDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Language { get; set; } = "en-US";
    public string Type { get; set; } = "public"; // public, semi-private, private
    public string Encoding { get; set; } = "UTF-8";
    public List<string> Links { get; set; } = [];
    public List<string> Legacylinks { get; set; } = [];

    public CapabilitiesBlock? Caps { get; set; }
    public LoginBlock? Login { get; set; }
    public SearchBlock? Search { get; set; }
    public DownloadBlock? Download { get; set; }
    public List<SettingField> Settings { get; set; } = [];
}

public class CapabilitiesBlock
{
    public Dictionary<string, string> Categories { get; set; } = [];
    public List<string> Modes { get; set; } = []; // search, tv-search, movie-search
}

public class LoginBlock
{
    public string Path { get; set; } = "";
    public string Method { get; set; } = "post"; // post, form, cookie, get
    public string Inputs { get; set; } = "";
    public ErrorBlock? Error { get; set; }
    public TestBlock? Test { get; set; }
}

public class SearchBlock
{
    public string Path { get; set; } = "";
    public string Method { get; set; } = "get";
    public Dictionary<string, string> Inputs { get; set; } = [];
    public RowsBlock? Rows { get; set; }
    public Dictionary<string, FieldSelector> Fields { get; set; } = [];
}
```

#### 2. CardigannEngine

**File**: `src/TunnelFin/Indexers/Cardigann/CardigannEngine.cs`

```csharp
public class CardigannEngine
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CardigannEngine> _logger;
    private readonly SelectorEngine _selectorEngine;

    public async Task<List<SearchResult>> SearchAsync(
        CardigannDefinition definition,
        IndexerCredentials? credentials,
        string query,
        CancellationToken ct = default)
    {
        // 1. Build search request from definition
        var request = BuildSearchRequest(definition, query);

        // 2. Execute request (with login if needed)
        var response = await ExecuteRequestAsync(definition, credentials, request, ct);

        // 3. Parse response using selectors
        return ParseSearchResponse(definition, response);
    }

    private HttpRequestMessage BuildSearchRequest(CardigannDefinition def, string query)
    {
        var search = def.Search ?? throw new InvalidOperationException("No search block");
        var url = ApplyFilters(search.Path, new Dictionary<string, string>
        {
            ["query"] = query,
            ["categories"] = string.Join(",", def.Caps?.Categories.Keys ?? [])
        });

        return new HttpRequestMessage(
            search.Method.ToUpperInvariant() == "POST" ? HttpMethod.Post : HttpMethod.Get,
            new Uri(def.Links.First() + url));
    }

    private List<SearchResult> ParseSearchResponse(CardigannDefinition def, string html)
    {
        var results = new List<SearchResult>();
        var rows = _selectorEngine.SelectRows(html, def.Search!.Rows!);

        foreach (var row in rows)
        {
            results.Add(new SearchResult
            {
                Title = _selectorEngine.SelectField(row, def.Search.Fields["title"]),
                Size = ParseSize(_selectorEngine.SelectField(row, def.Search.Fields["size"])),
                Seeders = ParseInt(_selectorEngine.SelectField(row, def.Search.Fields["seeders"])),
                Leechers = ParseInt(_selectorEngine.SelectField(row, def.Search.Fields["leechers"])),
                MagnetLink = _selectorEngine.SelectField(row, def.Search.Fields["magnet"]),
                InfoHash = _selectorEngine.SelectField(row, def.Search.Fields["infohash"])
            });
        }
        return results;
    }
}
```

#### 3. DefinitionUpdateService

**File**: `src/TunnelFin/Indexers/Cardigann/DefinitionUpdateService.cs`

```csharp
public class DefinitionUpdateService
{
    private const string ProwlarrCdnUrl = "https://indexers.prowlarr.com/master/11/package.zip";
    private readonly HttpClient _httpClient;
    private readonly string _definitionsPath;
    private readonly ILogger<DefinitionUpdateService> _logger;

    public async Task<bool> CheckAndUpdateAsync(CancellationToken ct = default)
    {
        var currentVersion = await GetCurrentVersionAsync();
        var remoteVersion = await GetRemoteVersionAsync(ct);

        if (remoteVersion <= currentVersion)
        {
            _logger.LogInformation("Definitions up to date (v{Version})", currentVersion);
            return false;
        }

        _logger.LogInformation("Updating definitions from v{Current} to v{Remote}",
            currentVersion, remoteVersion);

        await DownloadAndExtractAsync(ct);
        return true;
    }

    private async Task DownloadAndExtractAsync(CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(ProwlarrCdnUrl, ct);
        response.EnsureSuccessStatusCode();

        var tempPath = Path.Combine(Path.GetTempPath(), "prowlarr-definitions.zip");
        await using var fileStream = File.Create(tempPath);
        await response.Content.CopyToAsync(fileStream, ct);
        fileStream.Close();

        // Extract to definitions folder
        var extractPath = Path.Combine(_definitionsPath, "Official");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, recursive: true);

        ZipFile.ExtractToDirectory(tempPath, extractPath);
        File.Delete(tempPath);
    }
}
```

#### 4. SelectorEngine

**File**: `src/TunnelFin/Indexers/Cardigann/SelectorEngine.cs`

```csharp
public class SelectorEngine
{
    public IEnumerable<IElement> SelectRows(string html, RowsBlock rows)
    {
        var document = new HtmlParser().ParseDocument(html);
        return document.QuerySelectorAll(rows.Selector);
    }

    public string SelectField(IElement row, FieldSelector selector)
    {
        var element = selector.Selector != null
            ? row.QuerySelector(selector.Selector)
            : row;

        var value = selector.Attribute != null
            ? element?.GetAttribute(selector.Attribute)
            : element?.TextContent;

        return ApplyFilters(value ?? "", selector.Filters);
    }

    private string ApplyFilters(string value, List<FilterDef>? filters)
    {
        if (filters == null) return value;

        foreach (var filter in filters)
        {
            value = filter.Name switch
            {
                "trim" => value.Trim(),
                "prepend" => filter.Args?[0] + value,
                "append" => value + filter.Args?[0],
                "replace" => value.Replace(filter.Args?[0] ?? "", filter.Args?[1] ?? ""),
                "split" => value.Split(filter.Args?[0] ?? " ")[int.Parse(filter.Args?[1] ?? "0")],
                "regexp" => Regex.Match(value, filter.Args?[0] ?? "").Groups[1].Value,
                "querystring" => ExtractQueryParam(value, filter.Args?[0] ?? ""),
                "dateparse" => ParseDate(value, filter.Args?[0]),
                "timeago" => ParseTimeAgo(value),
                _ => value
            };
        }
        return value;
    }
}
```

#### 5. InternalTorznabController

**File**: `src/TunnelFin/Indexers/Cardigann/InternalTorznabController.cs`

```csharp
public class InternalTorznabController
{
    private readonly EmbeddedProwlarrService _prowlarrService;
    private readonly ILogger<InternalTorznabController> _logger;

    // Called by TorznabClient via internal HTTP
    public async Task<string> HandleRequestAsync(
        string indexerId,
        string function, // caps, search, tvsearch, movie
        Dictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        var definition = _prowlarrService.GetDefinition(indexerId);
        if (definition == null)
            return CreateErrorResponse($"Indexer '{indexerId}' not found");

        return function switch
        {
            "caps" => CreateCapsResponse(definition),
            "search" => await CreateSearchResponseAsync(definition, parameters, ct),
            "tvsearch" => await CreateTvSearchResponseAsync(definition, parameters, ct),
            "movie" => await CreateMovieSearchResponseAsync(definition, parameters, ct),
            _ => CreateErrorResponse($"Unknown function '{function}'")
        };
    }

    private async Task<string> CreateSearchResponseAsync(
        CardigannDefinition definition,
        Dictionary<string, string> parameters,
        CancellationToken ct)
    {
        var query = parameters.GetValueOrDefault("q", "");
        var results = await _prowlarrService.SearchAsync(definition.Id, query, ct);

        return ToTorznabXml(results);
    }

    private string ToTorznabXml(List<SearchResult> results)
    {
        var ns = XNamespace.Get("http://torznab.com/schemas/2015/feed");
        var doc = new XDocument(
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    results.Select(r => new XElement("item",
                        new XElement("title", r.Title),
                        new XElement("link", r.MagnetLink),
                        new XElement(ns + "attr", new XAttribute("name", "seeders"), new XAttribute("value", r.Seeders)),
                        new XElement(ns + "attr", new XAttribute("name", "peers"), new XAttribute("value", r.Leechers)),
                        new XElement(ns + "attr", new XAttribute("name", "size"), new XAttribute("value", r.Size)),
                        new XElement(ns + "attr", new XAttribute("name", "infohash"), new XAttribute("value", r.InfoHash ?? ""))
                    ))
                )
            )
        );
        return doc.ToString();
    }
}
```

### File Structure

```
src/TunnelFin/
├── Indexers/
│   ├── Cardigann/
│   │   ├── Models/
│   │   │   ├── CardigannDefinition.cs
│   │   │   ├── CapabilitiesBlock.cs
│   │   │   ├── LoginBlock.cs
│   │   │   ├── SearchBlock.cs
│   │   │   ├── DownloadBlock.cs
│   │   │   ├── FieldSelector.cs
│   │   │   └── FilterDef.cs
│   │   ├── CardigannEngine.cs
│   │   ├── SelectorEngine.cs
│   │   ├── DefinitionManager.cs
│   │   ├── DefinitionUpdateService.cs
│   │   ├── YamlDefinitionParser.cs
│   │   ├── InternalTorznabController.cs
│   │   └── IndexerCredentialStore.cs
│   └── EmbeddedProwlarrService.cs
└── Configuration/
    └── IndexerConfiguration.cs

tests/TunnelFin.Tests/
├── Indexers/
│   ├── Cardigann/
│   │   ├── YamlDefinitionParserTests.cs
│   │   ├── SelectorEngineTests.cs
│   │   ├── CardigannEngineTests.cs
│   │   └── DefinitionUpdateServiceTests.cs
│   └── InternalTorznabControllerTests.cs
```

### Implementation Phases

**Phase 1: Cardigann Models & YAML Parser (Week 1)**
1. Port CardigannDefinition models from Prowlarr
2. Implement YamlDefinitionParser using YamlDotNet
3. Write unit tests for parsing all Cardigann v11 features
4. Verify all 522+ definitions parse without errors

**Phase 2: Selector Engine (Week 1-2)**
1. Implement SelectorEngine with AngleSharp
2. Support all Cardigann filter types
3. Unit tests for each filter type
4. Integration tests with real HTML samples

**Phase 3: Cardigann Engine (Week 2)**
1. Implement CardigannEngine for search execution
2. Support login methods (post, form, cookie)
3. Handle session management and cookies
4. Rate limiting and exponential backoff

**Phase 4: Definition Management (Week 3)**
1. Implement DefinitionUpdateService
2. Version tracking and caching
3. Custom definition loading
4. Background-safe startup check

**Phase 5: Internal Torznab API (Week 3)**
1. Create InternalTorznabController
2. Wire TorznabClient to internal endpoint
3. Loopback security enforcement
4. Integration tests with existing TorznabClient

**Phase 6: Configuration UI (Week 4)**
1. Indexer list view in settings
2. Credential entry forms (from YAML settings)
3. Health status indicators
4. Manual definition refresh button

### Dependencies

**New NuGet Packages**:
- `YamlDotNet` 16.x - YAML parsing for Cardigann definitions
- `AngleSharp` 1.x - HTML parsing and CSS selector engine

**Existing Dependencies** (no changes):
- `Microsoft.Extensions.Http` 10.0.1 - HTTP client factory
- `Jellyfin.Controller` 10.11.5 - Plugin API
- `Jellyfin.Model` 10.11.5 - Data models

## Constitution Compliance

- **I. Privacy-First**: No telemetry sent to Prowlarr servers; definition sync is one-way download only. Credentials stored encrypted.
- **II. Seamless Integration**: Indexers appear in existing TunnelFin UI; no separate configuration app needed.
- **III. Test-First**: Unit tests for YAML parsing (all Cardigann features), integration tests for live searches. 80%+ coverage target.
- **IV. Decentralized**: No central service required; works offline with cached definitions. Self-contained within Jellyfin.
- **V. User Empowerment**: Users choose which indexers to enable, control refresh intervals, add custom definitions.

