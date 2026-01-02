# Quickstart: Network Transport Layer

**Feature**: 002-network-transport  
**Branch**: `002-network-transport`

## Prerequisites

- .NET 10.0 SDK installed
- TunnelFin repository cloned
- Feature `001-tunnelfin-core-plugin` complete (foundation classes)

## Build

```bash
cd /Volumes/4TB_Drive/Documents/TunnelFin
dotnet build src/TunnelFin/TunnelFin.csproj
```

## Run Tests

```bash
# Unit tests (mocked network)
dotnet test tests/TunnelFin.Tests --filter "Category!=Integration"

# Integration tests (requires network access to Tribler)
dotnet test tests/TunnelFin.Tests --filter "Category=Integration"
```

## Key Components

### 1. UDP Transport

```csharp
// Start transport (local port for receiving)
var transport = new UdpTransport();
await transport.StartAsync(port: 8090);  // Default local port per py-ipv8

// Send message to bootstrap node (ports 6421-6528, not 8000)
var endpoint = new IPEndPoint(IPAddress.Parse("130.161.119.206"), 6421);
await transport.SendAsync(messageBytes, endpoint);

// Receive message
var result = await transport.ReceiveAsync(cancellationToken);
Console.WriteLine($"Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}");
```

### 2. Bootstrap Discovery

```csharp
// Initialize bootstrap manager
var bootstrapManager = new BootstrapManager(transport, peerTable, settings);

// Discover peers from bootstrap nodes
int discovered = await bootstrapManager.DiscoverPeersAsync(cancellationToken);
Console.WriteLine($"Discovered {discovered} peers");

// Check if ready for circuits
if (peerTable.Count >= peerTable.MinimumCount)
{
    Console.WriteLine("Ready to create circuits");
}
```

### 3. Circuit Creation (Network)

```csharp
// Create circuit over real network
var circuitClient = new CircuitNetworkClient(transport, protocol);
var circuit = await circuitManager.CreateCircuitAsync(hopCount: 3);

// Circuit now established with live relay nodes
Console.WriteLine($"Circuit {circuit.CircuitId} established: {circuit.State}");
```

### 4. Traffic Tunneling

```csharp
// Create tunnel through circuit
var proxy = new TunnelProxy(circuitClient);
var tunnel = await proxy.CreateTunnelAsync(circuit, 
    new IPEndPoint(trackerIP, trackerPort), 
    cancellationToken);

// Use tunnel for BitTorrent traffic
await tunnel.WriteAsync(announceRequest);
var response = await tunnel.ReadAsync();
```

## Configuration

Key settings in `AnonymitySettings`:

| Setting | Default | Description |
|---------|---------|-------------|
| UdpPort | 8090 | Local UDP port for IPv8 (py-ipv8 default) |
| DefaultHopCount | 3 | Circuit anonymity hops |
| MaxConcurrentCircuits | 3 | Failover circuit pool |
| MinPeerTableSize | 20 | Required peers before circuits |
| CircuitHeartbeatIntervalSeconds | 30 | Keepalive frequency |
| CircuitEstablishmentTimeoutSeconds | 15 | Max time to build circuit |

## Verification Steps

1. **Transport binds successfully**:
   - No port conflict errors
   - `transport.IsRunning == true`

2. **Bootstrap discovery works**:
   - At least one bootstrap node responds
   - Peer table grows (check `peerTable.Count`)

3. **Handshake completes**:
   - Peers marked `IsHandshakeComplete == true`
   - Reliability metrics populated

4. **Circuit established**:
   - `circuit.State == CircuitState.Established`
   - All hops have shared secrets

5. **Traffic tunnels correctly**:
   - BitTorrent connections route through circuit
   - External IP check shows relay address, not user IP

## Troubleshooting

| Issue | Check |
|-------|-------|
| Port bind fails | Try different port, check firewall |
| No bootstrap response | Verify network connectivity, try alternate nodes |
| Handshake timeout | May be symmetric NAT - check NAT type |
| Circuit fails | Insufficient peers - wait for more discovery |
| Tunnel slow | Normal for 3-hop anonymity - latency expected |

## Next Steps

After completing this feature:
1. Run full test suite to verify all success criteria
2. Integration test with MonoTorrent streaming
3. Performance benchmarking under load

