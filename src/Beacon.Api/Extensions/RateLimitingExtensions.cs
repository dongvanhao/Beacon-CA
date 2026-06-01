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
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

                // Global concurrency limiter — uses TaggingConcurrencyLimiter to mark rejections
                // so OnRejected can distinguish 503 (concurrency) from 429 (rate limit)
                limiterOpts.GlobalLimiter = new TaggingConcurrencyLimiter(
                    opts.Global.ConcurrencyLimit,
                    opts.Global.QueueLimit);

                limiterOpts.OnRejected = async (context, ct) =>
                {
                    bool isConcurrency = context.HttpContext.Items.ContainsKey("_globalConcurrencyRejected");

                    context.HttpContext.Response.ContentType = "application/json";

                    if (isConcurrency)
                    {
                        context.HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                        var body = ApiResponse<object>.FailureResponse(
                            "Máy chủ đang bận. Vui lòng thử lại sau.",
                            ErrorCodes.RateLimit.SERVER_BUSY);

                        await context.HttpContext.Response.WriteAsync(
                            JsonSerializer.Serialize(body, _jsonOptions), ct);
                    }
                    else
                    {
                        // SlidingWindowLimiter with QueueLimit=0 returns no RetryAfter metadata — use 60s fallback
                        context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter);
                        var retrySeconds = retryAfter > TimeSpan.Zero ? (int)retryAfter.TotalSeconds : 60;

                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.HttpContext.Response.Headers.RetryAfter = retrySeconds.ToString();

                        var body = ApiResponse<object>.FailureResponse(
                            "Quá nhiều yêu cầu. Vui lòng thử lại sau.",
                            ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED,
                            [$"Retry after {retrySeconds} seconds"]);

                        await context.HttpContext.Response.WriteAsync(
                            JsonSerializer.Serialize(body, _jsonOptions), ct);
                    }
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

/// <summary>
/// Wraps a ConcurrencyLimiter and tags HttpContext.Items when a request is rejected,
/// so OnRejected can distinguish concurrency rejection (503) from rate-limit rejection (429).
/// </summary>
internal sealed class TaggingConcurrencyLimiter : PartitionedRateLimiter<HttpContext>
{
    private readonly PartitionedRateLimiter<HttpContext> _inner;

    internal TaggingConcurrencyLimiter(int permitLimit, int queueLimit)
    {
        _inner = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter(
                partitionKey: "global",
                factory: _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = permitLimit,
                    QueueLimit  = queueLimit,
                }));
    }

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        var lease = _inner.AttemptAcquire(resource, permitCount);
        if (!lease.IsAcquired)
            resource.Items["_globalConcurrencyRejected"] = true;
        return lease;
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource, int permitCount, CancellationToken cancellationToken)
    {
        var lease = await _inner.AcquireAsync(resource, permitCount, cancellationToken);
        if (!lease.IsAcquired)
            resource.Items["_globalConcurrencyRejected"] = true;
        return lease;
    }

    public override RateLimiterStatistics? GetStatistics(HttpContext resource)
        => _inner.GetStatistics(resource);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
