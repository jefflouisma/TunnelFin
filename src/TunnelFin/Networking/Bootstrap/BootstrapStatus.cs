namespace TunnelFin.Networking.Bootstrap;

/// <summary>
/// Status of the bootstrap discovery process (FR-006).
/// </summary>
public enum BootstrapStatus
{
    /// <summary>
    /// Bootstrap has not started yet.
    /// </summary>
    NotStarted,

    /// <summary>
    /// Contacting bootstrap nodes.
    /// </summary>
    Contacting,

    /// <summary>
    /// Discovering peers from bootstrap responses.
    /// </summary>
    Discovering,

    /// <summary>
    /// Bootstrap complete, peer table populated.
    /// </summary>
    Ready,

    /// <summary>
    /// Bootstrap failed (all nodes unreachable or timeout).
    /// </summary>
    Failed
}

