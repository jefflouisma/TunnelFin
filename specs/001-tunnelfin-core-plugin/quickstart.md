# TunnelFin Core Plugin - Developer Quickstart

**Feature**: 001-tunnelfin-core-plugin  
**Date**: January 1, 2026  
**Purpose**: Get developers up and running with TunnelFin development environment

## Prerequisites

- **.NET SDK 10.0** or later
- **Rancher Desktop** or **Docker Desktop** (for Jellyfin integration testing)
- **kubectl** (for Kubernetes cluster management)
- **Git** (for version control and reference repositories)

## Initial Setup

### 1. Clone Repository

```bash
git clone https://github.com/jefflouisma/TunnelFin.git
cd TunnelFin
```

### 2. Verify .NET SDK

```bash
dotnet --version
# Should output: 10.0.x or later
```

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Build Solution

```bash
dotnet build
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Running Tests

### Unit Tests (Fast - Run Frequently)

```bash
dotnet test tests/TunnelFin.Tests
```

Expected output:
```
Passed!  - Failed:     0, Passed:    XX, Skipped:     0, Total:    XX
```

### Integration Tests (Slower - Requires Jellyfin)

**Prerequisites**: Jellyfin instance running in Kubernetes

```bash
# Check Jellyfin is running
kubectl -n jellyfin get pods

# Run integration tests
dotnet test tests/TunnelFin.Integration
```

### Code Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Target: **80%+ coverage** (Constitution requirement)

## Development Workflow

### 1. Test-First Development (TDD)

**ALWAYS follow this workflow** (Constitution: Test-First Development):

```bash
# 1. Write failing test
# Edit: tests/TunnelFin.Tests/[Component]/[Feature]Tests.cs

# 2. Verify test fails (RED)
dotnet test tests/TunnelFin.Tests

# 3. Implement minimum code to pass (GREEN)
# Edit: src/TunnelFin/[Component]/[Feature].cs

# 4. Verify test passes
dotnet test tests/TunnelFin.Tests

# 5. Refactor while maintaining green tests
# Edit: src/TunnelFin/[Component]/[Feature].cs

# 6. Verify tests still pass
dotnet test tests/TunnelFin.Tests
```

### 2. Running Jellyfin Locally

**Start Jellyfin** (if not already running):

```bash
# Deploy Jellyfin to Kubernetes
kubectl apply -f k8s/jellyfin-deployment.yaml

# Check status
kubectl -n jellyfin get pods,svc

# Get external IP
kubectl -n jellyfin get svc jellyfin
```

**Access Jellyfin UI**:
```
http://192.168.64.6:8096
```

### 3. Deploy Plugin to Jellyfin

```bash
# Build plugin in Release mode
dotnet build src/TunnelFin -c Release

# Copy plugin DLL to Jellyfin pod
kubectl -n jellyfin cp \
  src/TunnelFin/bin/Release/net10.0/TunnelFin.dll \
  jellyfin-xxxxxxxxx-xxxxx:/config/plugins/TunnelFin.dll

# Restart Jellyfin to load plugin
kubectl -n jellyfin rollout restart deployment jellyfin
```

**Verify plugin loaded**:
1. Navigate to Jellyfin UI: http://192.168.64.6:8096
2. Go to Dashboard → Plugins
3. Confirm "TunnelFin" appears in plugin list

## Project Structure Overview

```
TunnelFin/
├── src/TunnelFin/              # Main plugin source
│   ├── Core/                   # Plugin entry point & Jellyfin integration
│   ├── Networking/             # IPv8 protocol & Tribler network
│   ├── BitTorrent/             # MonoTorrent integration
│   ├── Streaming/              # HTTP streaming endpoints
│   ├── Indexers/               # Torrent indexer implementations
│   ├── Discovery/              # Search, filter, sort, deduplication
│   ├── Models/                 # Data models & DTOs
│   ├── Configuration/          # Plugin settings
│   └── Jellyfin/               # Search/channel providers
├── tests/
│   ├── TunnelFin.Tests/        # Unit tests (90%)
│   └── TunnelFin.Integration/  # Integration tests (10%)
├── specs/001-tunnelfin-core-plugin/
│   ├── spec.md                 # Feature specification
│   ├── plan.md                 # Implementation plan
│   ├── research.md             # Technical research
│   ├── data-model.md           # Entity definitions
│   ├── contracts/              # API contracts (OpenAPI)
│   └── quickstart.md           # This file
└── reference_repos/            # Reference implementations
    ├── tribler/                # IPv8 protocol reference (Python)
    ├── monotorrent/            # BitTorrent library source (C#)
    ├── TorrServer/             # Streaming patterns (Go)
    ├── AIOStreams/             # Filtering/aggregation (TypeScript)
    └── Gelato/                 # Jellyfin plugin patterns (C#)
