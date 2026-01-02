namespace TunnelFin.Networking.Transport;

/// <summary>
/// Exponential backoff retry policy for network operations (FR-004).
/// </summary>
public class RetryPolicy
{
    private static readonly Random _random = new();

    /// <summary>
    /// Initial backoff delay in milliseconds.
    /// </summary>
    public int InitialDelayMs { get; }

    /// <summary>
    /// Maximum backoff delay in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; }

    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; }

    /// <summary>
    /// Jitter percentage (0.0 to 1.0) to add randomness to backoff.
    /// </summary>
    public double JitterPercent { get; }

    /// <summary>
    /// Creates a retry policy with exponential backoff.
    /// </summary>
    /// <param name="initialDelayMs">Initial delay (default: 100ms).</param>
    /// <param name="maxDelayMs">Maximum delay (default: 5000ms).</param>
    /// <param name="maxRetries">Maximum retries (default: 5).</param>
    /// <param name="jitterPercent">Jitter percentage (default: 0.25 = 25%).</param>
    public RetryPolicy(
        int initialDelayMs = 100,
        int maxDelayMs = 5000,
        int maxRetries = 5,
        double jitterPercent = 0.25)
    {
        if (initialDelayMs <= 0)
            throw new ArgumentException("Initial delay must be positive", nameof(initialDelayMs));
        if (maxDelayMs < initialDelayMs)
            throw new ArgumentException("Max delay must be >= initial delay", nameof(maxDelayMs));
        if (maxRetries < 0)
            throw new ArgumentException("Max retries must be non-negative", nameof(maxRetries));
        if (jitterPercent < 0 || jitterPercent > 1)
            throw new ArgumentException("Jitter must be between 0 and 1", nameof(jitterPercent));

        InitialDelayMs = initialDelayMs;
        MaxDelayMs = maxDelayMs;
        MaxRetries = maxRetries;
        JitterPercent = jitterPercent;
    }

    /// <summary>
    /// Calculate backoff delay for a given attempt number.
    /// </summary>
    /// <param name="attemptNumber">Attempt number (0-based).</param>
    /// <returns>Delay in milliseconds with jitter applied.</returns>
    public int GetBackoffDelayMs(int attemptNumber)
    {
        if (attemptNumber < 0)
            throw new ArgumentException("Attempt number must be non-negative", nameof(attemptNumber));

        // Exponential backoff: initialDelay * 2^attemptNumber
        var baseDelay = InitialDelayMs * Math.Pow(2, attemptNumber);
        var cappedDelay = Math.Min(baseDelay, MaxDelayMs);

        // Apply jitter: delay ± (delay * jitterPercent)
        var jitterRange = cappedDelay * JitterPercent;
        var jitter = (_random.NextDouble() * 2 - 1) * jitterRange; // Random value in [-jitterRange, +jitterRange]

        var finalDelay = cappedDelay + jitter;
        return (int)Math.Max(0, finalDelay);
    }

    /// <summary>
    /// Execute an async operation with retry logic.
    /// </summary>
    /// <typeparam name="T">Return type.</typeparam>
    /// <param name="operation">Operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Operation result.</returns>
    /// <exception cref="Exception">Rethrows last exception if all retries exhausted.</exception>
    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                lastException = ex;
                var delayMs = GetBackoffDelayMs(attempt);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        // All retries exhausted
        throw lastException ?? new InvalidOperationException("Retry failed with no exception");
    }
}

