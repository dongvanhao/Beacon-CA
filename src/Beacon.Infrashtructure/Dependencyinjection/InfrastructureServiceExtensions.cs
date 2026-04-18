using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Beacon.Infrashtructure.Repository.Identity;
using Beacon.Infrashtructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Beacon.Infrashtructure.Dependencyinjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();

        services.AddScoped<IJwtService, JwtService>();

        return services;
    }
}
