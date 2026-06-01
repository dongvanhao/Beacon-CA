using Beacon.Api.Options;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.IntergrationTests.Common;
using Beacon.Shared.Common.Responses;
using Beacon.Shared.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using System.Net;
using System.Net.Http.Json;

namespace Beacon.IntergrationTests.Identity;

/// <summary>
/// Factory riêng cho rate limiting tests.
/// Dùng PostConfigure&lt;RateLimitingOptions&gt; trong ConfigureServices để override limits với giá trị thấp.
/// RateLimiterOptions được resolve lazy (khi request đầu tiên đến), nên PostConfigure chạy trước khi policies được build.
/// Mỗi test tạo factory riêng để reset trạng thái rate limiter.
/// </summary>
public class RateLimitingWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"BeaconRateLimitDb-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<global::Beacon.Infrashtructure.Presistence.AppDbContext>>();
            services.RemoveAll<global::Beacon.Infrashtructure.Presistence.AppDbContext>();

            var dbName = _dbName;
            services.AddDbContext<global::Beacon.Infrashtructure.Presistence.AppDbContext>(opts =>
                opts.UseInMemoryDatabase(dbName));

            services.RemoveAll<IMinioClient>();
            services.RemoveAll<IStorageService>();
            services.RemoveAll<IImageProcessor>();
            services.RemoveAll(typeof(IHostedService));
            services.AddScoped<IStorageService, NoOpStorageService>();

            services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TokenValidationParameters.IssuerSigningKey =
                        new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                            System.Text.Encoding.UTF8.GetBytes(TestJwtSettings.SecretKey));
                    options.TokenValidationParameters.ValidIssuer   = TestJwtSettings.Issuer;
                    options.TokenValidationParameters.ValidAudience = TestJwtSettings.Audience;
                });

            // Override RateLimitingOptions với giá trị thấp — chạy sau tất cả Configure callbacks
            // RateLimiterOptions được build lazy (request đầu tiên), nên PostConfigure này có hiệu lực
            services.PostConfigure<RateLimitingOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Auth.LoginPermitLimit          = 3;
                opts.Auth.LoginWindowMinutes         = 1;
                opts.Auth.RegisterPermitLimit        = 3;
                opts.Auth.RegisterWindowHours        = 1;
                opts.Auth.RefreshTokenPermitLimit    = 3;
                opts.Auth.RefreshTokenWindowMinutes  = 1;
                opts.Auth.CheckEmailPermitLimit      = 3;
                opts.Auth.CheckEmailWindowSeconds    = 60;
                opts.Auth.CheckPhonePermitLimit      = 3;
                opts.Auth.CheckPhoneWindowSeconds    = 60;
                opts.Api.AuthenticatedPermitLimit    = 5;
                opts.Api.AuthenticatedWindowMinutes  = 1;
                opts.Api.AuthenticatedBurst          = 0;
                opts.Api.UnauthenticatedPermitLimit  = 5;
                opts.Api.UnauthenticatedWindowMinutes= 1;
                opts.Global.ConcurrencyLimit         = 1000;
                opts.Global.QueueLimit               = 0;
            });
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"]              = TestJwtSettings.SecretKey,
                ["JwtSettings:Issuer"]                 = TestJwtSettings.Issuer,
                ["JwtSettings:Audience"]               = TestJwtSettings.Audience,
                ["JwtSettings:ExpiryMinutes"]          = "15",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7",
                ["ConnectionStrings:DefaultConnection"] = "Server=test;Database=test",
                ["MinIO:Endpoint"]   = "localhost:9000",
                ["MinIO:AccessKey"]  = "test",
                ["MinIO:SecretKey"]  = "test",
                ["MinIO:BucketName"] = "test",
                ["MinIO:UseSSL"]     = "false",
            });
        });
    }
}

