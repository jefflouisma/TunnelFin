using System;
using System.IO;

namespace TunnelFin.Tests.Fixtures;

/// <summary>
/// Provides environment configuration for integration tests.
/// Loads configuration from .env file in project root.
/// </summary>
public static class TestEnvironment
{
    private static bool _loaded;
    
    /// <summary>
    /// Jellyfin server URL for integration tests.
    /// </summary>
    public static string? JellyfinUrl => Environment.GetEnvironmentVariable("JELLYFIN_URL");
    
    /// <summary>
    /// Jellyfin username for integration tests.
    /// </summary>
    public static string? JellyfinUsername => Environment.GetEnvironmentVariable("JELLYFIN_USERNAME");
    
    /// <summary>
    /// Jellyfin password for integration tests.
    /// </summary>
    public static string? JellyfinPassword => Environment.GetEnvironmentVariable("JELLYFIN_PASSWORD");
    
    /// <summary>
    /// Returns true if Jellyfin integration test credentials are configured.
    /// </summary>
    public static bool IsJellyfinConfigured => 
        !string.IsNullOrEmpty(JellyfinUrl) && 
        !string.IsNullOrEmpty(JellyfinUsername) && 
        !string.IsNullOrEmpty(JellyfinPassword);

    /// <summary>
    /// Loads environment variables from .env file in project root.
    /// Call this from test class constructor or fixture setup.
    /// </summary>
    public static void LoadEnvFile()
    {
        if (_loaded) return;
        
        // Find project root by looking for .env file
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var envPath = Path.Combine(dir, ".env");
            if (File.Exists(envPath))
            {
                LoadEnvFromFile(envPath);
                _loaded = true;
                return;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        
        _loaded = true; // Mark as loaded even if no file found
    }

    private static void LoadEnvFromFile(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;
                
            var idx = trimmed.IndexOf('=');
            if (idx <= 0) continue;
            
            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();
            
            // Only set if not already set (environment takes precedence)
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

