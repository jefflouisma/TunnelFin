# TunnelFin Search UX Implementation Plan

## Executive Summary

This document outlines the implementation strategy for TunnelFin's search user experience within Jellyfin. The goal is to enable users to search for torrent content intuitively while displaying network availability (anonymous vs direct) clearly.

## Current State

### What Works Today
- **API Search**: `/TunnelFin/Search?query=...` endpoint returns JSON results
- **Category Navigation**: Channel root shows predefined categories (Movies, TV, Anime, Test content)
- **Folder-Based Search**: `FolderId=search:term` triggers searches from channel navigation
- **Network Status**: Channel description shows 🟢/🟠 indicator for circuit availability
- **Per-Item Warnings**: Zero-seeder warning displayed in item Overview

### Limitations
1. **Jellyfin's `InternalChannelItemQuery` lacks `SearchTerm`** - only supports `FolderId` navigation
2. **Jellyfin's global search (`/Search/Hints`)** queries library database, not channels
3. **No search input box** in standard Jellyfin channel UI
4. **Network status only in channel description** - not per-item visibility

## Implementation Approach

### Phase 1: Enhanced Folder-Based Search (Immediate)

**Goal**: Enable search via URL-encoded folder navigation

#### Changes Required

1. **Add "🔍 Search..." Category** to `TunnelFinChannel.cs`:
```csharp
// Add to SearchCategories array
("search_help", "🔍 Search for Content...", "", "Enter your search in the URL: ?folderId=search:your+query")
```

2. **Handle Empty Search Folder**:
- When user clicks "🔍 Search...", return help text as a single item
- Explain URL format: `/Channels/{id}/Items?folderId=search:movie+name`

3. **Enhance Search Results with Network Status**:
```csharp
// In ToChannelItem(), add to Overview:
var networkStatus = _isNetworkAvailable ? "🟢 Anonymous" : "🟠 Direct";
overview = $"{networkStatus} | Size: {FormatBytes(result.Size)} | Seeders: {result.Seeders}";
```

#### E2E Test
```csharp
[Fact]
public async Task Search_ViaFolderId_ReturnsResults()
{
    // GET /Channels/{channelId}/Items?folderId=search:big+buck+bunny
    var result = await _client.GetChannelItems(channelId, "search:big buck bunny");
    Assert.NotEmpty(result.Items);
    Assert.Contains("🟢", result.Items[0].Overview); // or 🟠
}
```

---

### Phase 2: Plugin Configuration Search Page (Short-term)

**Goal**: Add a searchable web page accessible from Jellyfin Dashboard

#### Implementation

1. **Create `Configuration/searchPage.html`**:
```html
<div id="tunnelfin-search">
    <input type="text" id="search-input" placeholder="Search torrents...">
    <button id="search-btn">Search</button>
    <div id="results"></div>
</div>
<script>
    document.getElementById('search-btn').onclick = async () => {
        const query = document.getElementById('search-input').value;
        const res = await ApiClient.fetch('/TunnelFin/Search?query=' + encodeURIComponent(query));
        // Render results with play buttons
    };
</script>
```

2. **Register Page in Plugin.cs**:
```csharp
public IEnumerable<PluginPageInfo> GetPages() => new[]
{
    new PluginPageInfo { Name = "TunnelFin", EmbeddedResourcePath = "TunnelFin.Configuration.configPage.html" },
    new PluginPageInfo { Name = "Search", EmbeddedResourcePath = "TunnelFin.Configuration.searchPage.html" }
};
```

3. **Access**: Dashboard → Plugins → TunnelFin → Search tab

#### E2E Test
```csharp
[Fact]
public async Task SearchPage_Renders_AndReturnsResults()
{
    // Load page
    await page.GotoAsync("/web/index.html#!/configurationpage?name=Search");
    await page.FillAsync("#search-input", "sintel");
    await page.ClickAsync("#search-btn");
    await page.WaitForSelectorAsync(".result-card");
    var count = await page.Locator(".result-card").CountAsync();
    Assert.True(count > 0);
}
```

