using Jellyfin.Plugin.StreamingSources.Cache;
using Jellyfin.Plugin.StreamingSources.Debrid;
using Jellyfin.Plugin.StreamingSources.ExternalApi;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.StreamingSources;

    /// <summary>
    /// Registers plugin services in Jellyfin's dependency injection container.
    /// </summary>
    public sealed class ServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ISourceCache, MemorySourceCache>();
        serviceCollection.AddHttpClient<IExternalSourceClient, ExternalSourceClient>();
        serviceCollection.AddHttpClient<AllDebridProvider>();
        serviceCollection.AddTransient<IDebridProvider>(provider => provider.GetRequiredService<AllDebridProvider>());
    }
}
