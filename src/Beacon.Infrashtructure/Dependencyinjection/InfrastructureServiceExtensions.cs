using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Identity;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Domain.IRepository.Storage;
using Beacon.Infrashtructure.Presistence;
using Beacon.Infrashtructure.Repository.Checkins;
using Beacon.Infrashtructure.Repository.Group;
using Beacon.Infrashtructure.Repository.Identity;
using Beacon.Infrashtructure.Repository.Messaging;
using Beacon.Infrashtructure.Repository.Safety;
using Beacon.Infrashtructure.Repository.Settings;
using Beacon.Infrashtructure.Repository.Storage;
using Beacon.Infrashtructure.Services;
using Beacon.Infrashtructure.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>();
        services.AddScoped<IAdminRepository, AdminRepository>();
        services.AddScoped<IMediaObjectRepository, MediaObjectRepository>();
        services.AddScoped<ISafetySettingRepository, SafetySettingRepository>();
        services.AddScoped<ICheckinRepository, CheckinRepository>();
        services.AddScoped<IDailySafetyRecordRepository, DailySafetyRecordRepository>();

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IFriendRepository, FriendRepository>();
        services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
        services.AddScoped<IMessageGroupRepository, MessageGroupRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageGroupMemberSettingRepository, MessageGroupMemberSettingRepository>();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<INotificationService, NotificationService>();

        AddMinio(services, configuration);

        return services;
    }

    private static void AddMinio(IServiceCollection services, IConfiguration configuration)
    {
        // ✅ Tập trung toàn bộ cấu hình MinIO vào MinioSettings — chỉ đọc từ 1 section "MinIO"
        services.Configure<MinioSettings>(configuration.GetSection(MinioSettings.SectionName));

        services.AddSingleton<IMinioClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MinioSettings>>().Value;

            // SDK chỉ cần internal endpoint (không cần scheme prefix)
            var internalEndpoint = settings.Endpoint
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            var builder = new MinioClient()
                .WithEndpoint(internalEndpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey);

            if (settings.UseSSL) builder = builder.WithSSL();

            return builder.Build();
        });

        services.AddScoped<IStorageService, MinioStorageService>();
        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        services.AddHostedService<MinioBucketInitializer>();
    }
}
