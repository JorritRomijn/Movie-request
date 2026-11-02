using Jellyfin.MovieRequests.Repositories;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.MovieRequests.Services;

public sealed class DatabaseInitializationService : IHostedService
{
    public DatabaseInitializationService(RequestRepository requestRepository)
    {
        ArgumentNullException.ThrowIfNull(requestRepository);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
