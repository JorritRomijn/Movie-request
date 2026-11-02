using Jellyfin.MovieRequests.Repositories;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.MovieRequests.Services;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton(provider =>
        {
            var applicationPaths = provider.GetRequiredService<IApplicationPaths>();
            var databasePath = Path.Combine(
                applicationPaths.DataPath,
                "plugins",
                "MovieRequests",
                "requests.db");
            return new RequestRepository(databasePath);
        });
        serviceCollection.AddSingleton<IRequestService, RequestService>();
        serviceCollection.AddSingleton<ILibraryMovieLookup, LibraryMovieLookup>();
        serviceCollection.AddHostedService<DatabaseInitializationService>();
        serviceCollection.AddHttpClient("TMDb", client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin.MovieRequests/1.0");
        });
        serviceCollection.AddTransient<ITMDbService, TMDbService>();
    }
}
