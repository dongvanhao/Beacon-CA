using Beacon.Application.Common.Behaviors;
using Beacon.Application.Mappings.Identity;
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

        return services;
    }
}
