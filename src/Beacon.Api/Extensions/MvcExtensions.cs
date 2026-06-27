using Beacon.Api.Filters;

namespace Beacon.Api.Extensions;

public static class MvcExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddScoped<AdminAuditLogFilter>();
        services.AddControllers(options =>
            options.Filters.AddService<AdminAuditLogFilter>());
        return services;
    }
}
