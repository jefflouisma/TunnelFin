namespace TunnelFin.Models;

/// <summary>
/// Represents the current state of a torrent stream.
/// </summary>
public enum TorrentStreamState
{
    /// <summary>
    /// Stream is being initialized (circuit creation, torrent metadata download).
    /// </summary>
    Initializing,

    /// <summary>
    /// Stream is actively downloading torrent data.
    /// </summary>
    Downloading,

    /// <summary>
    /// Stream is ready for playback (sufficient buffer available).
    /// </summary>
    Streaming,

    /// <summary>
    /// Stream is paused by user.
    /// </summary>
    Paused,

    /// <summary>
    /// Stream has been stopped and resources released.
    /// </summary>
    Stopped,

    /// <summary>
    /// Stream failed due to an error.
    /// </summary>
    Failed
}

/// <summary>
/// Represents the current state of an anonymity circuit.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is being created (establishing hops).
    /// </summary>
    Creating,

    /// <summary>
    /// Circuit is fully established and ready for use.
    /// </summary>
    Established,

    /// <summary>
    /// Circuit creation or operation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Circuit has been closed and resources released.
    /// </summary>
    Closed
}

/// <summary>
/// Represents the type of content being searched or streamed.
/// </summary>
public enum ContentType
{
    /// <summary>
    /// Movie content.
    /// </summary>
    Movie,

    /// <summary>
    /// TV show content.
    /// </summary>
    TVShow,

    /// <summary>
    /// Anime content.
    /// </summary>
    Anime
}

/// <summary>
/// Represents the type of torrent indexer.
/// </summary>
public enum IndexerType
{
    /// <summary>
    /// Built-in indexer (1337x, Nyaa, RARBG).
    /// </summary>
    BuiltIn,

    /// <summary>
    /// Custom Torznab indexer.
    /// </summary>
    Torznab
}

/// <summary>
/// Represents attributes that can be used for filtering search results.
/// </summary>
public enum FilterAttribute
{
    /// <summary>
    /// Filter by quality (e.g., "1080p", "720p").
    /// </summary>
    Quality,

    /// <summary>
    /// Filter by codec (e.g., "x264", "x265").
    /// </summary>
    Codec,

    /// <summary>
    /// Filter by audio format (e.g., "AAC", "DTS").
    /// </summary>
    Audio,

    /// <summary>
    /// Filter by language.
    /// </summary>
    Language,

    /// <summary>
    /// Filter by file size.
    /// </summary>
    Size,

    /// <summary>
    /// Filter by number of seeders.
    /// </summary>
    Seeders,

    /// <summary>
    /// Filter by release group.
    /// </summary>
    ReleaseGroup,

    /// <summary>
    /// Filter by keywords in title.
    /// </summary>
    Keywords
}

/// <summary>
/// Represents filter comparison operators.
/// </summary>
public enum FilterOperator
{
    /// <summary>
    /// Equals (exact match).
    /// </summary>
    Equals,

    /// <summary>
    /// Not equals.
    /// </summary>
    NotEquals,

    /// <summary>
    /// Greater than (for numeric values).
    /// </summary>
    GreaterThan,

    /// <summary>
    /// Less than (for numeric values).
    /// </summary>
    LessThan,

    /// <summary>
    /// Contains (for string values).
    /// </summary>
    Contains,

    /// <summary>
    /// Does not contain (for string values).
    /// </summary>
    NotContains
}

/// <summary>
/// Represents attributes that can be used for sorting search results.
/// </summary>
public enum SortAttribute
{
    /// <summary>
    /// Sort by relevance score.
    /// </summary>
    Relevance,

    /// <summary>
    /// Sort by number of seeders.
    /// </summary>
    Seeders,

    /// <summary>
    /// Sort by file size.
    /// </summary>
    Size,

    /// <summary>
    /// Sort by upload date.
    /// </summary>
    UploadDate,

    /// <summary>
    /// Sort by quality.
    /// </summary>
    Quality,

    /// <summary>
    /// Sort by title (alphabetical).
    /// </summary>
    Title
}

/// <summary>
/// Represents sort direction.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Sort in ascending order (A-Z, 0-9, oldest first).
    /// </summary>
    Ascending,

    /// <summary>
    /// Sort in descending order (Z-A, 9-0, newest first).
    /// </summary>
    Descending
}

