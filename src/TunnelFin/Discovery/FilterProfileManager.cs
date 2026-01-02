using TunnelFin.Models;

namespace TunnelFin.Discovery;

/// <summary>
/// FilterProfileManager provides CRUD operations for filter profiles (T082).
/// Supports separate profiles for Movies, TV Shows, Anime per FR-024.
/// </summary>
public class FilterProfileManager
{
    private readonly Dictionary<Guid, FilterProfile> _profiles = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new filter profile.
    /// </summary>
    public FilterProfile CreateProfile(FilterProfile profile)
    {
        lock (_lock)
        {
            if (profile.ProfileId == Guid.Empty)
            {
                profile.ProfileId = Guid.NewGuid();
            }

            _profiles[profile.ProfileId] = profile;
            return profile;
        }
    }

    /// <summary>
    /// Gets a filter profile by ID.
    /// </summary>
    public FilterProfile? GetProfile(Guid profileId)
    {
        lock (_lock)
        {
            return _profiles.TryGetValue(profileId, out var profile) ? profile : null;
        }
    }

    /// <summary>
    /// Gets all filter profiles.
    /// </summary>
    public List<FilterProfile> GetAllProfiles()
    {
        lock (_lock)
        {
            return _profiles.Values.ToList();
        }
    }

    /// <summary>
    /// Gets filter profiles by content type (per FR-024).
    /// </summary>
    public List<FilterProfile> GetProfilesByContentType(ContentType contentType)
    {
        lock (_lock)
        {
            return _profiles.Values
                .Where(p => p.ContentTypes.Contains(contentType))
                .ToList();
        }
    }

    /// <summary>
    /// Updates an existing filter profile.
    /// </summary>
    public bool UpdateProfile(FilterProfile profile)
    {
        lock (_lock)
        {
            if (!_profiles.ContainsKey(profile.ProfileId))
            {
                return false;
            }

            _profiles[profile.ProfileId] = profile;
            return true;
        }
    }

    /// <summary>
    /// Deletes a filter profile.
    /// </summary>
    public bool DeleteProfile(Guid profileId)
    {
        lock (_lock)
        {
            return _profiles.Remove(profileId);
        }
    }

    /// <summary>
    /// Gets the default profile for a content type.
    /// Returns the first enabled profile for the content type, or null if none found.
    /// </summary>
    public FilterProfile? GetDefaultProfile(ContentType contentType)
    {
        lock (_lock)
        {
            return _profiles.Values
                .Where(p => p.IsEnabled && p.ContentTypes.Contains(contentType))
                .OrderBy(p => p.Priority)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Clears all profiles (for testing).
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            _profiles.Clear();
        }
    }
}

