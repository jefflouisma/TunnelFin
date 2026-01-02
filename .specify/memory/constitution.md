<!--
SYNC IMPACT REPORT
==================
Version Change: INITIAL → 1.0.0
Modified Principles: N/A (initial constitution)
Added Sections:
  - Core Principles (5 principles)
  - Technology Stack & Architecture
  - Development Workflow
  - Governance
Removed Sections: N/A
Templates Requiring Updates:
  ✅ plan-template.md - Constitution Check section aligned
  ✅ spec-template.md - Requirements align with privacy-first and testing principles
  ✅ tasks-template.md - Task categorization reflects test-first and phased approach
  ✅ agent-file-template.md - No agent-specific references found
Follow-up TODOs: None
-->

# TunnelFin Constitution

## Core Principles

### I. Privacy-First (NON-NEGOTIABLE)

User anonymity and privacy MUST be the default state for all operations. The plugin MUST:
- Route all BitTorrent traffic through the Tribler anonymity network by default
- Never transmit traffic over standard BitTorrent without explicit, per-session user consent
- Provide clear visual indicators (color-coded UI) distinguishing anonymous vs. non-anonymous streams
- Store minimal personally identifiable information in logs (default: no content titles or IP addresses)
- Securely encrypt and store cryptographic identities within Jellyfin's configuration

**Rationale**: Privacy is the foundational value proposition of TunnelFin. Compromising on
privacy defaults would undermine user trust and the project's core mission. Users who want
convenience over privacy can opt-in, but the default MUST protect users who may not fully
understand the risks.

### II. Seamless Integration

All features MUST integrate natively with Jellyfin's existing user experience. The plugin MUST:
- Present torrent content as native Jellyfin library items with complete metadata
- Register as a Jellyfin search provider for unified content discovery
- Expose streams via standard HTTP endpoints consumable by Jellyfin's player
- Follow Jellyfin's plugin API patterns and dependency injection conventions
- Avoid requiring external services, Docker containers, or separate applications

**Rationale**: Users choose Jellyfin for its unified media experience. Requiring external tools
or presenting a disjointed interface creates friction and reduces adoption. Deep integration
ensures TunnelFin feels like a natural extension of Jellyfin rather than a bolt-on hack.

### III. Test-First Development (NON-NEGOTIABLE)

All new functionality MUST follow test-driven development practices. The workflow MUST be:
1. Write tests that define expected behavior
2. Verify tests fail (red state)
3. Implement minimum code to pass tests (green state)
4. Refactor while maintaining passing tests

**Testing Requirements**:
- Unit tests MUST cover 80%+ of business logic, algorithms, and data transformations
- Integration tests MUST validate plugin loading and Jellyfin API contracts
- Tests MUST use xUnit, FluentAssertions, and Moq for consistency
- Critical areas requiring tests: filtering engine, sorting engine, deduplication, IPv8
  cryptography, privacy fallback logic

**Rationale**: TunnelFin reimplements complex protocols (IPv8, BitTorrent streaming) and handles
sensitive privacy decisions. Bugs in these areas could expose user identities or cause data
corruption. TDD ensures correctness from the start and provides regression protection as the
codebase evolves.

### IV. Decentralized Architecture

The plugin MUST operate as a pure peer-to-peer solution without reliance on centralized services.
The architecture MUST:
- Implement wire-compatible IPv8 protocol to participate in the existing Tribler network
- Contribute bandwidth back to the network proportional to usage (fairness principle)
- Support multiple decentralized torrent indexers (built-in and custom Torznab endpoints)
- Avoid dependencies on premium debrid services or centralized caching layers
- Function entirely within the Jellyfin process (no external daemons or microservices)

**Rationale**: Centralized services introduce single points of failure, privacy risks (service
providers can log activity), and ongoing costs. A decentralized approach aligns with the
privacy-first principle and ensures long-term sustainability without external dependencies.

### V. User Empowerment & Transparency

Users MUST have clear visibility and control over all privacy and performance trade-offs.
The plugin MUST:
- Provide configurable anonymity settings (hop count: 1-3 hops)
- Display real-time stream health metrics (peer count, download speed, buffer status)
- Warn users explicitly before any non-anonymous operation with clear consent prompts
- Expose advanced filtering and sorting controls for content discovery
- Support content-type-specific profiles (Movies, TV Shows, Anime)
- Allow users to define custom stream display formats

