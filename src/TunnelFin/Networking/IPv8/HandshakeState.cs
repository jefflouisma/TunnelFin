namespace TunnelFin.Networking.IPv8;

/// <summary>
/// State of the handshake process with a peer (FR-012).
/// </summary>
public enum HandshakeState
{
    /// <summary>
    /// No handshake initiated.
    /// </summary>
    None,

    /// <summary>
    /// Introduction-request sent, waiting for response.
    /// </summary>
    IntroRequestSent,

    /// <summary>
    /// Introduction-response received, handshake complete.
    /// </summary>
    IntroResponseReceived,

    /// <summary>
    /// Puncture-request sent (NAT traversal).
    /// </summary>
    PunctureRequestSent,

    /// <summary>
    /// Puncture received, NAT traversal complete.
    /// </summary>
    PunctureReceived,

    /// <summary>
    /// Handshake timed out.
    /// </summary>
    TimedOut,

    /// <summary>
    /// Handshake failed.
    /// </summary>
    Failed
}

