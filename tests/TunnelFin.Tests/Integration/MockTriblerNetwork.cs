using System.Net;
using TunnelFin.Networking.Bootstrap;
using TunnelFin.Networking.IPv8;
using TunnelFin.Networking.Transport;

namespace TunnelFin.Tests.Integration;

/// <summary>
/// Mock Tribler network simulator for integration tests.
/// Simulates peer discovery and handshake responses without requiring a live Tribler network.
/// </summary>
public class MockTriblerNetwork : IDisposable
{
    private readonly ITransport _transport;
    private readonly List<Peer> _mockPeers = new();
    private bool _disposed;

    public MockTriblerNetwork(ITransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        
        // Create mock peers
        _mockPeers.Add(CreateMockPeer("131.180.27.155", 6421));
        _mockPeers.Add(CreateMockPeer("131.180.27.156", 6422));
        _mockPeers.Add(CreateMockPeer("131.180.27.157", 6423));
    }

    private Peer CreateMockPeer(string address, int port)
    {
        var ipAddress = IPAddress.Parse(address);
        var ipBytes = ipAddress.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
            Array.Reverse(ipBytes);
        var ipv4Address = BitConverter.ToUInt32(ipBytes, 0);

        // Create mock public key (32 bytes)
        var publicKey = new byte[32];
        new Random().NextBytes(publicKey);

        var peer = new Peer(publicKey, ipv4Address, (ushort)port);

        // Mark as handshake complete and relay candidate for testing
        peer.IsHandshakeComplete = true;
        peer.IsRelayCandidate = true;

        // Set some reasonable metrics
        peer.RttMs = 50.0;
        peer.EstimatedBandwidth = 1_000_000; // 1 MB/s
        peer.RecordSuccess(); // Give it a good reliability score

        return peer;
    }

    /// <summary>
    /// Simulates peer discovery by populating the peer table with mock peers.
    /// </summary>
    public void SimulatePeerDiscovery(IPeerTable peerTable)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockTriblerNetwork));

        foreach (var peer in _mockPeers)
        {
            peerTable.AddPeer(peer);
        }
        peerTable.MarkRefreshed();
    }

    /// <summary>
    /// Gets a mock peer for testing.
    /// </summary>
    public Peer GetMockPeer(int index = 0)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockTriblerNetwork));
        if (index < 0 || index >= _mockPeers.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _mockPeers[index];
    }

    /// <summary>
    /// Gets all mock peers.
    /// </summary>
    public IReadOnlyList<Peer> GetAllMockPeers()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockTriblerNetwork));

        return _mockPeers.AsReadOnly();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _mockPeers.Clear();
        _disposed = true;
    }
}

