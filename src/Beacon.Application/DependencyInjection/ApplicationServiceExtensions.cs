using Beacon.Application.Common.Behaviors;
using Beacon.Application.Mappings.Checkins;
using Beacon.Application.Mappings.Group;
using Beacon.Application.Mappings.Identity;
using Beacon.Application.Mappings.Messaging;
using Beacon.Application.Mappings.Settings;
using Beacon.Application.Mappings.Storage;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Beacon.Application.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

        services.AddSingleton<UserAuthMapper>();
        services.AddSingleton<UserProfileMapper>();
        services.AddSingleton<AdminAuthMapper>();
        services.AddSingleton<MediaDtoMapper>();
        services.AddSingleton<SafetySettingMapper>();
        services.AddSingleton<CheckinMapper>();
        services.AddSingleton<CheckinStatusMapper>();

        services.AddSingleton<FriendRequestMapper>();
        services.AddSingleton<FriendMapper>();
        services.AddSingleton<MessageMapper>();
        services.AddSingleton<MessageGroupMapper>();
        services.AddSingleton<MessageGroupDetailMapper>();

        return services;
    }
}
