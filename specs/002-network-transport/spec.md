# Feature Specification: Network Transport Layer

**Feature Branch**: `002-network-transport`
**Created**: January 2, 2026
**Status**: Draft
**Input**: User description: "Implement UDP network transport layer for IPv8 protocol communication with Tribler network including bootstrap peer discovery, live handshake, circuit establishment over real network, and MonoTorrent traffic routing through anonymous circuits"

**Dependency**: This feature extends `001-tunnelfin-core-plugin` by implementing the missing network transport layer that enables actual communication with the Tribler anonymity network.

## Clarifications

### Session 2026-01-02

- Q: How many circuits should the system maintain concurrently? → A: 2-3 circuits (primary + warm standbys for failover)
- Q: How should the system handle potentially malicious or unreliable relays? → A: Track reliability metrics (success rate, latency variance) and deprioritize unreliable relays
- Q: How should the system handle IPv8 protocol version compatibility? → A: Target current stable (v3.x per py-ipv8 v3.1.0), require version handshake, reject incompatible peers

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Live Network Communication (Priority: P1)

A TunnelFin plugin instance needs to send and receive IPv8 protocol messages over the network to communicate with Tribler peers. Without this capability, the plugin cannot participate in the anonymity network and all traffic would be exposed directly.

**Why this priority**: This is the foundational capability - without actual network I/O, no anonymous communication is possible. All other features (peer discovery, circuits, traffic routing) depend on this.

**Independent Test**: Can be tested by starting the transport, binding to a port, and verifying that packets can be sent to and received from a known endpoint (e.g., echo server or loopback test).

**Acceptance Scenarios**:

1. **Given** the plugin is initializing, **When** the network transport starts, **Then** it binds to an available UDP port and reports readiness
2. **Given** the transport is running, **When** a peer sends an IPv8 message, **Then** the message is received and dispatched to the protocol handler
3. **Given** the transport is running, **When** the protocol layer sends a message, **Then** the message is transmitted to the specified peer address
4. **Given** the transport encounters a network error, **When** the error is transient, **Then** the transport retries with exponential backoff

---

### User Story 2 - Bootstrap Peer Discovery (Priority: P1)

The plugin needs to discover initial peers in the Tribler network to begin building its peer table and establishing circuits. Without bootstrap nodes, the plugin would have no entry point into the network.

**Why this priority**: Bootstrap discovery is essential for network entry. Without known peers, the plugin is isolated and cannot function.

**Independent Test**: Can be tested by connecting to known Tribler bootstrap nodes (TU Delft infrastructure) and verifying that introduction-request messages receive valid responses.

**Acceptance Scenarios**:

1. **Given** the plugin has no known peers, **When** it starts, **Then** it contacts hardcoded bootstrap nodes from Tribler's public infrastructure
2. **Given** a bootstrap node is contacted, **When** introduction-request is sent, **Then** introduction-response is received with peer addresses
3. **Given** bootstrap nodes are unreachable, **When** timeout occurs, **Then** the system retries with backup nodes and reports status to user
4. **Given** peers are discovered, **When** they respond to handshake, **Then** they are added to the peer table as relay candidates

---

### User Story 3 - Four-Way Handshake Protocol (Priority: P1)

The plugin needs to perform the IPv8 four-message handshake (introduction-request, introduction-response, puncture-request, puncture) to establish communication with peers and enable NAT traversal.

**Why this priority**: Handshake is required to verify peer identity and establish bidirectional communication through NAT. Without this, peers cannot be used as relays.

**Independent Test**: Can be tested by initiating handshake with a known Tribler peer and verifying all four messages are exchanged correctly with proper signatures.

**Acceptance Scenarios**:

1. **Given** a new peer is discovered, **When** handshake begins, **Then** introduction-request is sent with correct IPv8 message format
2. **Given** introduction-response is received, **When** peer requires NAT puncture, **Then** puncture-request is sent to the intermediary
3. **Given** puncture message is received, **When** NAT hole is established, **Then** the peer is marked as reachable and handshake-complete
4. **Given** handshake fails after retries, **When** peer is unreachable, **Then** peer is marked as failed and removed from relay candidates

---

### User Story 4 - Circuit Establishment Over Network (Priority: P1)

The plugin needs to establish multi-hop circuits by sending CREATE and EXTEND messages over the network to relay peers, receiving CREATED and EXTENDED responses, and managing circuit state based on actual network round-trips.

