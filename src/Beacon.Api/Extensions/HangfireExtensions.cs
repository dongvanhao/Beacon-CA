using Beacon.Api.Backgroundjobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.DataProtection;

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
        app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = app.Environment.IsDevelopment()
                ? [new DevelopmentHangfireDashboardAuthorizationFilter()]
                : [new AdminJwtHangfireDashboardAuthorizationFilter()],
            DashboardTitle = "Beacon — Background Jobs"
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

    private sealed class AdminJwtHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        private const string CookieName = "beacon_hf_auth";
        private const string Purpose = "Beacon.Hangfire.Dashboard";

        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();
            var cookie = http.Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(cookie))
                return false;

            try
            {
                var protector = http.RequestServices
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector(Purpose);

                var payload = protector.Unprotect(cookie);
                var separator = payload.IndexOf('|');
                if (separator < 0) return false;
                var expiryStr = payload[(separator + 1)..];

                return DateTimeOffset.TryParse(expiryStr, out var expiry)
                    && expiry > DateTimeOffset.UtcNow;
            }
            catch
            {
                return false;
            }
        }
    }
}
