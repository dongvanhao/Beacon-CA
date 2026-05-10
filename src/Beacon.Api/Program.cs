using Beacon.Api.Extensions;
using Beacon.Api.Middleware;
using Beacon.Application.DependencyInjection;
using Beacon.Infrashtructure.Dependencyinjection;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiAuth(builder.Configuration, builder.Environment);
builder.Services.AddApiSignalR(builder.Configuration);
builder.Services.AddSwagger();
builder.Services.AddHealthChecking(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

// Auto-apply pending EF Core migrations on startup (retry cho Docker — SQL Server khởi động chậm hơn API)
// Skip khi environment = "Testing" (integration tests dùng InMemory DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = app.Logger;
    var maxRetries = 5;
    var delaySec   = 5;

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

    // Re-normalize SearchIndex cho toàn bộ user bằng C# StringNormalizer.
    // Cần thiết vì SQL migration không thể xử lý đúng ký tự Đ/đ (encoding issue).
    // Batch 500 để tránh OOM khi prod có nhiều user.
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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwaggerDocs();
app.UseHttpsRedirection();
app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();
app.MapSignalRHubs();

app.Run();

public partial class Program { }