```

## Key Reference Files

When implementing features, consult these reference files:

| Component | Reference File | Purpose |
|-----------|---------------|---------|
| IPv8 Protocol | `reference_repos/tribler/src/tribler/core/tunnel/community.py` | Circuit creation, onion routing |
| SOCKS5 Proxy | `reference_repos/tribler/src/tribler/core/socks5/` | Anonymous connection proxy |
| Streaming | `reference_repos/monotorrent/src/MonoTorrent.Client/MonoTorrent.Streaming/` | Sequential piece download |
| Plugin Structure | `reference_repos/Gelato/Plugin.cs` | Jellyfin plugin lifecycle |
| Filtering | `reference_repos/AIOStreams/packages/` | Filter/sort engine patterns |

## Common Development Tasks

### Add New Functional Requirement

1. Update `specs/001-tunnelfin-core-plugin/spec.md` with new FR-XXX
2. Write failing test in `tests/TunnelFin.Tests/`
3. Implement feature in `src/TunnelFin/`
4. Verify test passes
5. Update `data-model.md` if entities changed
6. Update API contracts if endpoints changed

### Debug Plugin in Jellyfin

```bash
# View Jellyfin logs
kubectl -n jellyfin logs -f deployment/jellyfin

# Exec into Jellyfin pod
kubectl -n jellyfin exec -it jellyfin-xxxxxxxxx-xxxxx -- /bin/bash

# Check plugin directory
ls -la /config/plugins/
```

### Run Specific Test

```bash
# Run single test class
dotnet test --filter "FullyQualifiedName~FilterEngineTests"

# Run single test method
dotnet test --filter "FullyQualifiedName~FilterEngineTests.ApplyRequiredFilters_Should_ExcludeNonMatchingResults"
```

## Constitution Compliance Checklist

Before committing code, verify:

- [ ] **Privacy-First**: Anonymous by default, explicit consent for non-anonymous
- [ ] **Seamless Integration**: Native Jellyfin UX, no external dependencies
- [ ] **Test-First**: Tests written before implementation, 80%+ coverage
- [ ] **Decentralized**: Wire-compatible protocols, no centralized services
- [ ] **User Empowerment**: Transparent controls, configurable settings

## Deployment

### Building the Plugin

```bash
# Build release version
dotnet build --configuration Release

# Output location
ls -lh src/TunnelFin/bin/Release/net10.0/
```

### Creating Plugin Package

```bash
# Create plugin directory structure
mkdir -p TunnelFin/
cp src/TunnelFin/bin/Release/net10.0/TunnelFin.dll TunnelFin/
cp src/TunnelFin/bin/Release/net10.0/TunnelFin.pdb TunnelFin/
cp -r src/TunnelFin/bin/Release/net10.0/*.dll TunnelFin/

# Create zip package
cd TunnelFin/
zip -r ../tunnelfin_1.0.0.0.zip .
cd ..

# Verify package
unzip -l tunnelfin_1.0.0.0.zip
```

### Installing in Jellyfin

#### Option 1: Manual Installation

1. Copy `tunnelfin_1.0.0.0.zip` to Jellyfin plugins directory:
   ```bash
   # Linux/macOS
   cp tunnelfin_1.0.0.0.zip ~/.config/jellyfin/plugins/

   # Windows
   copy tunnelfin_1.0.0.0.zip %APPDATA%\jellyfin\plugins\
   ```

2. Extract the plugin:
   ```bash
   cd ~/.config/jellyfin/plugins/
   unzip tunnelfin_1.0.0.0.zip -d TunnelFin/
   ```

3. Restart Jellyfin server

4. Navigate to **Dashboard → Plugins** to verify installation

#### Option 2: Plugin Repository (Recommended)

1. Add TunnelFin repository to Jellyfin:
   - Navigate to **Dashboard → Plugins → Repositories**
   - Click **Add Repository**
   - **Repository Name**: TunnelFin
   - **Repository URL**: `https://raw.githubusercontent.com/jefflouisma/TunnelFin/main/manifest.json`
   - Click **Save**

2. Install from catalog:
   - Navigate to **Dashboard → Plugins → Catalog**
   - Find **TunnelFin** in the list
   - Click **Install**
   - Restart Jellyfin when prompted

### Configuration

After installation, configure TunnelFin:

1. Navigate to **Dashboard → Plugins → TunnelFin**
2. Configure anonymity settings:
   - **Default Hop Count**: 3 (recommended for privacy)
   - **Enable Bandwidth Contribution**: Yes (proportional relay)
   - **Allow Non-Anonymous Fallback**: No (privacy-first)
3. Configure indexers (add at least one):
   - Built-in: 1337x, RARBG, Nyaa
   - Torznab: Add custom indexers
4. Configure filters (optional):
   - Required: Minimum quality, language
   - Excluded: Unwanted formats, codecs
5. Click **Save**

### Verification

Verify the plugin is working:

```bash
# Check Jellyfin logs
tail -f ~/.config/jellyfin/log/log_*.txt | grep TunnelFin

# Expected output:
# [INF] TunnelFin plugin loaded successfully
# [INF] IPv8 network initialized
# [INF] Circuit manager started
```

## Next Steps

1. Review [spec.md](./spec.md) for feature requirements
2. Review [plan.md](./plan.md) for implementation strategy
3. Review [research.md](./research.md) for technical decisions
4. Review [data-model.md](./data-model.md) for entity definitions
5. Review [contracts/](./contracts/) for API specifications
6. Start implementing with TDD workflow

## Getting Help

- **Constitution**: `.specify/memory/constitution.md` - Core principles and governance
- **PRD**: `PRD.md` - Product requirements and architecture
- **AGENTS.md**: AI agent development instructions
- **Reference Repos**: `reference_repos/` - Implementation patterns

---

**Remember**: Test-first development is NON-NEGOTIABLE. Write tests, see them fail, make them pass, refactor. This is the TunnelFin way.

