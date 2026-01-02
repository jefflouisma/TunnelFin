# TunnelFin

The one-stop-shop Jellyfin streaming solution with anonymous BitTorrent streaming via Tribler's IPv8 network.

## Overview

TunnelFin is a .NET 10.0 Jellyfin plugin that enables anonymous torrent streaming through Tribler's IPv8 anonymity network, with content discovery via torrent indexers.

## Quick Start

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

```
TunnelFin/
├── src/
│   └── TunnelFin/                    # Main plugin project
│       ├── Core/                     # Core plugin functionality
│       ├── Networking/               # IPv8 networking layer (Tribler compatibility)
│       ├── BitTorrent/               # MonoTorrent integration & streaming
│       ├── Indexers/                 # Torrent indexer management
│       ├── Streaming/                # HTTP stream management
│       ├── Configuration/            # Plugin configuration
│       └── Models/                   # Data models
│
├── tests/
│   ├── TunnelFin.Tests/              # Unit tests (xUnit + Moq)
│   │   ├── Core/                     # Core functionality tests
│   │   ├── Networking/               # IPv8 networking tests
│   │   ├── BitTorrent/               # Torrent engine tests
│   │   ├── Indexers/                 # Indexer management tests
│   │   ├── Streaming/                # Stream management tests
│   │   └── Fixtures/                 # Test fixtures and helpers
│   │
│   └── TunnelFin.Integration/        # Integration tests (Docker-based)
│       ├── Docker/                   # Docker container tests
│       └── Helpers/                  # Integration test helpers
│
├── reference_repos/                  # Reference implementations
│   ├── AIOStreams/                   # Content aggregation reference
│   ├── Gelato/                       # Jellyfin integration
│   ├── TorrServer/                   # Torrent streaming reference
│   ├── monotorrent/                  # BitTorrent library
│   └── tribler/                      # Anonymity layer reference
│
├── TunnelFin.sln                     # Solution file
├── Directory.Build.props             # Common build properties
├── global.json                       # .NET SDK version
├── .gitignore                        # Git ignore rules
├── LICENSE                           # GPL-3.0 license
└── PRD.md                            # Product Requirements Document
```

## Dependencies

### Main Project (TunnelFin)
- **Jellyfin.Controller** (10.9.0) - Jellyfin plugin API
- **MonoTorrent** (3.0.2) - BitTorrent protocol implementation
- **NSec.Cryptography** (25.4.0) - Ed25519 cryptography for IPv8
- **Microsoft.Extensions.Http** (10.0.1) - HTTP client factory

### Unit Tests (TunnelFin.Tests)
- **xUnit** (2.6.6) - Test framework
- **Moq** (4.20.72) - Mocking framework
- **FluentAssertions** (8.8.0) - Assertion library

### Integration Tests (TunnelFin.Integration)
- **xUnit** (2.6.6) - Test framework
- **Testcontainers** (4.10.0) - Docker container management
- **FluentAssertions** (8.8.0) - Assertion library

## Development Workflow

### Build
```bash
dotnet build
```

### Run Tests
```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/TunnelFin.Tests

# Integration tests only (requires Docker/Rancher Desktop)
dotnet test tests/TunnelFin.Integration

# Run specific test class
dotnet test --filter "FullyQualifiedName~FilterEngineTests"
```

### Clean & Restore
```bash
dotnet clean
dotnet restore
```

## Testing

### Unit Tests (90% of tests)
- **Focus**: Business logic, algorithms, data transformations
- **Tools**: xUnit, Moq, FluentAssertions
- **Pattern**: "Humble Object" - keep Jellyfin dependencies minimal
- **Target**: 80% code coverage

### Integration Tests (10% of tests)
- **Focus**: Plugin loading, Jellyfin API integration
- **Tools**: xUnit, Testcontainers, FluentAssertions
- **Pattern**: Black-box testing with Docker containers
- **Requirement**: Docker/Rancher Desktop running

### Code Coverage
```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Install report generator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open report
open coveragereport/index.html
```

### Test Naming Conventions
- **Unit Tests**: `MethodName_Should_ExpectedBehavior_When_Condition`
- **Integration Tests**: `Feature_Should_ExpectedBehavior`

### Priority Test Areas (Based on PRD)
1. **Filtering Engine** - Required/Preferred/Excluded/Include rules, regex matching
2. **Sorting Engine** - Multi-criteria sorting, custom sort orders
3. **Deduplication** - By infohash, filename, smart detection hash
4. **IPv8 Cryptography** - Ed25519 keypair generation, onion routing
5. **Privacy Fallback** - Tribler-first logic, user consent flow

## Resources

- **PRD**: See `PRD.md` for detailed requirements
- **License**: GPL-3.0 (see `LICENSE`)
- **Repository**: https://github.com/jefflouisma/TunnelFin
