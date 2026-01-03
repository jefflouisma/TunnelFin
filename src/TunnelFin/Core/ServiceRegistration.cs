using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using TunnelFin.BitTorrent;
using TunnelFin.Configuration;
using TunnelFin.Indexers;
using TunnelFin.Jellyfin;
using TunnelFin.Streaming;

namespace TunnelFin.Core;

/// <summary>
/// Registers TunnelFin services with the Jellyfin dependency injection container.
/// Implements IPluginServiceRegistrator to hook into Jellyfin's service registration lifecycle.
/// </summary>
public class ServiceRegistration : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers all TunnelFin services with the dependency injection container.
    /// Called by Jellyfin during application startup.
    /// </summary>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="applicationHost">The Jellyfin application host.</param>
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        // Configuration
        services.AddSingleton<StreamingConfig>();

        // HTTP client for indexers
        services.AddHttpClient<IIndexerManager, IndexerManager>();

        // Core services
        services.AddSingleton<ITorrentEngine, TorrentEngine>();
        services.AddSingleton<IStreamManager, StreamManager>();

        // Jellyfin channel integration - this is how Jellyfin discovers the channel
        services.AddSingleton<IChannel, TunnelFinChannel>();
    }
}

