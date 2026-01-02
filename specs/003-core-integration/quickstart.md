# Quickstart: Core Integration Layer

**Feature**: 003-core-integration  
**Date**: 2026-01-02  
**Audience**: Developers implementing or testing the core integration layer

## Overview

This quickstart guide covers building, testing, and configuring the TunnelFin core integration layer. This feature connects MonoTorrent streaming, indexer scrapers, Jellyfin plugin interfaces, and circuit-routed peer connections into a functional streaming system.

---

## Prerequisites

### Required Software

- **.NET 10.0 SDK** - Download from https://dotnet.microsoft.com/download/dotnet/10.0
- **Jellyfin 10.11.5+** - Download from https://jellyfin.org/downloads
- **Git** - For cloning the repository
- **Docker** (optional) - For integration tests with live indexers

### Required Services

- **Tribler/IPv8 network** - Must be running for circuit-routed connections (from feature 002)
  - If not available, circuit routing can be disabled (privacy warning shown to user)

### Development Environment

- **IDE**: Visual Studio 2022, VS Code with C# extension, or JetBrains Rider
- **OS**: macOS, Linux, or Windows (cross-platform .NET)

---

## Build Instructions

### 1. Clone Repository

```bash
git clone https://github.com/jefflouisma/TunnelFin.git
cd TunnelFin
git checkout 003-core-integration
```

### 2. Restore Dependencies

```bash
dotnet restore src/TunnelFin/TunnelFin.csproj
```

### 3. Build Plugin

```bash
dotnet build src/TunnelFin/TunnelFin.csproj --configuration Release
```

**Output**: `src/TunnelFin/bin/Release/net10.0/TunnelFin.dll`

### 4. Install Plugin to Jellyfin

```bash
# Copy plugin DLL to Jellyfin plugins directory
# macOS/Linux:
cp src/TunnelFin/bin/Release/net10.0/TunnelFin.dll ~/.config/jellyfin/plugins/TunnelFin/

# Windows:
copy src\TunnelFin\bin\Release\net10.0\TunnelFin.dll %APPDATA%\jellyfin\plugins\TunnelFin\

# Restart Jellyfin server
sudo systemctl restart jellyfin  # Linux
# or restart via Jellyfin dashboard
```

---

## Test Instructions

### Unit Tests

```bash
# Run all unit tests
dotnet test tests/TunnelFin.Tests/TunnelFin.Tests.csproj

# Run specific test class
dotnet test tests/TunnelFin.Tests/TunnelFin.Tests.csproj --filter "FullyQualifiedName~TorrentEngineTests"

# Run with coverage
dotnet test tests/TunnelFin.Tests/TunnelFin.Tests.csproj --collect:"XPlat Code Coverage"
```

### Integration Tests

**Note**: Integration tests require Docker for live indexer queries.

```bash
# Start Docker containers for test indexers
docker-compose -f tests/docker-compose.yml up -d

# Run integration tests
dotnet test tests/TunnelFin.Tests/TunnelFin.Tests.csproj --filter "Category=Integration"

# Stop Docker containers
docker-compose -f tests/docker-compose.yml down
```

### Test Coverage Report

```bash
# Generate HTML coverage report
dotnet test tests/TunnelFin.Tests/TunnelFin.Tests.csproj --collect:"XPlat Code Coverage"
reportgenerator -reports:"tests/TunnelFin.Tests/TestResults/*/coverage.cobertura.xml" -targetdir:"coverage" -reporttypes:Html

# Open report
open coverage/index.html  # macOS
xdg-open coverage/index.html  # Linux
start coverage/index.html  # Windows
```

---

## Configuration

### 1. Access Plugin Settings

1. Open Jellyfin web UI (http://localhost:8096)
2. Navigate to **Dashboard** → **Plugins** → **TunnelFin**
3. Click **Settings**

### 2. Configure Indexers

#### Torznab Indexer (Recommended)

1. Click **Add Indexer** → **Torznab**
2. Fill in configuration:
   - **Name**: "Jackett - 1337x" (or your indexer name)
   - **Base URL**: "http://localhost:9117/api/v2.0/indexers/1337x/results/torznab"
   - **API Key**: Your Jackett API key
   - **Rate Limit**: 1.0 requests/second (default)
   - **Categories**: 2000 (Movies), 5000 (TV)
3. Click **Test** to verify connectivity
4. Click **Save**

#### HTML Scraper (Fallback)

1. Click **Add Indexer** → **HTML Scraper**
2. Select indexer type:
   - **1337x**: https://1337x.to
   - **Nyaa**: https://nyaa.si
   - **TorrentGalaxy**: https://torrentgalaxy.to
   - **EZTV**: https://eztv.re
3. Fill in configuration:
   - **Name**: "Nyaa Direct"
   - **Base URL**: "https://nyaa.si"
   - **Rate Limit**: 1.0 requests/second
4. Click **Test** to verify connectivity
5. Click **Save**

### 3. Configure Circuit Settings

1. Navigate to **Circuit Settings** tab
2. Configure options:
   - **Enable Circuit Routing**: ✅ (default: true)
   - **Hop Count**: 3 (default, range: 1-5)
   - **Circuit Pool Size**: 100 (default)
   - **Circuit Timeout**: 10 minutes (default)
   - **Fallback to Direct**: ❌ (default: false, shows privacy warning if enabled)
3. Click **Save**

### 4. Configure Streaming Settings

1. Navigate to **Streaming Settings** tab
2. Configure options:
   - **Prebuffer Size**: 10 seconds (default)
   - **Max Concurrent Streams**: 10 (default)
   - **Idle Timeout**: 30 minutes (default)
   - **Disk Cache Size**: 5 GB (default)
3. Click **Save**

---

## First-Time Setup Workflow

### 1. Verify Tribler is Running

```bash
# Check if Tribler/IPv8 network is accessible
curl http://localhost:8085/health  # Adjust port based on your setup
```

If Tribler is not running, circuit routing will be disabled automatically (privacy warning shown).

### 2. Add Test Indexer

Use the configuration steps above to add at least one indexer (Torznab recommended).

### 3. Test Search

1. Open Jellyfin web UI
2. Navigate to **Channels** → **TunnelFin**
3. Enter search query: "Big Buck Bunny"
4. Verify results appear from configured indexers

### 4. Test Playback

1. Select a search result
2. Click **Play**
3. Verify video starts playing within 30 seconds
4. Test seeking by dragging playback position

### 5. Monitor Logs

```bash
# Tail Jellyfin logs
tail -f ~/.local/share/jellyfin/log/log_*.txt  # macOS/Linux
tail -f %PROGRAMDATA%\Jellyfin\Server\log\log_*.txt  # Windows

# Look for TunnelFin log entries
grep "TunnelFin" ~/.local/share/jellyfin/log/log_*.txt
```


