namespace TunnelFin.Core;

/// <summary>
/// ErrorLogger provides privacy-aware error logging (T093).
/// Implements FR-041: Error logs without content titles or user identifiers.
/// Implements SC-013: No PII exposure in logs.
/// </summary>
public class ErrorLogger
{
    private readonly List<ErrorEntry> _errors = new();
    private readonly object _lock = new();
    private const int MaxStoredErrors = 1000;

    /// <summary>
    /// Logs an error without PII (FR-041, SC-013).
    /// </summary>
    /// <param name="component">Component name (e.g., "CircuitManager")</param>
    /// <param name="message">Error message (must not contain content titles or user identifiers)</param>
    public void LogError(string component, string message)
    {
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("Component cannot be empty", nameof(component));
        
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        lock (_lock)
        {
            _errors.Add(new ErrorEntry
            {
                Component = component,
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            // Keep only the most recent errors
            if (_errors.Count > MaxStoredErrors)
            {
                _errors.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets recent errors (most recent first).
    /// </summary>
    /// <param name="count">Maximum number of errors to return</param>
    public List<ErrorEntry> GetRecentErrors(int count)
    {
        lock (_lock)
        {
            return _errors
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    /// <summary>
    /// Gets total error count.
    /// </summary>
    public int GetErrorCount()
    {
        lock (_lock)
        {
            return _errors.Count;
        }
    }

    /// <summary>
    /// Clears all stored errors.
    /// </summary>
    public void ClearErrors()
    {
        lock (_lock)
        {
            _errors.Clear();
        }
    }
}

/// <summary>
/// Represents a single error entry.
/// </summary>
public class ErrorEntry
{
    /// <summary>
    /// Component that logged the error.
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Error message (no PII per FR-041).
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