**Rationale**: Privacy and performance often involve trade-offs (more hops = more privacy but
slower speeds). Users have different risk tolerances and use cases. Providing transparency and
control respects user agency and allows power users to optimize for their specific needs.

## Technology Stack & Architecture

### Mandatory Technologies

- **Language**: C# / .NET 10.0 (Jellyfin plugin compatibility requirement)
- **BitTorrent Engine**: MonoTorrent 3.0.2 (mature C# implementation with StreamProvider API)
- **Cryptography**: NSec.Cryptography 25.4.0 (Ed25519 for peer identity)
- **HTTP Client**: Microsoft.Extensions.Http 10.0.1 (indexer requests)
- **Plugin API**: Jellyfin.Controller 10.11.5 + Jellyfin.Model 10.11.5 (plugin integration)

### Architectural Constraints

- **Self-Contained**: All functionality MUST run within the Jellyfin process (no external services)
- **Phased Implementation**: Development MUST proceed in phases (Foundational Protocols →
  Core Streaming → Content Discovery → Advanced Features)
- **Reference-Driven**: Implementation MUST reference proven patterns from:
  - `tribler/` (IPv8 protocol, onion routing)
  - `monotorrent/` (BitTorrent streaming)
  - `TorrServer/` (sequential piece prioritization)
  - `AIOStreams/` (filtering, sorting, deduplication)
  - `Gelato/` (Jellyfin plugin patterns)

### Performance & Scale Requirements

- **Streaming**: Sequential piece download to enable immediate playback
- **Caching**: Search results cached 5-15 minutes (user-configurable)
- **Logging**: Minimal by default, verbose mode available for troubleshooting
- **Metadata**: Fetch from TMDB (movies/TV) and AniList/MyAnimeList (anime)

## Development Workflow

### Code Quality Gates

All code changes MUST pass these gates before merge:
1. **Tests Pass**: `dotnet test` succeeds with 80%+ coverage
2. **Build Succeeds**: `dotnet build` completes without errors
3. **Constitution Compliance**: Changes align with all core principles
4. **Reference Validation**: Implementation patterns match reference repositories where applicable

### Branching & Commits

- **Feature Branches**: `###-feature-name` format (e.g., `001-ipv8-protocol`)
- **Commit Frequency**: Commit after each logical task or checkpoint
- **Commit Messages**: Descriptive, referencing task IDs where applicable

### Documentation Requirements

- **PRD**: Product Requirements Document (PRD.md) defines all features and architecture
- **Specs**: Feature specifications in `/specs/[###-feature-name]/spec.md`
- **Plans**: Implementation plans in `/specs/[###-feature-name]/plan.md`
- **Tasks**: Task lists in `/specs/[###-feature-name]/tasks.md`
- **README**: High-level overview, quick start, project structure

### Testing Strategy

- **Unit Tests (90%)**: Business logic, algorithms, data transformations
  - Location: `tests/TunnelFin.Tests/`
  - Pattern: "Humble Object" - minimize Jellyfin dependencies
  - Naming: `MethodName_Should_ExpectedBehavior_When_Condition`

- **Integration Tests (10%)**: Plugin loading, Jellyfin API integration
  - Location: `tests/TunnelFin.Integration/`
  - Pattern: Black-box testing with Docker containers
  - Requirement: Docker/Rancher Desktop running
  - Naming: `Feature_Should_ExpectedBehavior`

## Governance

### Amendment Process

1. **Proposal**: Document proposed change with rationale and impact analysis
2. **Review**: Assess impact on existing features, templates, and documentation
3. **Version Bump**: Apply semantic versioning (MAJOR.MINOR.PATCH)
   - **MAJOR**: Backward-incompatible governance changes or principle removals
   - **MINOR**: New principles or materially expanded guidance
   - **PATCH**: Clarifications, wording fixes, non-semantic refinements
4. **Propagation**: Update all dependent templates and documentation
5. **Sync Report**: Document changes in constitution file header

### Compliance Verification

- All feature specifications MUST include a "Constitution Check" section
- All implementation plans MUST verify alignment with core principles
- Code reviews MUST validate adherence to privacy-first and test-first principles
- Complexity MUST be justified when violating simplicity expectations

### Runtime Guidance

- Use `AGENTS.md` for AI agent-specific development instructions
- Use `PRD.md` as the authoritative source for feature requirements
- Use `.specify/templates/` for consistent documentation structure
- Use `reference_repos/` for implementation pattern validation

**Version**: 1.0.0 | **Ratified**: 2026-01-01 | **Last Amended**: 2026-01-01
