# Implementation Plan: Network Transport Layer

**Branch**: `002-network-transport` | **Date**: January 2, 2026 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-network-transport/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement UDP network transport layer enabling live IPv8 protocol communication with the Tribler anonymity network. This builds on the existing `001-tunnelfin-core-plugin` foundation (Protocol, CircuitManager, Handshake, Peer classes) by adding actual network I/O capabilities: UDP socket management, bootstrap peer discovery, live handshake execution, circuit establishment over real connections, and BitTorrent traffic routing through anonymous circuits.

## Technical Context

**Language/Version**: C# / .NET 10.0 (Jellyfin plugin requirement)
**Primary Dependencies**: MonoTorrent 3.0.2 (BitTorrent), NSec.Cryptography 25.4.0 (Ed25519), System.Net.Sockets (UDP)
**Storage**: In-memory peer table, Jellyfin config for persistent identity
**Testing**: xUnit, FluentAssertions, Moq (per constitution)
**Target Platform**: Cross-platform (Windows, Linux, macOS) via Jellyfin
**Project Type**: Single project (Jellyfin plugin)
**Performance Goals**: Bootstrap <30s, Circuit establishment <15s, 95% peer handshake success
**Constraints**: 2-3 concurrent circuits, 20+ peer table, <5% tunneled packet loss
**Scale/Scope**: Single user instance, ~50-100 active peers, 2-3 circuits

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Verify alignment with TunnelFin Constitution (`.specify/memory/constitution.md`):

- [x] **Privacy-First**: All traffic routed through circuits by default. Direct connection only with explicit per-session consent (FR-022). No IP logging in default mode (FR-024).
- [x] **Seamless Integration**: Pure .NET implementation within Jellyfin process. No external services, Docker containers, or separate applications required.
- [x] **Test-First Development**: Tests defined before implementation. 80%+ coverage planned for transport, handshake, circuit, and routing logic.
- [x] **Decentralized Architecture**: Wire-compatible IPv8 v3.x protocol (per py-ipv8 v3.1.0). Connects to existing Tribler network. No centralized services (bootstrap nodes are decentralized infrastructure).
- [x] **User Empowerment**: Configurable hop count, visible circuit status, clear consent prompts for direct fallback.

**Violations Requiring Justification**: None

## Project Structure

### Documentation (this feature)

```text
specs/002-network-transport/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (internal APIs)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/TunnelFin/
├── Networking/
│   ├── Transport/                    # NEW: UDP transport layer
│   │   ├── UdpTransport.cs          # Socket management, send/receive
│   │   ├── ITransport.cs            # Transport abstraction interface
│   │   ├── TransportMetrics.cs      # Packets sent/received counters
│   │   └── RetryPolicy.cs           # Exponential backoff logic
│   ├── Bootstrap/                    # NEW: Peer discovery
│   │   ├── BootstrapManager.cs      # Bootstrap node contact
│   │   ├── BootstrapNode.cs         # Known node addresses
│   │   └── PeerTable.cs             # Discovered peers + reliability
│   ├── IPv8/                         # EXISTING + EXTEND
│   │   ├── Protocol.cs              # EXTEND: wire network I/O
│   │   ├── Handshake.cs             # EXTEND: live handshake execution
│   │   ├── HandshakeStateMachine.cs # NEW: per-peer state tracking
│   │   ├── Peer.cs                  # EXTEND: reliability metrics
│   │   └── ProtocolVersion.cs       # NEW: v3.x version handling
│   ├── Circuits/                     # EXISTING + EXTEND
│   │   ├── CircuitManager.cs        # EXTEND: network transmission
│   │   ├── CircuitNetworkClient.cs  # NEW: CREATE/EXTEND over UDP
│   │   └── CircuitHeartbeat.cs      # NEW: 30s keepalive
│   └── Tunnel/                       # NEW: Traffic routing
│       ├── TunnelProxy.cs           # TCP-over-circuit proxy
│       ├── LayeredEncryption.cs     # Per-hop encryption
│       └── TunnelStream.cs          # MonoTorrent integration
└── Configuration/
    └── AnonymitySettings.cs         # EXTEND: transport config

tests/TunnelFin.Tests/
├── Networking/
│   ├── Transport/
│   │   └── UdpTransportTests.cs
│   ├── Bootstrap/
│   │   └── BootstrapManagerTests.cs
│   └── Tunnel/
│       └── TunnelProxyTests.cs
└── Integration/
    └── LiveNetworkTests.cs          # Real Tribler network tests
```

**Structure Decision**: Extends existing `src/TunnelFin/Networking` hierarchy. New components added under `Transport/`, `Bootstrap/`, and `Tunnel/` directories. Existing Protocol, Handshake, CircuitManager classes extended to use real network I/O.

## Complexity Tracking

> No Constitution violations - table not applicable.

N/A - All principles satisfied without violations.
