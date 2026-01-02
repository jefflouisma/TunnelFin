# Research: Network Transport Layer

**Feature**: 002-network-transport
**Date**: January 2, 2026
**Last Validated**: January 2, 2026 (via Perplexity research)

## Research Tasks

### 1. UDP Socket Management in .NET

**Question**: Best practices for high-performance UDP socket handling in C#/.NET for protocol communication?

**Decision**: Use raw `Socket` class with async/await for performance-critical paths; `UdpClient` acceptable for simpler scenarios

**Rationale** (validated via Perplexity research):
- For high-performance (60k-75k packets/second), raw `Socket` class is recommended over `UdpClient`
- `Socket.ReceiveFromAsync(Memory<byte>, SocketFlags, SocketAddress)` enables zero-copy operations
- Use pinned buffers via `GC.AllocateArray<byte>(size, pinned: true)` to reduce GC pressure
- .NET 6+ includes ValueTask-based async methods optimized for high throughput
- For TunnelFin's use case (low message rate protocol), `UdpClient` is adequate but `Socket` provides future scalability

**Updated Approach**:
```csharp
// High-performance pattern for IPv8 protocol
byte[] buffer = GC.AllocateArray<byte>(65527, pinned: true);
var receivedAddress = new SocketAddress(socket.AddressFamily);
var receivedBytes = await socket.ReceiveFromAsync(buffer.AsMemory(), SocketFlags.None, receivedAddress);
```

**Alternatives Considered**:
- `UdpClient`: Simpler but higher overhead, more allocations - acceptable for our low message rate
- Pipelines API: Overkill for discrete UDP datagrams
- libuv bindings: External dependency, cross-platform complexity

### 2. Tribler Bootstrap Infrastructure

**Question**: What are the current Tribler bootstrap nodes and their availability?

**Decision**: Use TU Delft infrastructure with fallback DNS addresses (VALIDATED)

**Rationale** (validated via py-ipv8 source code inspection):
- Official bootstrap nodes confirmed from `py-ipv8/ipv8/configuration.py`
- **Corrected ports**: Not 8000, actual ports are 6421-6528
- Multiple redundant nodes across TU Delft infrastructure

**Validated Bootstrap Nodes** (from py-ipv8 source):
```
IP Addresses:
- 130.161.119.206:6421, 130.161.119.206:6422
- 131.180.27.155:6423, 131.180.27.156:6424
- 131.180.27.161:6427, 131.180.27.161:6521, 131.180.27.161:6522
- 131.180.27.162:6523, 131.180.27.162:6524
- 130.161.119.215:6525, 130.161.119.215:6526
- 130.161.119.201:6527, 130.161.119.201:6528

DNS Addresses:
- dispersy1.tribler.org:6421, dispersy1.st.tudelft.nl:6421
- dispersy2.tribler.org:6422, dispersy2.st.tudelft.nl:6422
- dispersy3.tribler.org:6423, dispersy3.st.tudelft.nl:6423
- dispersy4.tribler.org:6424
```

**Bootstrap Timeout**: 30.0 seconds (from py-ipv8 defaults)

**Alternatives Considered**:
- DHT-only bootstrap: Slower initial discovery
- Custom bootstrap server: Centralized, defeats decentralization goal

### 3. IPv8 Protocol Version Compatibility

**Question**: Which IPv8 protocol version to target for Tribler network compatibility?

**Decision**: Target py-ipv8 v3.x (current stable is v3.1.0) - CORRECTED from v2.x

**Rationale** (validated via Perplexity research):
- **CORRECTION**: Current stable py-ipv8 is v3.1.0, not v2.x
- v3.x includes dataclass payload updates with base class changes
- Minimum Python 3.9 support in v3.x (informational for reference implementation)
- Wire protocol backward compatibility with v2.x not explicitly documented but likely compatible
- Should implement version detection and negotiate accordingly

**Key v3.x Changes from v2.x**:
- RequestCache now waits for cache arrival
- Peer address freeze support
- Taskmanager shutdown tasks
- Fixed community bootstrap
- Dataclass payloads use base class

**Approach**: Implement wire-compatible with v3.x, test against live Tribler network to verify

**Alternatives Considered**:
- Target v2.x only: May encounter decreasing peer population as Tribler updates
- Multi-version negotiation: Implement if v2/v3 incompatibilities discovered during testing

### 4. NAT Traversal Strategy

**Question**: How to handle NAT traversal for peer connectivity?

**Decision**: Implement STUN-like hole punching via puncture messages (VALIDATED)

**Rationale** (validated via Perplexity research):
- IPv8 uses integrated NAT puncturing via `puncture-request` and `puncture` messages
- No external STUN servers required - fully decentralized approach
- Uses custom NAT-traversing DHT for address discovery
- Public keys abstract IP addresses, avoiding reliance on central servers

**NAT Type Compatibility**:
| NAT Type | Hole Punching Success |
|----------|----------------------|
| Full Cone | High (any source allowed) |
| Restricted/Port-Restricted Cone | Medium (predictable punching) |
| Symmetric | Low (requires relay fallback) |

**Estimated NAT Distribution**: ~60-80% of residential NATs are cone-like (varies by region/ISP)

**Puncture Mechanism**:
1. Peer A sends `puncture-request` to Peer B via known intermediary
2. Peer B sends `puncture` (UDP packets) to Peer A's predicted public endpoint
3. Both sides create NAT mappings, treating replies as solicited traffic
4. Bidirectional connectivity established

