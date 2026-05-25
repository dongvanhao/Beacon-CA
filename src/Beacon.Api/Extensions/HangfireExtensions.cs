using Beacon.Api.Backgroundjobs;
using Hangfire;
using Hangfire.Dashboard;
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

        services.AddScoped<DailyRecordSeedingJob>();
        services.AddScoped<SafetyReminderJob>();
        services.AddScoped<SafetyMissedCheckerJob>();

        return services;
    }

    public static void MapHangfireDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new DevelopmentHangfireDashboardAuthorizationFilter() }
            });
    }

    public static void RegisterRecurringJobs(this IApplicationBuilder app)
    {
        // 17:00 UTC = 00:00 Vietnam (UTC+7) — seed DailySafetyRecord for all monitored users
        RecurringJob.AddOrUpdate<DailyRecordSeedingJob>(
            "daily-record-seeding",
            job => job.ExecuteAsync(),
            "0 17 * * *");

        RecurringJob.AddOrUpdate<SafetyReminderJob>(
            "safety-reminder",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));

        RecurringJob.AddOrUpdate<SafetyMissedCheckerJob>(
            "safety-missed-checker",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));
    }

    private sealed class DevelopmentHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) => true;
    }
}
