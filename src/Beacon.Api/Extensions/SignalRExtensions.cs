using Beacon.Api.Hubs;
using Beacon.Api.Services;
using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Api.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddApiSignalR(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        return services;
    }

    public static WebApplication MapSignalRHubs(this WebApplication app)
    {
        app.MapHub<BeaconHub>("/hubs/beacon");
        return app;
    }
}
