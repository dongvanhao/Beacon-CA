using System.Net;
using Beacon.Infrashtructure.DevSeed;
using Beacon.Infrashtructure.Presistence;
using Beacon.IntergrationTests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Beacon.IntergrationTests.Dev;

public class DevTestDataControllerTests
{
    private const string ResetToken = "local-test-reset-token";

    [Fact]
    public async Task Reset_returns_success_and_reseeds_in_development_inmemory()
    {
        await using var factory = new DevTestDataFactory(
            environment: "Development",
            databaseProvider: "InMemory",
            devSeedEnabled: true,
            resetToken: ResetToken);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/test-data/reset");
        request.Headers.Add("X-Dev-Seed-Token", ResetToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync()).Should().Be(2);
        (await db.Users.AnyAsync(u => u.Username == DevTestDataSeeder.SeedLoginUsername)).Should().BeTrue();
        (await db.Friends.CountAsync()).Should().Be(1);
        (await db.SafetySettings.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Reset_rejects_invalid_token()
    {
        await using var factory = new DevTestDataFactory(
            environment: "Development",
            databaseProvider: "InMemory",
            devSeedEnabled: true,
            resetToken: ResetToken);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/test-data/reset");
        request.Headers.Add("X-Dev-Seed-Token", "wrong-token");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reset_is_not_available_outside_development()
    {
        await using var factory = new DevTestDataFactory(
            environment: "Production",
            databaseProvider: "InMemory",
            devSeedEnabled: true,
            resetToken: ResetToken);
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dev/test-data/reset");
        request.Headers.Add("X-Dev-Seed-Token", ResetToken);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await using var factory = new DevTestDataFactory(
            environment: "Development",
            databaseProvider: "InMemory",
            devSeedEnabled: true,
            resetToken: ResetToken);
        factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevTestDataSeeder>();
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync(u => u.Username == DevTestDataSeeder.SeedLoginUsername)).Should().Be(1);
        (await db.Friends.CountAsync()).Should().Be(1);
    }

    private sealed class DevTestDataFactory(
        string environment,
        string databaseProvider,
        bool devSeedEnabled,
        string resetToken) : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"BeaconDevTest-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = databaseProvider,
                    ["Database:InMemoryName"] = _dbName,
                    ["DevSeed:Enabled"] = devSeedEnabled.ToString(),
                    ["DevSeed:ResetOnStartup"] = "false",
                    ["DevSeed:ResetToken"] = resetToken,
                    ["ExternalServices:UseNoOpStorage"] = "true",
                    ["JwtSettings:SecretKey"] = TestJwtSettings.SecretKey,
                    ["JwtSettings:Issuer"] = TestJwtSettings.Issuer,
                    ["JwtSettings:Audience"] = TestJwtSettings.Audience,
                    ["JwtSettings:ExpiryMinutes"] = "15",
                    ["JwtSettings:RefreshTokenExpiryDays"] = "7",
                    ["ConnectionStrings:DefaultConnection"] = "Server=test;Database=test"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.RemoveAll(typeof(IHostedService));
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName));
            });
        }
    }
}
