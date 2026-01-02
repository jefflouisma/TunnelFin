using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TunnelFin.Core;

namespace TunnelFin.Networking.Transport;

/// <summary>
/// UDP transport implementation for IPv8 protocol (FR-001, FR-002, FR-003).
/// </summary>
public class UdpTransport : ITransport, IDisposable
{
    private const int IPv4Mtu = 1472; // 1500 - 20 (IP header) - 8 (UDP header)
    private const int ReceiveBufferSize = 65536; // Maximum UDP packet size

    private readonly PrivacyAwareLogger _logger;
    private readonly object _lock = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;

    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;

    /// <inheritdoc/>
    public IPEndPoint? LocalEndPoint { get; private set; }

    /// <inheritdoc/>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<DatagramReceivedEventArgs>? DatagramReceived;

    /// <summary>
    /// Total packets sent.
    /// </summary>
    public long PacketsSent => Interlocked.Read(ref _packetsSent);

    /// <summary>
    /// Total packets received.
    /// </summary>
    public long PacketsReceived => Interlocked.Read(ref _packetsReceived);

    /// <summary>
    /// Total bytes sent.
    /// </summary>
    public long BytesSent => Interlocked.Read(ref _bytesSent);

    /// <summary>
    /// Total bytes received.
    /// </summary>
    public long BytesReceived => Interlocked.Read(ref _bytesReceived);

    /// <summary>
    /// Creates a new UDP transport instance.
    /// </summary>
    /// <param name="logger">Logger for privacy-aware logging.</param>
    public UdpTransport(ILogger logger)
    {
        _logger = new PrivacyAwareLogger(logger ?? throw new ArgumentNullException(nameof(logger)));
    }

    /// <inheritdoc/>
    public async Task StartAsync(ushort port = 0, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsRunning)
                throw new InvalidOperationException("Transport is already running");

            try
            {
                // Bind to random available port if port = 0 (per py-ipv8 behavior)
                _udpClient = new UdpClient(port);
                var boundEndPoint = (IPEndPoint)_udpClient.Client.LocalEndPoint!;

                // Replace 0.0.0.0 with 127.0.0.1 for LocalEndPoint (loopback)
                // This ensures the endpoint is routable for local testing
                var address = boundEndPoint.Address.Equals(IPAddress.Any)
                    ? IPAddress.Loopback
                    : boundEndPoint.Address;
                LocalEndPoint = new IPEndPoint(address, boundEndPoint.Port);
                IsRunning = true;

                _logger.LogInformation("UDP transport started on port {Port}", LocalEndPoint.Port);
            }
            catch (SocketException ex)
            {
                throw new SocketException((int)ex.SocketErrorCode);
            }
        }

        // Start async receive loop
        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

        // Small delay to ensure receive loop is running
        await Task.Delay(10, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _receiveCts?.Cancel();
        }

        // Wait for receive loop to complete
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        lock (_lock)
        {
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
            LocalEndPoint = null;
        }

        _logger.LogInformation("UDP transport stopped");
    }

    /// <inheritdoc/>
    public async Task<int> SendAsync(ReadOnlyMemory<byte> data, IPEndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running");

        if (data.Length > IPv4Mtu)
            throw new ArgumentException($"Packet size {data.Length} exceeds MTU {IPv4Mtu}", nameof(data));

        try
        {
            var bytesSent = await _udpClient!.SendAsync(data, endpoint, cancellationToken);
            Interlocked.Increment(ref _packetsSent);
            Interlocked.Add(ref _bytesSent, bytesSent);
            return bytesSent;
        }
        catch (SocketException ex)
        {
            _logger.LogError("Send failed to {Endpoint}", ex, endpoint);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            throw new InvalidOperationException("Transport is not running");

        return await _udpClient!.ReceiveAsync(cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        // Use pinned buffer for zero-copy receive (FR-002)
        var buffer = GC.AllocateArray<byte>(ReceiveBufferSize, pinned: true);

        while (!cancellationToken.IsCancellationRequested && IsRunning)
        {
            try
            {
                var result = await _udpClient!.ReceiveAsync(cancellationToken);
                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, result.Buffer.Length);

                // Fire event
                DatagramReceived?.Invoke(this, new DatagramReceivedEventArgs(result.Buffer, result.RemoteEndPoint));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Receive error", ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopAsync().GetAwaiter().GetResult();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

