namespace TunnelFin.Core;

/// <summary>
/// Base exception for all TunnelFin-specific errors.
/// </summary>
public class TunnelFinException : Exception
{
    public TunnelFinException() { }
    public TunnelFinException(string message) : base(message) { }
    public TunnelFinException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when circuit establishment fails.
/// This can occur due to insufficient relay nodes, timeout, or network errors.
/// </summary>
public class CircuitEstablishmentException : TunnelFinException
{
    /// <summary>
    /// The requested hop count for the circuit.
    /// </summary>
    public int RequestedHopCount { get; set; }

    /// <summary>
    /// The number of hops successfully established before failure.
    /// </summary>
    public int EstablishedHops { get; set; }

    /// <summary>
    /// The circuit ID (if partially created).
    /// </summary>
    public Guid? CircuitId { get; set; }

    public CircuitEstablishmentException() { }

    public CircuitEstablishmentException(string message) : base(message) { }

    public CircuitEstablishmentException(string message, Exception innerException) 
        : base(message, innerException) { }

    public CircuitEstablishmentException(
        string message, 
        int requestedHopCount, 
        int establishedHops, 
        Guid? circuitId = null) 
        : base(message)
    {
        RequestedHopCount = requestedHopCount;
        EstablishedHops = establishedHops;
        CircuitId = circuitId;
    }
}

/// <summary>
/// Exception thrown when stream initialization fails.
/// This can occur due to invalid magnet URI, torrent metadata download failure, or resource limits.
/// </summary>
public class StreamInitializationException : TunnelFinException
{
    /// <summary>
    /// The magnet URI that failed to initialize.
    /// </summary>
    public string? MagnetUri { get; set; }

    /// <summary>
    /// The info hash of the torrent (if available).
    /// </summary>
    public string? InfoHash { get; set; }

    /// <summary>
    /// The stream ID (if partially created).
    /// </summary>
    public Guid? StreamId { get; set; }

    /// <summary>
    /// Whether the failure was due to circuit establishment.
    /// </summary>
    public bool IsCircuitFailure { get; set; }

    public StreamInitializationException() { }

    public StreamInitializationException(string message) : base(message) { }

    public StreamInitializationException(string message, Exception innerException) 
        : base(message, innerException) { }

    public StreamInitializationException(
        string message, 
        string? magnetUri = null, 
        string? infoHash = null, 
        Guid? streamId = null,
        bool isCircuitFailure = false) 
        : base(message)
    {
        MagnetUri = magnetUri;
        InfoHash = infoHash;
        StreamId = streamId;
        IsCircuitFailure = isCircuitFailure;
    }
}

/// <summary>
/// Exception thrown when resource limits are exceeded.
/// This can occur when max concurrent streams, cache size, or other quotas are reached.
/// </summary>
public class ResourceLimitException : TunnelFinException
{
    /// <summary>
    /// The type of resource that exceeded its limit.
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// The current value of the resource.
    /// </summary>
    public long CurrentValue { get; set; }

    /// <summary>
    /// The maximum allowed value for the resource.
    /// </summary>
    public long MaxValue { get; set; }

    public ResourceLimitException() { }

    public ResourceLimitException(string message) : base(message) { }

    public ResourceLimitException(string message, Exception innerException) 
        : base(message, innerException) { }

    public ResourceLimitException(
        string message, 
        string resourceType, 
        long currentValue, 
        long maxValue) 
        : base(message)
    {
        ResourceType = resourceType;
        CurrentValue = currentValue;
        MaxValue = maxValue;
    }
}

