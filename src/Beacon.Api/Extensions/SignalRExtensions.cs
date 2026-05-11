using Beacon.Api.Hubs;
using Beacon.Api.Services;
using Beacon.Application.Common.Interfaces.IService;
using StackExchange.Redis;

namespace Beacon.Api.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddApiSignalR(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");

        var signalR = services.AddSignalR();

        if (!string.IsNullOrWhiteSpace(redisConnection))
            signalR.AddStackExchangeRedis(redisConnection, opts =>
                opts.Configuration.ChannelPrefix = RedisChannel.Literal("beacon"));

        services.AddSingleton<IMessageGroupPresenceTracker, InMemoryMessageGroupPresenceTracker>();
        services.AddSingleton<IUserOnlineTracker, InMemoryUserOnlineTracker>();
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        return services;
    }

    public static WebApplication MapSignalRHubs(this WebApplication app)
    {
        app.MapHub<BeaconHub>("/hubs/beacon");
        return app;
    }
}
