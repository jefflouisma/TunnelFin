using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace TunnelFin.Core;

/// <summary>
/// Privacy-aware logger wrapper that excludes PII and content titles from logs.
/// Implements FR-037 (privacy-first logging) and FR-041 (no content title logging).
/// </summary>
public class PrivacyAwareLogger
{
    private readonly ILogger _logger;
    private readonly bool _enableVerboseLogging;

    // Regex patterns for detecting and redacting sensitive information
    private static readonly Regex IpAddressPattern = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled);
    private static readonly Regex Ipv6Pattern = new(@"\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b", RegexOptions.Compiled);
    private static readonly Regex InfoHashPattern = new(@"\b[0-9a-fA-F]{40}\b", RegexOptions.Compiled);
    private static readonly Regex MagnetUriPattern = new(@"magnet:\?[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PrivacyAwareLogger(ILogger logger, bool enableVerboseLogging = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _enableVerboseLogging = enableVerboseLogging;
    }

    /// <summary>
    /// Redacts sensitive information from a log message.
    /// </summary>
    /// <param name="message">The original message.</param>
    /// <returns>The redacted message.</returns>
    private static string RedactSensitiveInfo(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Redact IP addresses (both IPv4 and IPv6)
        message = IpAddressPattern.Replace(message, "[IP_REDACTED]");
        message = Ipv6Pattern.Replace(message, "[IP_REDACTED]");

        // Redact info hashes (keep first 8 chars for debugging)
        message = InfoHashPattern.Replace(message, m => $"{m.Value.Substring(0, 8)}...[HASH_REDACTED]");

        // Redact magnet URIs (keep the magnet: prefix for context)
        message = MagnetUriPattern.Replace(message, "magnet:?[URI_REDACTED]");

        return message;
    }

    /// <summary>
    /// Logs a debug message with privacy redaction.
    /// Only logged if verbose logging is enabled.
    /// </summary>
    public void LogDebug(string message, params object[] args)
    {
        if (!_enableVerboseLogging)
            return;

        var redactedMessage = RedactSensitiveInfo(message);
        _logger.LogDebug(redactedMessage, args);
    }

    /// <summary>
    /// Logs an information message with privacy redaction.
    /// </summary>
    public void LogInformation(string message, params object[] args)
    {
        var redactedMessage = RedactSensitiveInfo(message);
        _logger.LogInformation(redactedMessage, args);
    }

    /// <summary>
    /// Logs a warning message with privacy redaction.
    /// </summary>
    public void LogWarning(string message, params object[] args)
    {
        var redactedMessage = RedactSensitiveInfo(message);
        _logger.LogWarning(redactedMessage, args);
    }

    /// <summary>
    /// Logs an error message with privacy redaction.
    /// </summary>
    public void LogError(string message, Exception? exception = null, params object[] args)
    {
        var redactedMessage = RedactSensitiveInfo(message);
        if (exception != null)
            _logger.LogError(exception, redactedMessage, args);
        else
            _logger.LogError(redactedMessage, args);
    }

    /// <summary>
    /// Logs a critical error message with privacy redaction.
    /// </summary>
    public void LogCritical(string message, Exception? exception = null, params object[] args)
    {
        var redactedMessage = RedactSensitiveInfo(message);
        if (exception != null)
            _logger.LogCritical(exception, redactedMessage, args);
        else
            _logger.LogCritical(redactedMessage, args);
    }

    /// <summary>
    /// Logs circuit creation event (privacy-safe).
    /// </summary>
    public void LogCircuitCreated(Guid circuitId, int hopCount, double rttMs)
    {
        LogInformation(
            "Circuit created: ID={CircuitId}, Hops={HopCount}, RTT={RttMs}ms",
            circuitId, hopCount, rttMs);
    }

    /// <summary>
    /// Logs circuit failure event (privacy-safe).
    /// </summary>
    public void LogCircuitFailed(Guid circuitId, int hopCount, string reason)
    {
        LogWarning(
            "Circuit failed: ID={CircuitId}, Hops={HopCount}, Reason={Reason}",
            circuitId, hopCount, RedactSensitiveInfo(reason));
    }

    /// <summary>
    /// Logs stream initialization event (privacy-safe, no content title).
    /// </summary>
    public void LogStreamInitialized(Guid streamId, long sizeBytes, bool isAnonymous)
    {
        LogInformation(
            "Stream initialized: ID={StreamId}, Size={SizeBytes} bytes, Anonymous={IsAnonymous}",
            streamId, sizeBytes, isAnonymous);
    }

    /// <summary>
    /// Logs stream progress event (privacy-safe).
    /// </summary>
    public void LogStreamProgress(Guid streamId, double progressPercent, long speedBps)
    {
        LogDebug(
            "Stream progress: ID={StreamId}, Progress={ProgressPercent}%, Speed={SpeedBps} B/s",
            streamId, progressPercent, speedBps);
    }

    /// <summary>
    /// Logs stream failure event (privacy-safe).
    /// </summary>
    public void LogStreamFailed(Guid streamId, string reason)
    {
        LogError(
            "Stream failed: ID={StreamId}, Reason={Reason}",
            null, streamId, RedactSensitiveInfo(reason));
    }
}

