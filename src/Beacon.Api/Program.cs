using Beacon.Api.Extensions;
using Beacon.Api.Filters;
using Beacon.Api.Middleware;
using Beacon.Application.DependencyInjection;
using Beacon.Infrashtructure.Configuration;
using Beacon.Infrashtructure.Dependencyinjection;
using Beacon.Infrashtructure.DevSeed;
using Beacon.Infrashtructure.Presistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");
var useInMemoryDatabase = DatabaseProviderOptions.IsInMemory(builder.Configuration);

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

var knownProxies = builder.Configuration
    .GetSection("ForwardedHeaders:KnownProxies")
    .Get<string[]>() ?? [];

foreach (var proxy in knownProxies)
{
    if (IPAddress.TryParse(proxy, out var ip))
        forwardedHeadersOptions.KnownProxies.Add(ip);
}

if (int.TryParse(builder.Configuration["ForwardedHeaders:ForwardLimit"], out var forwardLimit))
    forwardedHeadersOptions.ForwardLimit = forwardLimit;

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiAuth(builder.Configuration, builder.Environment);
builder.Services.AddApiSignalR(builder.Configuration);
if (!isTesting && !useInMemoryDatabase)
    builder.Services.AddHangfireJobs(builder.Configuration);
builder.Services.AddSwagger();
builder.Services.AddHealthChecking(builder.Configuration);
builder.Services.AddRateLimiting(builder.Configuration);
builder.Services.AddApiControllers();

var app = builder.Build();

if (!isTesting)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (useInMemoryDatabase)
    {
        await db.Database.EnsureCreatedAsync();

        if (app.Environment.IsDevelopment() && IsEnabled(app.Configuration, "DevSeed:Enabled"))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DevTestDataSeeder>();
            if (IsEnabled(app.Configuration, "DevSeed:ResetOnStartup"))
                await seeder.ResetAsync();
            else
                await seeder.SeedAsync();
        }
    }
    else
    {
        var logger = app.Logger;
        var maxRetries = 5;
        var delaySec = 5;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                db.Database.Migrate();
                logger.LogInformation("Database migration applied successfully.");
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(
                    "Migration attempt {Attempt}/{Max} failed. Retrying in {Delay}s... ({Error})",
                    attempt, maxRetries, delaySec, ex.Message);
                Thread.Sleep(TimeSpan.FromSeconds(delaySec));
                delaySec *= 2;
            }
        }

        try
        {
            const int batchSize = 500;
            var skip = 0;
            var totalUpdated = 0;
            List<Beacon.Domain.Entities.Identity.User> batch;

            do
            {
                batch = db.Users.OrderBy(u => u.Id).Skip(skip).Take(batchSize).ToList();
                var updated = 0;
                foreach (var user in batch)
                {
                    var before = user.SearchIndex;
                    user.UpdateSearchIndex();
                    if (user.SearchIndex != before) updated++;
                }

                if (updated > 0) db.SaveChanges();
                totalUpdated += updated;
                skip += batchSize;
            } while (batch.Count == batchSize);

            if (totalUpdated > 0)
                logger.LogInformation("Re-normalized SearchIndex for {Count} user(s).", totalUpdated);
        }
        catch (Exception ex)
        {
            logger.LogWarning("SearchIndex seeder failed (non-critical): {Error}", ex.Message);
        }
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwaggerDocs();
app.UseHttpsRedirection();
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "AllowSpecificOrigins");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();
app.MapSignalRHubs();
if (!isTesting && !useInMemoryDatabase)
{
    app.MapHangfireDashboard();
    app.RegisterRecurringJobs();
}

static bool IsEnabled(IConfiguration configuration, string key)
    => bool.TryParse(configuration[key], out var enabled) && enabled;

app.Run();

public partial class Program { }
