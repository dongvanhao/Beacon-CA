using Beacon.Application.Common.Behaviors;
using Beacon.Application.Mappings.Authorization;
using Beacon.Application.Mappings.AccountManagement;
using Beacon.Application.Mappings.Checkins;
using Beacon.Application.Mappings.Group;
using Beacon.Application.Mappings.Identity;
using Beacon.Application.Mappings.Messaging;
using Beacon.Application.Mappings.Posts;
using Beacon.Application.Mappings.Safety;
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
        services.AddSingleton<AccountManagementMapper>();
        services.AddSingleton<PermissionMapper>();
        services.AddSingleton<RoleMapper>();
        services.AddSingleton<MediaDtoMapper>();
        services.AddSingleton<SafetySettingMapper>();
        services.AddSingleton<CheckinMapper>();
        services.AddSingleton<CheckinHistoryMapper>();
        services.AddSingleton<CheckinStatusMapper>();

        services.AddSingleton<FriendRequestMapper>();
        services.AddSingleton<FriendMapper>();
        services.AddSingleton<FriendPresenceMapper>();
        services.AddSingleton<MessageMapper>();
        services.AddScoped<MessagePostMapper>();
        services.AddSingleton<MessageGroupMapper>();
        services.AddSingleton<MessageGroupDetailMapper>();

        services.AddSingleton<PostDtoMapper>();
        services.AddSingleton<EmergencyContactMapper>();

        return services;
    }
}
