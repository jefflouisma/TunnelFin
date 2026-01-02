namespace TunnelFin.Networking.IPv8;

/// <summary>
/// IPv8 protocol version handling (FR-013a).
/// Implements version 3.x compatibility per py-ipv8 v3.1.0.
/// </summary>
public class ProtocolVersion
{
    /// <summary>
    /// Major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Minor version number.
    /// </summary>
    public int Minor { get; }

    /// <summary>
    /// Patch version number.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Current protocol version (3.1.0).
    /// </summary>
    public static readonly ProtocolVersion Current = new(3, 1, 0);

    /// <summary>
    /// Minimum compatible version (3.0.0).
    /// </summary>
    public static readonly ProtocolVersion MinimumCompatible = new(3, 0, 0);

    /// <summary>
    /// Creates a new protocol version.
    /// </summary>
    /// <param name="major">Major version.</param>
    /// <param name="minor">Minor version.</param>
    /// <param name="patch">Patch version.</param>
    public ProtocolVersion(int major, int minor, int patch)
    {
        if (major < 0)
            throw new ArgumentException("Major version cannot be negative", nameof(major));
        if (minor < 0)
            throw new ArgumentException("Minor version cannot be negative", nameof(minor));
        if (patch < 0)
            throw new ArgumentException("Patch version cannot be negative", nameof(patch));

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Checks if this version is compatible with another version.
    /// Compatible if major versions match and this version >= other version.
    /// </summary>
    /// <param name="other">Version to check compatibility with.</param>
    /// <returns>True if compatible, false otherwise.</returns>
    public bool IsCompatibleWith(ProtocolVersion other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        // Major version must match
        if (Major != other.Major)
            return false;

        // This version must be >= other version
        if (Minor < other.Minor)
            return false;

        if (Minor == other.Minor && Patch < other.Patch)
            return false;

        return true;
    }

    /// <summary>
    /// Parses a version string (e.g., "3.1.0").
    /// </summary>
    /// <param name="versionString">Version string to parse.</param>
    /// <returns>Parsed protocol version.</returns>
    public static ProtocolVersion Parse(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            throw new ArgumentException("Version string cannot be empty", nameof(versionString));

        var parts = versionString.Split('.');
        if (parts.Length != 3)
            throw new ArgumentException("Version string must be in format 'major.minor.patch'", nameof(versionString));

        if (!int.TryParse(parts[0], out var major))
            throw new ArgumentException("Invalid major version", nameof(versionString));

        if (!int.TryParse(parts[1], out var minor))
            throw new ArgumentException("Invalid minor version", nameof(versionString));

        if (!int.TryParse(parts[2], out var patch))
            throw new ArgumentException("Invalid patch version", nameof(versionString));

        return new ProtocolVersion(major, minor, patch);
    }

    /// <summary>
    /// Converts the version to a string.
    /// </summary>
    /// <returns>Version string (e.g., "3.1.0").</returns>
    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }

    /// <summary>
    /// Checks equality with another version.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not ProtocolVersion other)
            return false;

        return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch);
    }
}