**Why this priority**: Circuits are the core anonymity mechanism. Until circuits can be established over real network connections, no anonymous traffic routing is possible.

**Independent Test**: Can be tested by selecting relay peers from the peer table, sending CREATE/EXTEND messages, and verifying circuit is established with correct shared secrets.

**Acceptance Scenarios**:

1. **Given** sufficient relay candidates exist, **When** circuit creation is requested, **Then** CREATE message is sent to first hop over UDP
2. **Given** CREATED response is received, **When** more hops are needed, **Then** EXTEND message is sent through the circuit
3. **Given** all hops respond with EXTENDED, **When** circuit is complete, **Then** circuit state is marked as established with shared keys
4. **Given** a hop fails to respond, **When** timeout occurs, **Then** circuit is marked failed and new circuit attempted with different relays

---

### User Story 5 - Anonymous Traffic Routing (Priority: P2)

BitTorrent traffic from the streaming engine needs to be routed through established circuits so that peers in the torrent swarm see relay IP addresses instead of the user's real IP.

**Why this priority**: This is the ultimate goal - anonymous streaming. Depends on circuits being established first, hence P2.

**Independent Test**: Can be tested by establishing a circuit, routing a BitTorrent connection through it, and verifying that swarm peers see the exit node's IP, not the user's IP.

**Acceptance Scenarios**:

1. **Given** an established circuit exists, **When** BitTorrent connects to a peer, **Then** the connection is tunneled through the circuit
2. **Given** traffic is routed through circuit, **When** swarm peers log connections, **Then** they see exit node IP, not user IP
3. **Given** circuit fails mid-stream, **When** traffic cannot be routed, **Then** streaming pauses and new circuit is established (or fallback with consent)
4. **Given** user consents to direct connection, **When** no circuits available, **Then** traffic routes directly with visible warning in UI

---

### Edge Cases

- **All bootstrap nodes unreachable**: System enters degraded mode, retries periodically (every 30 seconds), and clearly indicates to user that anonymous routing is unavailable
- **NAT type incompatible (symmetric NAT)**: System infers NAT type from puncture success/failure patterns during peer introduction exchanges; warns user if hole punching fails for >50% of peers (indicating likely symmetric NAT); falls back to relay-only mode
- **Circuit hop becomes unresponsive mid-session**: System detects via 30-second heartbeat timeout, tears down circuit gracefully, and attempts re-establishment with alternate peers within 30 seconds
- **Port already in use**: System selects random available port by default (per py-ipv8 behavior); if specific port configured, tries alternative ports in range before reporting failure
- **High packet loss network (>10% loss)**: System triggers adaptive retry with exponential backoff (100ms initial, 5s max); after 5 consecutive failures, marks peer as unreachable and selects alternate relay
- **Malicious peer sends invalid messages**: System validates all message signatures and formats using Ed25519 verification, drops invalid messages silently, increments peer unreliability score

## Requirements *(mandatory)*

### Functional Requirements

**UDP Transport Layer**

- **FR-001**: System MUST bind to a configurable UDP port (default: random available port per py-ipv8 behavior, configurable range: 1024-65535) for IPv8 protocol communication
- **FR-002**: System MUST support concurrent sending and receiving of UDP datagrams without blocking
- **FR-003**: System MUST handle packet fragmentation for messages exceeding MTU (typically 1472 bytes for IPv4)
- **FR-004**: System MUST implement retry logic with exponential backoff (initial: 100ms, max: 5s, max retries: 5) for unreliable delivery

**Bootstrap Discovery**

- **FR-005**: System MUST include hardcoded addresses of Tribler bootstrap nodes (TU Delft infrastructure, validated from py-ipv8 v3.1.0 source: 130.161.119.206:6421, 130.161.119.206:6422, 131.180.27.155:6423, 131.180.27.156:6424, 131.180.27.161:6427, 131.180.27.161:6521, 131.180.27.161:6522, 131.180.27.162:6523, 131.180.27.162:6524, 130.161.119.215:6525, 130.161.119.215:6526, 130.161.119.201:6527, 130.161.119.201:6528)
- **FR-006**: System MUST contact bootstrap nodes on startup and request peer introductions
- **FR-007**: System MUST maintain a minimum peer table size of 20 peers before attempting circuit creation
- **FR-008**: System MUST refresh peer table periodically (default: every 5 minutes) by requesting new introductions

**Handshake Protocol**

