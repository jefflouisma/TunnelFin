using TunnelFin.Models;

namespace TunnelFin.Indexers.BuiltIn;

/// <summary>
/// Nyaa torrent indexer implementation (FR-016).
/// Anime-specific indexer for searching nyaa.si.
/// </summary>
public class IndexerNyaa : IIndexer
{
    private const string BaseUrl = "https://nyaa.si";
    private bool _isEnabled = true;

    /// <inheritdoc/>
    public string Name => "Nyaa";

    /// <inheritdoc/>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    /// <inheritdoc/>
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        ContentType contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        // TODO: Implement actual HTTP scraping
        // 1. Build search URL for anime category
        // 2. Fetch HTML page
        // 3. Parse torrent results
        // 4. Extract anime-specific metadata (episode, season, fansub group)
        // 5. Return SearchResult list

        // Placeholder implementation
        await Task.Delay(10, cancellationToken);
        return new List<SearchResult>();
    }

    /// <inheritdoc/>
    public IndexerCapabilities GetCapabilities()
    {
        return new IndexerCapabilities
        {
            SupportedContentTypes = new List<ContentType>
            {
                ContentType.Anime
            },
            SupportsAdvancedSearch = false,
            MaxResults = 100,
            TimeoutSeconds = 10
        };
    }
}

