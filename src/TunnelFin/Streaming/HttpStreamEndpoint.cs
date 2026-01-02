using System.Text.RegularExpressions;

namespace TunnelFin.Streaming;

/// <summary>
/// Handles HTTP range request processing for streaming endpoints (FR-009).
/// Implements RFC 7233 range request handling per streaming-api.yaml contract.
/// </summary>
public class HttpStreamEndpoint
{
    private readonly Guid _streamId;
    private readonly long _contentLength;

    /// <summary>
    /// Stream identifier.
    /// </summary>
    public Guid StreamId => _streamId;

    /// <summary>
    /// Total content length in bytes.
    /// </summary>
    public long ContentLength => _contentLength;

    /// <summary>
    /// Initializes a new instance of the HttpStreamEndpoint class.
    /// </summary>
    /// <param name="streamId">Stream identifier.</param>
    /// <param name="contentLength">Total content length in bytes.</param>
    public HttpStreamEndpoint(Guid streamId, long contentLength)
    {
        _streamId = streamId;
        _contentLength = contentLength;
    }

    /// <summary>
    /// Parses an HTTP Range header (RFC 7233).
    /// </summary>
    /// <param name="rangeHeader">Range header value (e.g., "bytes=0-1023").</param>
    /// <param name="contentLength">Total content length.</param>
    /// <returns>Tuple of (start, end) byte positions, or null if invalid.</returns>
    public (long start, long end)? ParseRangeHeader(string rangeHeader, long contentLength)
    {
        if (string.IsNullOrWhiteSpace(rangeHeader))
            return null;

        // RFC 7233: Range: bytes=start-end
        var match = Regex.Match(rangeHeader, @"bytes=(\d*)-(\d*)");
        if (!match.Success)
            return null;

        var startStr = match.Groups[1].Value;
        var endStr = match.Groups[2].Value;

        // Handle suffix-byte-range-spec: bytes=-500 (last 500 bytes)
        if (string.IsNullOrEmpty(startStr) && !string.IsNullOrEmpty(endStr))
        {
            if (long.TryParse(endStr, out long suffixLength))
            {
                long start = Math.Max(0, contentLength - suffixLength);
                long end = contentLength - 1;
                return (start, end);
            }
            return null;
        }

        // Handle byte-range-spec: bytes=500- or bytes=0-999
        if (!string.IsNullOrEmpty(startStr))
        {
            if (!long.TryParse(startStr, out long start))
                return null;

            long end;
            if (string.IsNullOrEmpty(endStr))
            {
                // Open-ended range: bytes=500-
                end = contentLength - 1;
            }
            else
            {
                if (!long.TryParse(endStr, out end))
                    return null;
            }

            return (start, end);
        }

        return null;
    }

    /// <summary>
    /// Builds a Content-Range header value (RFC 7233).
    /// </summary>
    /// <param name="start">Start byte position.</param>
    /// <param name="end">End byte position.</param>
    /// <param name="totalLength">Total content length.</param>
    /// <returns>Content-Range header value (e.g., "bytes 0-1023/10000").</returns>
    public string BuildContentRangeHeader(long start, long end, long totalLength)
    {
        return $"bytes {start}-{end}/{totalLength}";
    }

    /// <summary>
    /// Gets the appropriate HTTP status code for a request.
    /// </summary>
    /// <param name="hasRangeHeader">Whether the request includes a Range header.</param>
    /// <returns>200 for full content, 206 for partial content.</returns>
    public int GetStatusCode(bool hasRangeHeader)
    {
        return hasRangeHeader ? 206 : 200;
    }

    /// <summary>
    /// Validates a byte range against content length.
    /// </summary>
    /// <param name="start">Start byte position.</param>
    /// <param name="end">End byte position.</param>
    /// <param name="contentLength">Total content length.</param>
    /// <returns>True if range is valid.</returns>
    public bool ValidateRange(long start, long end, long contentLength)
    {
        if (start < 0 || end < 0)
            return false;

        if (start > end)
            return false;

        if (end >= contentLength)
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the content length for a range request.
    /// </summary>
    /// <param name="start">Start byte position.</param>
    /// <param name="end">End byte position.</param>
    /// <returns>Number of bytes in the range.</returns>
    public long CalculateRangeLength(long start, long end)
    {
        return end - start + 1;
    }
}