- **FR-009**: System MUST implement the four-message IPv8 handshake: introduction-request, introduction-response, puncture-request, puncture
- **FR-010**: System MUST sign all introduction messages with the node's Ed25519 private key
- **FR-011**: System MUST verify signatures on all received introduction messages before processing
- **FR-012**: System MUST complete handshake within 10 seconds or mark peer as unreachable
- **FR-013**: System MUST support NAT hole punching via the puncture message exchange
- **FR-013a**: System MUST target IPv8 protocol v3.x (current stable py-ipv8 v3.1.0), include protocol version in handshake, and reject peers with incompatible versions

**Circuit Network Operations**

- **FR-014**: System MUST send CREATE messages over UDP to establish first hop with circuit ID and ephemeral key
- **FR-015**: System MUST send EXTEND messages through established circuit hops to extend circuit
- **FR-016**: System MUST handle CREATED/EXTENDED responses and derive shared secrets using Diffie-Hellman
- **FR-017**: System MUST implement circuit heartbeat (every 30 seconds) to detect dead circuits
- **FR-018**: System MUST send DESTROY messages to cleanly tear down circuits
- **FR-018a**: System MUST maintain 2-3 concurrent circuits (1 primary, 1-2 warm standbys) for failover redundancy
- **FR-018b**: System MUST track relay reliability metrics (circuit success rate, response latency variance) and deprioritize unreliable relays in circuit selection

**Traffic Routing Integration**

- **FR-019**: System MUST provide a tunnel interface that routes outbound TCP connections through circuits
- **FR-020**: System MUST encrypt all tunneled traffic with layered encryption (one layer per hop)
- **FR-021**: System MUST support routing BitTorrent tracker and peer connections through circuits
- **FR-022**: System MUST fall back to direct connection only with explicit user consent per session

**Observability**

- **FR-023**: System MUST expose metrics for: packets sent/received, peer count, circuit count, bootstrap status
- **FR-024**: System MUST log network events at configurable verbosity (errors only, info, debug) without exposing peer IPs in default mode

### Key Entities

- **UdpTransport**: Manages UDP socket lifecycle, sending/receiving datagrams, and dispatching to protocol handlers
- **BootstrapManager**: Maintains list of bootstrap nodes, tracks discovery state, manages peer table population
- **PeerTable**: Stores discovered peers with their addresses, public keys, RTT measurements, reliability metrics (success rate, latency variance), and relay suitability scores; deprioritizes unreliable relays in circuit selection
- **HandshakeStateMachine**: Orchestrates four-message handshake state machine for each peer connection (tracks state per peer: None → IntroRequestSent → IntroResponseReceived → PunctureReceived → Complete)
- **CircuitNetworkClient**: Handles actual network transmission of CREATE/EXTEND/DESTROY messages and response processing
- **TunnelProxy**: Intercepts BitTorrent connections and routes them through established circuits

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Plugin successfully contacts at least one bootstrap node within 30 seconds of startup in 95% of attempts
- **SC-002**: Peer table reaches minimum size (20 peers) within 2 minutes of startup in 90% of attempts
- **SC-003**: Four-way handshake completes successfully with at least 80% of discovered peers
- **SC-004**: Circuit establishment (3 hops) completes within 15 seconds in 90% of attempts when sufficient peers available
- **SC-005**: Tunneled BitTorrent connections successfully hide user IP from swarm peers (verifiable via external IP check service)
- **SC-006**: Packet loss on tunneled connections does not exceed 5% under normal network conditions
- **SC-007**: Circuit remains stable for at least 10 minutes under active streaming load in 95% of cases
- **SC-008**: System recovers from circuit failure and re-establishes within 30 seconds in 90% of cases
- **SC-009**: No TODO comments, placeholder implementations, or stub methods remain in network transport code


## Assumptions

- **A-001**: Tribler bootstrap nodes (TU Delft infrastructure) remain operational and accessible
- **A-002**: IPv8 protocol v3.x specification remains stable (based on current py-ipv8 v3.1.0 reference implementation)
- **A-003**: User's network allows outbound UDP traffic on configurable ports
- **A-004**: Most users are behind NAT but not symmetric NAT (hole punching will work for majority)
- **A-005**: The 001-tunnelfin-core-plugin provides working cryptographic primitives (Ed25519, X25519) and message serialization
- **A-006**: MonoTorrent library supports custom connection routing/proxying
- **A-007**: Jellyfin plugin environment allows UDP socket operations
