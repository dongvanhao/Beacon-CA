using Beacon.Api.Options;
using Beacon.Shared.Common.Responses;
using Beacon.Shared.Constants;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace Beacon.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Lazy binding — resolved at first request, AFTER all config sources (incl. test PostConfigure) applied
        services.Configure<RateLimitingOptions>(configuration.GetSection("RateLimiting"));

        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>>((limiterOpts, appOpts) =>
            {
                var opts = appOpts.Value;
                if (!opts.Enabled) return;

                // Auth endpoints — per-IP sliding window
                limiterOpts.AddPolicy("auth-login", ctx =>
                    PerIpSlidingWindow(ctx, "auth-login",
                        opts.Auth.LoginPermitLimit,
                        TimeSpan.FromMinutes(opts.Auth.LoginWindowMinutes)));

                limiterOpts.AddPolicy("auth-admin-login", ctx =>
                    PerIpSlidingWindow(ctx, "auth-admin-login",
                        opts.Auth.AdminLoginPermitLimit,
                        TimeSpan.FromMinutes(opts.Auth.AdminLoginWindowMinutes)));

                limiterOpts.AddPolicy("auth-register", ctx =>
                    PerIpSlidingWindow(ctx, "auth-register",
                        opts.Auth.RegisterPermitLimit,
                        TimeSpan.FromHours(opts.Auth.RegisterWindowHours)));

                limiterOpts.AddPolicy("auth-refresh-token", ctx =>
                    PerIpSlidingWindow(ctx, "auth-refresh-token",
                        opts.Auth.RefreshTokenPermitLimit,
                        TimeSpan.FromMinutes(opts.Auth.RefreshTokenWindowMinutes)));

                limiterOpts.AddPolicy("auth-check", ctx =>
                    PerIpSlidingWindow(ctx, "auth-check",
                        opts.Auth.CheckEmailPermitLimit,
                        TimeSpan.FromSeconds(opts.Auth.CheckEmailWindowSeconds)));

                // General API — token bucket per user (authenticated) or per IP (anonymous)
                limiterOpts.AddPolicy("api-general", ctx =>
                {
                    var user   = ctx.User;
                    bool isAdmin = user.HasClaim("actor", "admin") || user.IsInRole("SuperAdmin");

                    if (user.Identity?.IsAuthenticated == true)
                    {
                        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                     ?? user.FindFirst("sub")?.Value
                                     ?? "anonymous";

                        int permitLimit = isAdmin ? opts.Api.AdminPermitLimit : opts.Api.AuthenticatedPermitLimit;
                        int burst       = isAdmin ? opts.Api.AdminBurst       : opts.Api.AuthenticatedBurst;

                        return RateLimitPartition.GetTokenBucketLimiter(
                            partitionKey: $"user:{userId}",
                            factory: _ => new TokenBucketRateLimiterOptions
                            {
                                TokenLimit          = permitLimit + burst,
                                ReplenishmentPeriod = TimeSpan.FromMinutes(opts.Api.AuthenticatedWindowMinutes),
                                TokensPerPeriod     = permitLimit,
                                AutoReplenishment   = true,
                                QueueLimit          = 0,
                            });
                    }

                    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: $"ip:{ip}",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit       = opts.Api.UnauthenticatedPermitLimit,
                            Window            = TimeSpan.FromMinutes(opts.Api.UnauthenticatedWindowMinutes),
                            SegmentsPerWindow = 3,
                            QueueLimit        = 0,
                        });
                });

                // Global concurrency limiter
                limiterOpts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                    RateLimitPartition.GetConcurrencyLimiter(
                        partitionKey: "global",
                        factory: _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = opts.Global.ConcurrencyLimit,
                            QueueLimit  = opts.Global.QueueLimit,
                        }));

                // Always 429 for named-policy rejections.
                // SlidingWindowLimiter with QueueLimit=0 returns no RetryAfter metadata — use 60s fallback.
                limiterOpts.OnRejected = async (context, ct) =>
                {
                    context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
                    var retrySeconds = retryAfter > TimeSpan.Zero ? (int)retryAfter.TotalSeconds : 60;

                    context.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString();

                    var body = ApiResponse<object>.FailureResponse(
                        "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
                        ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED,
                        [$"Retry after {retrySeconds} seconds"]);

                    await context.HttpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(body,
                            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                        ct);
                };
            });

        return services;
    }

    private static RateLimitPartition<string> PerIpSlidingWindow(
        HttpContext ctx, string policyPrefix, int permitLimit, TimeSpan window)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: $"{policyPrefix}:{ip}",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit       = permitLimit,
                Window            = window,
                SegmentsPerWindow = 3,
                QueueLimit        = 0,
            });
    }
}
