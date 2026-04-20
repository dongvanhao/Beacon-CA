using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;
using Beacon.Infrashtructure.Presistence;
using Beacon.Infrashtructure.Repository.Identity;
using Beacon.Infrashtructure.Repository.Storage;
using Beacon.Infrashtructure.Services;
using Beacon.Infrashtructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

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
        services.AddScoped<IMediaObjectRepository, MediaObjectRepository>();

        services.AddScoped<IJwtService, JwtService>();

        AddMinio(services, configuration);

        return services;
    }

    private static void AddMinio(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMinioClient>(_ =>
        {
            var rawEndpoint = configuration["MinIO:Endpoint"] ?? "localhost:9000";
            var useSsl = configuration.GetValue<bool?>("MinIO:UseSSL") ?? false;
            var accessKey = configuration["MinIO:AccessKey"] ?? string.Empty;
            var secretKey = configuration["MinIO:SecretKey"] ?? string.Empty;

            var endpoint = rawEndpoint
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            var builder = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey);

            if (useSsl) builder = builder.WithSSL();

            return builder.Build();
        });

        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        services.AddHostedService<MinioBucketInitializer>();
    }
}
