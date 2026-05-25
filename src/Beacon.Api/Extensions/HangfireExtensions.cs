using Beacon.Api.Backgroundjobs;
using Hangfire;
using Hangfire.SqlServer;

namespace Beacon.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions { SchemaName = "Hangfire" }));

        services.AddHangfireServer();

        services.AddScoped<SafetyReminderJob>();
        services.AddScoped<SafetyMissedCheckerJob>();

        return services;
    }

    public static void MapHangfireDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire");
    }

    public static void RegisterRecurringJobs(this IApplicationBuilder app)
    {
        RecurringJob.AddOrUpdate<SafetyReminderJob>(
            "safety-reminder",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));

        RecurringJob.AddOrUpdate<SafetyMissedCheckerJob>(
            "safety-missed-checker",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));
    }
}
