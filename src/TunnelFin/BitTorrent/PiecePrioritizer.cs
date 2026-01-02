namespace TunnelFin.BitTorrent;

/// <summary>
/// Implements sequential piece prioritization for streaming playback (FR-008).
/// Prioritizes downloading pieces sequentially within a sliding buffer window
/// to enable immediate playback (similar to TorrServer's approach).
/// </summary>
public class PiecePrioritizer
{
    private readonly int _totalPieces;
    private readonly int _bufferWindowSize;

    /// <summary>
    /// Total number of pieces in the torrent.
    /// </summary>
    public int TotalPieces => _totalPieces;

    /// <summary>
    /// Size of the buffer window (number of pieces to prioritize ahead of playback position).
    /// Default is 10-20 pieces per FR-008.
    /// </summary>
    public int BufferWindowSize => _bufferWindowSize;

    /// <summary>
    /// Initializes a new instance of the PiecePrioritizer class.
    /// </summary>
    /// <param name="totalPieces">Total number of pieces in the torrent.</param>
    /// <param name="bufferWindowSize">Number of pieces to prioritize ahead (default: 15).</param>
    public PiecePrioritizer(int totalPieces, int bufferWindowSize = 15)
    {
        if (totalPieces <= 0)
            throw new ArgumentException("Total pieces must be positive", nameof(totalPieces));
        if (bufferWindowSize <= 0)
            throw new ArgumentException("Buffer window size must be positive", nameof(bufferWindowSize));

        _totalPieces = totalPieces;
        _bufferWindowSize = bufferWindowSize;
    }

    /// <summary>
    /// Gets the next pieces to download based on current playback position (FR-008).
    /// Prioritizes pieces sequentially within the buffer window.
    /// </summary>
    /// <param name="availablePieces">Array of piece indices that are available from peers.</param>
    /// <param name="currentPosition">Current playback position (piece index).</param>
    /// <returns>Ordered list of piece indices to download, prioritized sequentially.</returns>
    public IReadOnlyList<int> GetNextPieces(int[] availablePieces, int currentPosition)
    {
        if (availablePieces == null || availablePieces.Length == 0)
            return Array.Empty<int>();

        // Calculate buffer window: [currentPosition, currentPosition + bufferWindowSize)
        int windowStart = currentPosition;
        int windowEnd = Math.Min(currentPosition + _bufferWindowSize, _totalPieces);

        // Filter pieces within the buffer window and sort sequentially
        var prioritizedPieces = availablePieces
            .Where(piece => piece >= windowStart && piece < windowEnd)
            .OrderBy(piece => piece)
            .Take(_bufferWindowSize)
            .ToList();

        return prioritizedPieces;
    }

    /// <summary>
    /// Calculates the priority score for a piece based on its distance from playback position.
    /// Lower scores indicate higher priority.
    /// </summary>
    /// <param name="pieceIndex">Piece index to score.</param>
    /// <param name="currentPosition">Current playback position.</param>
    /// <returns>Priority score (0 = highest priority).</returns>
    public int GetPiecePriority(int pieceIndex, int currentPosition)
    {
        // Pieces behind playback position have lowest priority
        if (pieceIndex < currentPosition)
            return int.MaxValue;

        // Pieces within buffer window have priority based on distance
        int distance = pieceIndex - currentPosition;
        if (distance < _bufferWindowSize)
            return distance;

        // Pieces beyond buffer window have low priority
        return int.MaxValue - 1;
    }

    /// <summary>
    /// Determines if a piece is within the current buffer window.
    /// </summary>
    /// <param name="pieceIndex">Piece index to check.</param>
    /// <param name="currentPosition">Current playback position.</param>
    /// <returns>True if piece is within buffer window.</returns>
    public bool IsInBufferWindow(int pieceIndex, int currentPosition)
    {
        return pieceIndex >= currentPosition && 
               pieceIndex < currentPosition + _bufferWindowSize &&
               pieceIndex < _totalPieces;
    }
}

