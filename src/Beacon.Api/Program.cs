using Beacon.Api.Extensions;
using Beacon.Api.Middleware;
using Beacon.Application.DependencyInjection;
using Beacon.Infrashtructure.Dependencyinjection;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddApiAuth(builder.Configuration);
builder.Services.AddSwagger();
builder.Services.AddHealthChecking(builder.Configuration);
builder.Services.AddControllers();

var app = builder.Build();

// Auto-apply pending EF Core migrations on startup (retry cho Docker — SQL Server khởi động chậm hơn API)
using (var scope = app.Services.CreateScope())
{
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
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwaggerDocs();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthCheckEndpoints();

app.Run();