---

### Phase 3: Standalone Search Web UI (Medium-term)

**Goal**: Create a dedicated `/TunnelFin/` page with full search functionality

#### Implementation

1. **Add HTML Endpoint to `TunnelFinApiController.cs`**:
```csharp
[HttpGet]
[AllowAnonymous]
[Produces("text/html")]
public IActionResult Index()
{
    return Content(GetSearchPageHtml(), "text/html");
}
```

2. **HTML Template Features**:
- Search input with autocomplete
- Results grid with poster images (from TMDB)
- Network status badge per result (🟢/🟠)
- Seeder count with color coding (green >10, yellow 1-10, red 0)
- Direct play button → Jellyfin playback URL
- Filter dropdowns (quality, category)

3. **Play Integration**:
```javascript
// Generate Jellyfin play URL
const playUrl = `/web/index.html#!/details?id=${channelItemId}&serverId=${serverId}`;
window.location = playUrl;
```

#### E2E Test
```csharp
[Fact]
public async Task StandaloneSearchPage_PlaysContent()
{
    await page.GotoAsync("/TunnelFin/");
    await page.FillAsync("#query", "big buck bunny");
    await page.ClickAsync("#search");
    await page.WaitForSelectorAsync("[data-testid='result']");
    
    // Verify network status displayed
    var badge = await page.Locator(".network-badge").First.TextContentAsync();
    Assert.Contains("🟢", badge); // or 🟠
    
    // Click play
    await page.ClickAsync("[data-testid='play-btn']");
    await page.WaitForURLAsync(new Regex("/details"));
}
```

---

### Phase 4: Rich Metadata Integration (Parallel)

**Goal**: Enhance search results with TMDB posters and full metadata

#### Implementation

1. **Add TMDB Client** (`Services/TmdbClient.cs`):
```csharp
public async Task<MovieMetadata?> SearchMovieAsync(string title, int? year)
{
    // Returns poster URL, overview, rating, IMDB/TMDB IDs
}
```

2. **Enrich TorrentResult in IndexerManager**:
```csharp
// After search, attempt TMDB lookup for each result
foreach (var result in results)
{
    var (title, year) = ParseTitleAndYear(result.Title);
    var metadata = await _tmdbClient.SearchMovieAsync(title, year);
    if (metadata != null)
    {
        result.PosterUrl = metadata.PosterUrl;
        result.TmdbId = metadata.Id;
        result.Overview = metadata.Overview;
    }
}
```

3. **Update ChannelItemInfo**:
```csharp
return new ChannelItemInfo
{
    ImageUrl = result.PosterUrl, // TMDB poster
    Overview = $"{result.Overview}\n\n{networkStatus} | {FormatBytes(result.Size)}",
    ProviderIds = new Dictionary<string, string>
    {
        ["Tmdb"] = result.TmdbId?.ToString(),
        ["Imdb"] = result.ImdbId
    }
};
```

#### E2E Test
```csharp
[Fact]
public async Task SearchResults_HaveTmdbPosters()
{
    var results = await _client.GetChannelItems(channelId, "search:inception");
    var item = results.Items.First();
    Assert.NotNull(item.ImageUrl);
    Assert.StartsWith("https://image.tmdb.org", item.ImageUrl);
}
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          USER ENTRY POINTS                               │
├─────────────────────────────────────────────────────────────────────────┤
│  1. Channel UI         2. Plugin Config Page    3. Standalone Page      │
│  Click category        Search input box         /TunnelFin/             │
│  or use URL param      in Dashboard             Full web UI             │
└───────────┬───────────────────┬─────────────────────────┬───────────────┘
            │                   │                         │
            ▼                   ▼                         ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                         SEARCH FLOW                                        │