**Alternatives Considered**:
- External STUN servers: Centralized dependency, violates decentralization principle
- UPnP port forwarding: Not available in all environments
- Relay-only mode: Significantly slower, use only as fallback for symmetric NAT

### 5. MonoTorrent Traffic Routing Integration

**Question**: How to route MonoTorrent connections through anonymous circuits?

**Decision**: Implement custom `ISocketConnector` adapter wrapping circuit tunnels (VALIDATED)

**Rationale** (validated via MonoTorrent source code inspection):
- MonoTorrent provides `ISocketConnector` interface for custom connection handling
- Interface located at: `MonoTorrent.Connections.ISocketConnector`
- Single method: `ReusableTask<Socket> ConnectAsync(Uri uri, CancellationToken token)`
- Can intercept at socket creation level, transparent to BitTorrent protocol

**Implementation Approach**:
```csharp
public class TunnelSocketConnector : ISocketConnector
{
    private readonly CircuitManager _circuitManager;

    public async ReusableTask<Socket> ConnectAsync(Uri uri, CancellationToken token)
    {
        // Create virtual socket that routes through circuit
        var circuit = await _circuitManager.GetOrCreateCircuitAsync(token);
        return new TunnelSocket(circuit, uri.Host, uri.Port);
    }
}
```

**Note**: `TunnelSocket` will need to extend/wrap `Socket` to implement circuit-based I/O

**Alternatives Considered**:
- SOCKS5 proxy: Additional complexity, not native integration
- Modify MonoTorrent source: Maintenance burden, fork required
- Raw TCP interception: More complex, `ISocketConnector` is cleaner

### 6. Retry and Backoff Strategy

**Question**: Optimal retry parameters for unreliable UDP delivery?

**Decision**: Exponential backoff with jitter (100ms initial, 5s max, 5 retries)

**Rationale**:
- Matches spec FR-004 requirements
- Exponential backoff prevents thundering herd on network issues
- Jitter (±25%) prevents synchronized retries
- 5 retries covers ~15s total, adequate for circuit establishment timeout

**Parameters**:
```
InitialDelay: 100ms
MaxDelay: 5000ms
MaxRetries: 5
JitterFactor: 0.25
Timeout per message: 2000ms
```

### 7. Relay Reliability Metrics

**Question**: What metrics to track for relay quality assessment?

**Decision**: Track success rate, latency variance, and recency

**Rationale**:
- Success rate: Circuit creation success / attempts (per clarification Q2)
- Latency variance: Std dev of RTT measurements (detect unstable peers)
- Recency: Penalize stale peers not seen recently
- Combined score used for relay selection priority

**Formula**:
```
ReliabilityScore = (SuccessRate * 0.5) + (1 / (1 + LatencyVariance) * 0.3) + (RecencyFactor * 0.2)
```

## Technology Decisions Summary

| Component | Technology | Version | Validation Status |
|-----------|------------|---------|-------------------|
| UDP Transport | System.Net.Sockets.Socket | .NET 10.0 | ✅ Validated |
| Cryptography | NSec.Cryptography | 25.4.0 | ✅ Existing |
| BitTorrent | MonoTorrent | 3.0.2 | ✅ Validated (Aug 2024 release) |
| Testing | xUnit + FluentAssertions + Moq | Latest | ✅ Per constitution |
| Protocol | py-ipv8 v3.x | v3.1.0 compatible | ⚠️ Updated from v2.x |
| Bootstrap Ports | 6421-6528 | TU Delft | ⚠️ Corrected from 8000 |

## Open Questions Resolved

All NEEDS CLARIFICATION items from spec have been addressed:
- ✅ Circuit count: 2-3 concurrent (clarification session)
- ✅ Relay reliability: Metrics-based deprioritization (clarification session)
- ✅ Protocol version: py-ipv8 v3.x (corrected from v2.x via Perplexity validation)
- ✅ Bootstrap nodes: Full list validated from py-ipv8 source code
- ✅ MonoTorrent integration: `ISocketConnector` interface confirmed

## Validation Summary

| Claim | Original | Validated | Status |
|-------|----------|-----------|--------|
| UdpClient recommended | Yes | Socket preferred for high-perf | ⚠️ Corrected |
| Bootstrap ports 8000 | Yes | Ports 6421-6528 | ⚠️ Corrected |
| IPv8 v2.x | Yes | v3.1.0 is current | ⚠️ Corrected |
| NAT puncture mechanism | Yes | Confirmed, no STUN needed | ✅ Validated |
| IConnection interface | Yes | ISocketConnector confirmed | ✅ Validated |
| MonoTorrent v3.0.2 | Yes | Confirmed (Aug 4, 2024) | ✅ Validated |

## References

- [py-ipv8 source](https://github.com/Tribler/py-ipv8) - Protocol reference implementation (v3.1.0)
- [py-ipv8 configuration.py](https://github.com/Tribler/py-ipv8/blob/master/ipv8/configuration.py) - Bootstrap nodes
- [Tribler source](https://github.com/Tribler/tribler) - Full anonymity network
- [MonoTorrent](https://github.com/alanmcgovern/monotorrent) - BitTorrent engine (v3.0.2)
- [.NET UDP Performance](https://enclave.io/high-performance-udp-sockets-net8/) - Socket best practices

