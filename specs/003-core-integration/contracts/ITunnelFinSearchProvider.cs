using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace TunnelFin.Jellyfin;

/// <summary>
/// Jellyfin ISearchProvider implementation for TunnelFin.
/// Integrates torrent search into Jellyfin's global search UI.
/// </summary>
public interface ITunnelFinSearchProvider : ISearchProvider
{
    /// <summary>
    /// Searches for content matching the query.
    /// Called when user performs search in Jellyfin UI.
    /// </summary>
    /// <param name="query">Search query with term and filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results as Jellyfin BaseItem objects</returns>
    Task<IEnumerable<SearchResult>> GetSearchResults(SearchQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Converts a TorrentResult to a Jellyfin SearchResult.
    /// </summary>
    /// <param name="result">Torrent search result</param>
    /// <returns>Search result for Jellyfin UI</returns>
    SearchResult ToSearchResult(TorrentResult result);

    /// <summary>
    /// Gets the name of this search provider.
    /// Displayed in Jellyfin UI as source of search results.
    /// </summary>
    string Name { get; }
}

