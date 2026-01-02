using System.Net;

namespace TunnelFin.Networking.Transport;

/// <summary>
/// Event arguments for received datagram packets.
/// </summary>
public class DatagramReceivedEventArgs : EventArgs
{
    /// <summary>
    /// Received data.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// Source endpoint.
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Timestamp when packet was received.
    /// </summary>
    public DateTime ReceivedAt { get; }

    /// <summary>
    /// Creates event arguments for a received datagram.
    /// </summary>
    /// <param name="data">Received data.</param>
    /// <param name="remoteEndPoint">Source endpoint.</param>
    public DatagramReceivedEventArgs(ReadOnlyMemory<byte> data, IPEndPoint remoteEndPoint)
    {
        Data = data;
        RemoteEndPoint = remoteEndPoint;
        ReceivedAt = DateTime.UtcNow;
    }
}

