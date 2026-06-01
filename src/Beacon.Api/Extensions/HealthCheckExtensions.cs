using Beacon.Api.HealthChecks;
using Beacon.Infrashtructure.Configuration;
using Beacon.Shared.Constants;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Beacon.Api.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Map check name → error code mặc định khi check đó fail mà không tự emit code qua <c>HealthCheckResult.Data</c>.
    /// Dùng cho built-in checks (như <c>AddSqlServer()</c>) không cho phép inject custom code.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultFailureCodes = new()
    {
        ["sqlserver"] = ErrorCodes.HealthCheck.DATABASE_UNREACHABLE,
        ["backend"]   = ErrorCodes.HealthCheck.BACKEND_UNHEALTHY
    };

    public static IServiceCollection AddHealthChecking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();
        var checks = services.AddHealthChecks()
            .AddCheck("backend", () => HealthCheckResult.Healthy("API is running"),
                tags: ["live"]);

        if (DatabaseProviderOptions.IsInMemory(configuration))
        {
            checks.AddCheck("inmemory", () => HealthCheckResult.Healthy("In-memory database is configured"),
                tags: ["db", "ready"]);
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            checks.AddSqlServer(connectionString!,
                name: "sqlserver",
                tags: ["db", "ready"]);
        }

        if (IsEnabled(configuration, "ExternalServices:UseNoOpStorage"))
        {
            checks.AddCheck("storage", () => HealthCheckResult.Healthy("No-op storage is configured"),
                tags: ["storage", "ready"]);
        }
        else
        {
            checks.AddCheck<MinioHealthCheck>("minio",
                tags: ["minio", "storage", "ready"]);
        }

        return services;
    }

    /// <summary>
    /// Đăng ký các health check endpoints cho API.
    /// </summary>
    /// <remarks>
    /// Các endpoints được mount:
    ///
    /// - <c>GET /api/v1/health</c>       — tổng hợp tất cả checks (dashboard/monitoring)
    /// - <c>GET /api/v1/health/live</c>  — liveness probe (Kubernetes). Chỉ kiểm tra backend process.
    /// - <c>GET /api/v1/health/ready</c> — readiness probe (Kubernetes/LB). Kiểm tra SQL Server + MinIO.
    /// - <c>GET /api/v1/health/db</c>    — chỉ SQL Server.
    /// - <c>GET /api/v1/health/minio</c> — chỉ MinIO.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện ở top-level response:
    ///
    /// - <c>null</c>: Tất cả checks Healthy (success = true, HTTP 200).
    /// - <c>DATABASE_UNREACHABLE</c>: SQL Server không kết nối được (timeout/network/credential).
    /// - <c>MINIO_NOT_CONFIGURED</c>: MinIO thiếu cấu hình <c>MinIO:Endpoint</c> — trạng thái Degraded (HTTP 503).
    /// - <c>MINIO_UNREACHABLE</c>: MinIO đã config nhưng không reach được.
    /// - <c>MINIO_BAD_STATUS</c>: MinIO reach được nhưng trả về HTTP status khác 2xx.
    /// - <c>BACKEND_UNHEALTHY</c>: Backend check fail (hiếm khi xảy ra — nếu app die thì response cũng không trả được).
    /// - <c>HEALTH_CHECK_MULTIPLE_FAILURES</c>: Có từ 2 services trở lên cùng fail — xem trong <c>data.checks</c> để lấy code từng service.
    ///
    /// Cấu trúc <c>data</c>:
    /// <code>
    /// {
    ///   "status":        "string  (Healthy | Degraded | Unhealthy)",
    ///   "totalDuration": "string  (hh:mm:ss.fff)",
    ///   "checks": [
    ///     {
    ///       "name":        "string  (backend | sqlserver | minio)",
    ///       "status":      "string  (Healthy | Degraded | Unhealthy)",
    ///       "code":        "string? (error code riêng của check này, null nếu Healthy)",
    ///       "description": "string? (mô tả lỗi cho con người)",
    ///       "duration":    "string  (hh:mm:ss.fff)",
    ///       "error":       "string? (exception message nếu có)"
    ///     }
    ///   ]
    /// }
    /// </code>
    ///
    /// HTTP status: <c>200</c> khi Healthy, <c>503</c> khi Degraded/Unhealthy.
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    public static WebApplication MapHealthCheckEndpoints(this WebApplication app)
    {
        var options = new HealthCheckOptions { ResponseWriter = WriteJsonResponse };

        // /health, /health/db, /health/minio — chỉ admin mới xem được (expose infrastructure info)
        app.MapHealthChecks("/api/v1/health",        options)
           .RequireAuthorization("AdminOnly");
        app.MapHealthChecks("/api/v1/health/db",     new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("db"),
            ResponseWriter = WriteJsonResponse
        }).RequireAuthorization("AdminOnly");
        app.MapHealthChecks("/api/v1/health/minio",  new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("minio"),
            ResponseWriter = WriteJsonResponse
        }).RequireAuthorization("AdminOnly");

        // /live, /ready — public cho Kubernetes liveness/readiness probe
        app.MapHealthChecks("/api/v1/health/live",   new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("live"),
            ResponseWriter = WriteJsonResponse
        }).AllowAnonymous();
        app.MapHealthChecks("/api/v1/health/ready",  new HealthCheckOptions
        {
            Predicate      = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJsonResponse
        }).AllowAnonymous();

        return app;
    }

    private static async Task WriteJsonResponse(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";

        var checks = report.Entries.Select(e => new
        {
            name        = e.Key,
            status      = e.Value.Status.ToString(),
            code        = GetCheckCode(e.Key, e.Value),
            description = e.Value.Description,
            duration    = e.Value.Duration.ToString(@"hh\:mm\:ss\.fff"),
            error       = e.Value.Exception?.Message
        }).ToList();

        var failed = checks.Where(c => c.status != "Healthy").ToList();
        var (message, topCode) = BuildMessageAndCode(report, failed);

        var result = new
        {
            success = report.Status == HealthStatus.Healthy,
            message,
            code   = topCode,
            data   = new
            {
                status        = report.Status.ToString(),
                totalDuration = report.TotalDuration.ToString(@"hh\:mm\:ss\.fff"),
                checks
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

    private static string? GetCheckCode(string name, HealthReportEntry entry)
    {
        if (entry.Status == HealthStatus.Healthy)
            return null;

        if (entry.Data.TryGetValue("code", out var code) && code is string codeStr)
            return codeStr;

        return DefaultFailureCodes.TryGetValue(name, out var defaultCode)
            ? defaultCode
            : "HEALTH_CHECK_FAILED";
    }

    private static (string message, string? code) BuildMessageAndCode<T>(
        HealthReport report,
        List<T> failed) where T : class
    {
        if (report.Status == HealthStatus.Healthy)
            return ("All services healthy", null);

        var failedDetails = report.Entries
            .Where(e => e.Value.Status != HealthStatus.Healthy)
            .Select(e =>
            {
                var detail = e.Value.Description
                          ?? e.Value.Exception?.Message
                          ?? "no details";
                return $"{e.Key} is {e.Value.Status.ToString().ToLower()} ({detail})";
            })
            .ToList();

        var message = failedDetails.Count == 1
            ? failedDetails[0]
            : $"{failedDetails.Count} services affected: {string.Join("; ", failedDetails)}";

        string? topCode;
        if (failedDetails.Count > 1)
        {
            topCode = ErrorCodes.HealthCheck.MULTIPLE_FAILURES;
        }
        else
        {
            var singleFailed = report.Entries.First(e => e.Value.Status != HealthStatus.Healthy);
            topCode = GetCheckCode(singleFailed.Key, singleFailed.Value);
        }

        return (message, topCode);
    }

    private static bool IsEnabled(IConfiguration configuration, string key)
        => bool.TryParse(configuration[key], out var enabled) && enabled;
}