│                                                                            │
│   InternalChannelItemQuery    API Call                 API Call           │
│   FolderId="search:term"      /TunnelFin/Search        /TunnelFin/Search  │
│            │                       │                        │             │
│            └───────────────────────┴────────────────────────┘             │
│                                    │                                       │
│                                    ▼                                       │
│                          ┌─────────────────┐                              │
│                          │ IndexerManager  │                              │
│                          │ .SearchAsync()  │                              │
│                          └────────┬────────┘                              │
│                                   │                                        │
│         ┌─────────────────────────┼─────────────────────────┐             │
│         ▼                         ▼                         ▼             │
│  ┌────────────┐           ┌─────────────┐          ┌─────────────┐       │
│  │  Prowlarr  │           │ Built-in    │          │  Torznab    │       │
│  │  (if on)   │           │ Scrapers    │          │  Clients    │       │
│  └─────┬──────┘           └──────┬──────┘          └──────┬──────┘       │
│        │                         │                        │               │
│        └─────────────────────────┴────────────────────────┘               │
│                                  │                                         │
│                                  ▼                                         │
│                    ┌─────────────────────────┐                            │
│                    │  Merge & Deduplicate    │                            │
│                    │  Sort by Seeders        │                            │
│                    └───────────┬─────────────┘                            │
│                                │                                           │
│                                ▼                                           │
│                    ┌─────────────────────────┐                            │
│                    │  TMDB Metadata Lookup   │ (Phase 4)                  │
│                    │  Add posters, IDs       │                            │
│                    └───────────┬─────────────┘                            │
│                                │                                           │
└────────────────────────────────┼───────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         RESULT PRESENTATION                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│   ┌─────────────────────────────────────────────────────────────────┐   │
│   │  🎬 Big Buck Bunny (2008)                              🟢      │   │
│   │  ──────────────────────────────────────────────────────────────  │   │
│   │  [POSTER]   Size: 1.5 GB | Seeders: 245 | Leechers: 12         │   │
│   │             Resolution: 1080p | Codec: x265                      │   │
│   │             Source: 1337x                                        │   │
│   │                                              [▶ PLAY]            │   │
│   └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│   Network Status Legend:                                                 │
│   🟢 = Anonymous (routed through Tribler circuits)                      │
│   🟠 = Direct (IP exposed - user warned before playback)                │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `src/TunnelFin/Jellyfin/TunnelFinChannel.cs` | Modify | Add search help category, enhance Overview with network status |
| `src/TunnelFin/Configuration/searchPage.html` | Create | Plugin config search page |
| `src/TunnelFin/Core/Plugin.cs` | Modify | Register search page |
| `src/TunnelFin/Api/TunnelFinApiController.cs` | Modify | Add HTML index endpoint |
| `src/TunnelFin/Services/TmdbClient.cs` | Create | TMDB metadata lookup |
| `src/TunnelFin/Models/TorrentResult.cs` | Modify | Add PosterUrl, TmdbId fields |
| `tests/TunnelFin.Tests/Integration/SearchE2ETests.cs` | Create | E2E tests for all phases |

---

## Testing Strategy

### Unit Tests
- `TmdbClient.ParseTitleAndYear()` - title parsing
- `TunnelFinChannel.ToChannelItem()` - network status in Overview
- `IndexerManager.SearchAsync()` - deduplication, sorting

### Integration Tests
- API search returns valid JSON with all fields
- Channel navigation with `search:term` returns results
- TMDB enrichment adds poster URLs

### E2E Tests (Browser Automation)
- Plugin config search page renders and searches
- Standalone page full flow: search → results → play
- Network status badge correctly reflects circuit availability

---

## Timeline

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| Phase 1 | 2 days | Folder-based search, network status per item |
| Phase 2 | 3 days | Plugin config search page |
| Phase 3 | 4 days | Standalone search web UI |
| Phase 4 | 3 days | TMDB metadata integration |

**Total: ~12 days**

---

## Open Questions

1. **TMDB API Key**: Should we embed a default key or require users to provide their own?
2. **Caching**: How long should TMDB metadata be cached?
3. **Anonymous-Only Mode**: Should there be an option to hide results that can't be anonymized?
4. **Mobile**: Does the standalone page need responsive design for mobile Jellyfin apps?

