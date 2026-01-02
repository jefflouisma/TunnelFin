namespace TunnelFin.Networking.IPv8;

/// <summary>
/// NAT type classification for peer connectivity (FR-014).
/// Based on STUN RFC 3489 NAT behavior classification.
/// </summary>
public enum NatType
{
    /// <summary>
    /// NAT type is unknown or not yet determined.
    /// </summary>
    Unknown,

    /// <summary>
    /// No NAT - peer has a public IP address.
    /// </summary>
    None,

    /// <summary>
    /// Full cone NAT - all requests from same internal IP:port are mapped to same external IP:port.
    /// Any external host can send packets to the mapped external address.
    /// </summary>
    FullCone,

    /// <summary>
    /// Restricted cone NAT - like full cone, but external host must have received a packet first.
    /// </summary>
    RestrictedCone,

    /// <summary>
    /// Port-restricted cone NAT - like restricted cone, but external host must match both IP and port.
    /// </summary>
    PortRestrictedCone,

    /// <summary>
    /// Symmetric NAT - different external IP:port for each destination.
    /// Most restrictive, requires NAT puncture for peer-to-peer connectivity.
    /// </summary>
    Symmetric
}

