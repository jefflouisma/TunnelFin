using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace TunnelFin.Core;

/// <summary>
/// TunnelFin Plugin - Privacy-first torrent streaming for Jellyfin (T009)
/// Enables anonymous torrent streaming through Tribler anonymity network
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Plugin GUID - unique identifier for TunnelFin
    /// </summary>
    private static readonly Guid PluginGuid = Guid.Parse("A7F8B3C2-1D4E-4A5B-9C6D-7E8F9A0B1C2D");

    /// <summary>
    /// Singleton instance of the plugin
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Initializes a new instance of the TunnelFin plugin
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths</param>
    /// <param name="xmlSerializer">XML serializer for configuration</param>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the plugin's unique identifier
    /// </summary>
    public override Guid Id => PluginGuid;

    /// <summary>
    /// Gets the plugin name
    /// </summary>
    public override string Name => "TunnelFin";

    /// <summary>
    /// Gets the plugin description
    /// </summary>
    public override string Description =>
        "Privacy-first torrent streaming plugin that enables anonymous content streaming through the Tribler anonymity network. " +
        "Features include multi-hop onion routing, integrated content discovery, advanced filtering, and seamless Jellyfin integration.";

    /// <summary>
    /// Gets the plugin configuration pages (T083)
    /// </summary>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var prefix = GetType().Namespace;
        yield return new PluginPageInfo
        {
            Name = "TunnelFin",
            EmbeddedResourcePath = prefix + ".Configuration.config.html"
        };
    }

    /// <summary>
    /// Updates the plugin configuration and clears caches
    /// </summary>
    /// <param name="configuration">New configuration</param>
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        var config = (PluginConfiguration)configuration;

        // Validate configuration before applying
        if (!config.IsValid(out var errors))
        {
            throw new InvalidOperationException(
                $"Invalid configuration: {string.Join(", ", errors)}");
        }

        base.UpdateConfiguration(config);

        // TODO: Clear caches when implemented
        // - Search result cache
        // - Metadata cache
        // - Torrent data cache
    }
}

