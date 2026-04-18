using Beacon.Api.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Beacon.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthChecking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddHttpClient();
        services.AddHealthChecks()
            .AddCheck("backend", () => HealthCheckResult.Healthy("API is running"),
                tags: ["live"])
            .AddSqlServer(connectionString!,
                name: "sqlserver",
                tags: ["db", "ready"])
            .AddCheck<MinioHealthCheck>("minio",
                tags: ["minio", "storage", "ready"]);

        return services;
    }

    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        var options = new HealthCheckOptions { ResponseWriter = WriteJsonResponse };

        app.MapHealthChecks("/health",        options);
        app.MapHealthChecks("/health/live",   new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("live"),
            ResponseWriter = WriteJsonResponse
        });
        app.MapHealthChecks("/health/ready",  new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJsonResponse
        });
        app.MapHealthChecks("/health/db",     new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("db"),
            ResponseWriter = WriteJsonResponse
        });
        app.MapHealthChecks("/health/minio",  new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("minio"),
            ResponseWriter = WriteJsonResponse
        });

        return app;
    }

    private static async Task WriteJsonResponse(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";

        var isHealthy = report.Status == HealthStatus.Healthy;
        var message = report.Status switch
        {
            HealthStatus.Healthy   => "All services healthy",
            HealthStatus.Degraded  => "Some services degraded",
            HealthStatus.Unhealthy => "One or more services unhealthy",
            _                      => "Unknown status"
        };

        var result = new
        {
            success = isHealthy,
            message,
            code   = isHealthy ? (string?)null : "HEALTH_CHECK_FAILED",
            data   = new
            {
                status        = report.Status.ToString(),
                totalDuration = report.TotalDuration.ToString(@"hh\:mm\:ss\.fff"),
                checks        = report.Entries.Select(e => new
                {
                    name        = e.Key,
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration    = e.Value.Duration.ToString(@"hh\:mm\:ss\.fff"),
                    error       = e.Value.Exception?.Message
                })
            },
            errors = (object?)null
        };

        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }),
            ctx.RequestAborted);
    }
}
