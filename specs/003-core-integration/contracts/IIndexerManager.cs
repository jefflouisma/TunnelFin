using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TunnelFin.Indexers;

/// <summary>
/// Aggregates multiple indexers (Torznab and HTML scrapers) for content discovery.
/// Handles parallel queries, result merging, and rate limiting.
/// </summary>
public interface IIndexerManager
{
    /// <summary>
    /// Searches all enabled indexers for content matching the query.
    /// Results are merged, deduplicated by InfoHash, and sorted by seeders.
    /// </summary>
    /// <param name="query">Search query (e.g., "Big Buck Bunny")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merged and sorted list of torrent results</returns>
    Task<IReadOnlyList<TorrentResult>> SearchAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Searches a specific indexer by ID.
    /// Useful for testing or when user wants to query a single source.
    /// </summary>
    /// <param name="indexerId">Indexer ID from configuration</param>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results from specified indexer only</returns>
    Task<IReadOnlyList<TorrentResult>> SearchIndexerAsync(Guid indexerId, string query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all configured indexers.
    /// </summary>
    /// <returns>List of indexer configurations</returns>
    IReadOnlyList<IndexerConfig> GetIndexers();

    /// <summary>
    /// Adds a new indexer configuration.
    /// </summary>
    /// <param name="config">Indexer configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddIndexerAsync(IndexerConfig config, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing indexer configuration.
    /// </summary>
    /// <param name="config">Updated indexer configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateIndexerAsync(IndexerConfig config, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an indexer configuration.
    /// </summary>
    /// <param name="indexerId">Indexer ID to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveIndexerAsync(Guid indexerId, CancellationToken cancellationToken);

    /// <summary>
    /// Tests connectivity to an indexer.
    /// Useful for validating configuration before saving.
    /// </summary>
    /// <param name="config">Indexer configuration to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if indexer is reachable and returns valid results</returns>
    Task<bool> TestIndexerAsync(IndexerConfig config, CancellationToken cancellationToken);
}

