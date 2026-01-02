using TunnelFin.Models;

namespace TunnelFin.Indexers;

/// <summary>
/// Interface for torrent indexers (FR-016, FR-017).
/// Defines the contract for searching torrent content across different indexer implementations.
/// </summary>
public interface IIndexer
{
    /// <summary>
    /// Name of the indexer (e.g., "1337x", "Nyaa", "RARBG").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the indexer is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Searches the indexer for torrent content.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="contentType">Content type for filtering.</param>
    /// <param name="cancellationToken">Cancellation token for timeout.</param>
    /// <returns>List of search results.</returns>
    Task<List<SearchResult>> SearchAsync(string query, ContentType contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the capabilities of this indexer.
    /// </summary>
    IndexerCapabilities GetCapabilities();
}

/// <summary>
/// Describes the capabilities of an indexer.
/// </summary>
public class IndexerCapabilities
{
    /// <summary>
    /// Content types supported by this indexer.
    /// </summary>
    public List<ContentType> SupportedContentTypes { get; set; } = new();

    /// <summary>
    /// Whether the indexer supports advanced search features.
    /// </summary>
    public bool SupportsAdvancedSearch { get; set; }

    /// <summary>
    /// Maximum results per search.
    /// </summary>
    public int MaxResults { get; set; } = 100;

    /// <summary>
    /// Search timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

