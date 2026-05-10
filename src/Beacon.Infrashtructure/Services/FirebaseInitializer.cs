using FirebaseAdmin;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Beacon.Infrashtructure.Services;

internal sealed class FirebaseInitializer(ILogger<FirebaseInitializer> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (FirebaseApp.DefaultInstance is not null)
            logger.LogInformation("[Firebase] FCM initialized. ProjectId={ProjectId}",
                FirebaseApp.DefaultInstance.Options.ProjectId ?? "(unknown)");
        else
            logger.LogWarning("[Firebase] No credential configured — FCM push disabled. Notifications will use SignalR only.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