public class RateLimitingTests
{
    // UC1-T1: Login vượt limit → 429 với RATE_LIMIT_EXCEEDED + Retry-After header
    [Fact]
    public async Task Login_WhenExceedLimit_Returns429()
    {
        await using var factory = new RateLimitingWebApplicationFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var r = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, $"lần {i + 1} chưa vượt limit");
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await limited.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    // UC1-T2: Fresh factory = fresh quota — hai factories độc lập nhau
    [Fact]
    public async Task Login_FreshFactory_HasFreshQuota()
    {
        await using var factoryA = new RateLimitingWebApplicationFactory();
        var clientA = factoryA.CreateClient();
        for (var i = 0; i < 3; i++)
            await clientA.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });

        var limitedA = await clientA.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
        limitedA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Factory B: quota chưa bị ảnh hưởng bởi factory A
        await using var factoryB = new RateLimitingWebApplicationFactory();
        var clientB = factoryB.CreateClient();
        var responseB = await clientB.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
        responseB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
    }

    // UC1-T3: check-email vượt limit → 429
    [Fact]
    public async Task CheckEmail_WhenExceedLimit_Returns429()
    {
        await using var factory = new RateLimitingWebApplicationFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.GetAsync("/api/v1/auth/check-email?email=test@test.com");
            ok.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await client.GetAsync("/api/v1/auth/check-email?email=test@test.com");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await limited.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be(ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    // UC1-T4: check-phone vượt limit → 429
    [Fact]
    public async Task CheckPhone_WhenExceedLimit_Returns429()
    {
        await using var factory = new RateLimitingWebApplicationFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var ok = await client.GetAsync("/api/v1/auth/check-phone?phoneNumber=%2B84901234567");
            ok.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        var limited = await client.GetAsync("/api/v1/auth/check-phone?phoneNumber=%2B84901234567");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await limited.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be(ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED);
        limited.Headers.Contains("Retry-After").Should().BeTrue();
    }

    // UC2-T1: Authenticated user vượt general limit → 429
    [Fact]
    public async Task AuthenticatedUser_WhenExceedGeneralLimit_Returns429()
    {
        await using var factory = new RateLimitingWebApplicationFactory();
        var client = factory.CreateClient();
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", TokenHelper.GenerateUserToken(userId, "testuser"));

        // Gọi 5 lần (= AuthenticatedPermitLimit + burst = 5 + 0 = 5 tokens)
        for (var i = 0; i < 5; i++)
        {
            var ok = await client.GetAsync("/api/v1/auth/me");
            ok.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, $"lần {i + 1} chưa vượt limit");
        }

        var limited = await client.GetAsync("/api/v1/auth/me");
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var body = await limited.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Code.Should().Be(ErrorCodes.RateLimit.RATE_LIMIT_EXCEEDED);
    }

    // UC3-T2: Enabled = false → tất cả request pass through (no 429)
    [Fact]
    public async Task RateLimiting_WhenDisabled_AllRequestsPassThrough()
    {
        await using var factory = new DisabledRateLimitingWebApplicationFactory();
        var client = factory.CreateClient();

        // Gọi 10 lần — vượt quá giới hạn thấp (3) của RateLimitingWebApplicationFactory
        // Nhưng với Enabled=false, tất cả đều pass
        for (var i = 0; i < 10; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
            ok.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, $"rate limiting disabled, lần {i + 1} không được là 429");
        }
    }
}

/// <summary>Factory với rate limiting disabled để test UC3-T2.</summary>
public class DisabledRateLimitingWebApplicationFactory : RateLimitingWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<RateLimitingOptions>(opts =>
            {
                opts.Enabled = false;
            });
        });
    }
}

/// <summary>Factory với ConcurrencyLimit=1 để test UC3-T1 (503 khi server bận).</summary>
public class ConcurrencyLimitedWebApplicationFactory : RateLimitingWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<RateLimitingOptions>(opts =>
            {
                opts.Global.ConcurrencyLimit = 1;
                opts.Global.QueueLimit       = 0;
            });
        });
    }
}

public class RateLimitingSlice3Tests
{
    // UC3-T1: Global concurrency exceeded → 503 SERVER_BUSY (không phải 429)
    [Fact]
    public async Task GlobalConcurrency_WhenExceeded_Returns503()
    {
        await using var factory = new ConcurrencyLimitedWebApplicationFactory();
        var client = factory.CreateClient();

        // Gửi 10 request song song — với ConcurrencyLimit=1, các request trùng nhau sẽ nhận 503
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => client.PostAsJsonAsync("/api/v1/auth/login",
                new { username = "u", password = "p" }))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var busyResponses = responses
            .Where(r => r.StatusCode == HttpStatusCode.ServiceUnavailable)
            .ToList();

        busyResponses.Should().NotBeEmpty("ConcurrencyLimit=1 với 10 concurrent request phải reject ít nhất một");

        var body = await busyResponses.First().Content.ReadFromJsonAsync<ApiResponse<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ErrorCodes.RateLimit.SERVER_BUSY);
        busyResponses.First().Headers.Contains("Retry-After").Should().BeFalse("503 không có Retry-After");
    }

    // UC3-T2 đã có trong RateLimitingTests — Enabled=false pass through (duplicate guard)
}

public class RateLimitingSlice4Tests
{
    // UC5-T1: X-Forwarded-For tạo partition key độc lập cho mỗi IP
    [Fact]
    public async Task Login_DifferentXForwardedForIPs_HaveIndependentQuota()
    {
        await using var factory = new RateLimitingWebApplicationFactory();

        var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.1");

        var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Forwarded-For", "10.0.0.2");

        // Exhaust clientA quota (3 = LoginPermitLimit trong test factory)
        for (var i = 0; i < 3; i++)
            await clientA.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });

        var limitedA = await clientA.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
        limitedA.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "clientA đã vượt quota");

        // clientB dùng IP khác — quota hoàn toàn độc lập
        var okB = await clientB.PostAsJsonAsync("/api/v1/auth/login", new { username = "u", password = "p" });
        okB.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
            "clientB có partition key khác (10.0.0.2 vs 10.0.0.1)");
    }
}
