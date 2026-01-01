using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

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
        // Core services will be registered here as they are implemented
        // Phase 3+ will add:
        // - services.AddSingleton<ICircuitManager, CircuitManager>();
        // - services.AddSingleton<ITorrentEngine, TorrentEngine>();
        // - services.AddSingleton<ISearchService, SearchService>();
        // - services.AddSingleton<IFilterService, FilterService>();
        // - services.AddSingleton<IMetadataService, MetadataService>();
        // - services.AddHttpClient<ITriblerClient, TriblerClient>();
        
        // For now, this is a placeholder that will be populated as services are implemented
    }
}

