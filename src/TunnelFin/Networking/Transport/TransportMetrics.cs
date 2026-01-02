namespace TunnelFin.Networking.Transport;

/// <summary>
/// Transport layer metrics (FR-023).
/// </summary>
public class TransportMetrics
{
    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;
    private long _sendErrors;
    private long _receiveErrors;

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
    /// Total send errors.
    /// </summary>
    public long SendErrors => Interlocked.Read(ref _sendErrors);

    /// <summary>
    /// Total receive errors.
    /// </summary>
    public long ReceiveErrors => Interlocked.Read(ref _receiveErrors);

    /// <summary>
    /// Record a sent packet.
    /// </summary>
    /// <param name="bytes">Number of bytes sent.</param>
    public void RecordSent(int bytes)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, bytes);
    }

    /// <summary>
    /// Record a received packet.
    /// </summary>
    /// <param name="bytes">Number of bytes received.</param>
    public void RecordReceived(int bytes)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, bytes);
    }

    /// <summary>
    /// Record a send error.
    /// </summary>
    public void RecordSendError()
    {
        Interlocked.Increment(ref _sendErrors);
    }

    /// <summary>
    /// Record a receive error.
    /// </summary>
    public void RecordReceiveError()
    {
        Interlocked.Increment(ref _receiveErrors);
    }

    /// <summary>
    /// Reset all metrics to zero.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _packetsSent, 0);
        Interlocked.Exchange(ref _packetsReceived, 0);
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _bytesReceived, 0);
        Interlocked.Exchange(ref _sendErrors, 0);
        Interlocked.Exchange(ref _receiveErrors, 0);
    }
}

