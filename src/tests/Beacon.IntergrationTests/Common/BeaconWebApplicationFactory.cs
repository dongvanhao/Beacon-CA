using Beacon.Application.Common.Interfaces.IService;
using Beacon.Infrashtructure.Presistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Minio;

namespace Beacon.IntergrationTests.Common;

public class BeaconWebApplicationFactory : WebApplicationFactory<Program>
{
    // Unique per factory instance — captured once so all scopes share the same InMemory DB
    private readonly string _dbName = $"BeaconTestDb-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove SQL Server DbContext registration
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            // All scopes share same InMemory DB (captured name, not re-evaluated)
            var dbName = _dbName;
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            // Remove MinIO services to avoid startup errors
            services.RemoveAll<IMinioClient>();
            services.RemoveAll<IStorageService>();
            services.RemoveAll<IImageProcessor>();
            services.RemoveAll(typeof(IHostedService));

            // Replace storage with no-op stub
            services.AddScoped<IStorageService, NoOpStorageService>();

            // Override JWT validation to use test key
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

public static class TestJwtSettings
{
    public const string SecretKey = "beacon-integration-test-secret-key-32chars!!";
    public const string Issuer    = "beacon-test";
    public const string Audience  = "beacon-test";
}
