using System.Net;

namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Represents a known bootstrap node in the Tribler network (FR-005).
/// Bootstrap nodes are TU Delft infrastructure nodes used for initial peer discovery.
/// </summary>
public class BootstrapNode
{
    /// <summary>
    /// IPv4 address of the bootstrap node.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// UDP port of the bootstrap node (6421-6528 range).
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// When the node was last contacted (attempt).
    /// </summary>
    public DateTime? LastContactAttempt { get; set; }

    /// <summary>
    /// When the node last responded successfully.
    /// </summary>
    public DateTime? LastSuccessfulContact { get; set; }

    /// <summary>
    /// Whether the node is currently reachable.
    /// </summary>
    public bool IsReachable { get; set; }

    /// <summary>
    /// Gets the endpoint for this bootstrap node.
    /// </summary>
    public IPEndPoint GetEndPoint()
    {
        if (!IPAddress.TryParse(Address, out var ipAddress))
            throw new InvalidOperationException($"Invalid IP address: {Address}");

        return new IPEndPoint(ipAddress, Port);
    }

    /// <summary>
    /// Validates the bootstrap node configuration.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Address))
            return false;

        if (!IPAddress.TryParse(Address, out _))
            return false;

        if (Port < 6421 || Port > 6528)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the default TU Delft bootstrap nodes (FR-005).
    /// </summary>
    public static List<BootstrapNode> GetDefaultNodes()
    {
        return new List<BootstrapNode>
        {
            new() { Address = "130.161.119.206", Port = 6421 },
            new() { Address = "130.161.119.206", Port = 6422 },
            new() { Address = "130.161.119.215", Port = 6423 },
            new() { Address = "130.161.119.215", Port = 6424 },
            new() { Address = "130.161.119.201", Port = 6425 },
            new() { Address = "130.161.119.201", Port = 6426 },
            new() { Address = "130.161.119.206", Port = 6521 },
            new() { Address = "130.161.119.206", Port = 6522 },
            new() { Address = "130.161.119.215", Port = 6523 },
            new() { Address = "130.161.119.215", Port = 6524 },
            new() { Address = "130.161.119.215", Port = 6525 },
            new() { Address = "130.161.119.215", Port = 6526 },
            new() { Address = "130.161.119.201", Port = 6527 },
            new() { Address = "130.161.119.201", Port = 6528 },
        };
    }
}

