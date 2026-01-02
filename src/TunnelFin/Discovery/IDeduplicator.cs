using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// Interface for deduplicating search results.
/// </summary>
public interface IDeduplicator
{
    /// <summary>
    /// Deduplicates a list of search results (FR-025).
    /// </summary>
    /// <param name="results">Search results to deduplicate.</param>
    /// <returns>Deduplicated list with best quality/seeders preferred.</returns>
    List<SearchResult> Deduplicate(List<SearchResult> results);
}

