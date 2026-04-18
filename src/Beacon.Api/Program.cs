using Beacon.Api.Authorization;
using Beacon.Api.HealthChecks;
using Beacon.Api.Middleware;
using Beacon.Api.Services;
using Beacon.Application;
using Beacon.Application.Common.Behaviors;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Infrashtructure.Presistence;
using Beacon.Infrashtructure.Repository.Identity;
using Microsoft.AspNetCore.Authorization;
using Beacon.Infrashtructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

//  Health Check
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks()
    .AddCheck("backend", () => HealthCheckResult.Healthy("API is running"),
        tags: ["live"])
    .AddSqlServer(
        connectionString: connectionString!,
        name: "sqlserver",
        tags: ["db", "ready"])
    .AddCheck<MinioHealthCheck>("minio",
        tags: ["minio", "storage", "ready"]);
// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập access token. Ví dụ: eyJhbGci..."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

//  Current user
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

//  MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

//  FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

//  Auth — Repository + Service
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IJwtService, JwtService>();

//  Authorization
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));
});


//  JWT Bearer
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

//  CORS — cho phép tất cả origin, method, header (*)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-apply pending EF Core migrations on startup (retry cho Docker — SQL Server khởi động chậm hơn API)
// Seed data (SuperAdmin, Roles, Permissions) đã được nhúng trong migration Seed_SuperAdmin
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
            delaySec *= 2; // exponential backoff: 5s → 10s → 20s → 40s
        }
    }
}

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

//  Health Check endpoints
static async Task WriteJsonResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";

    var isHealthy = report.Status == HealthStatus.Healthy;
    var message   = report.Status switch
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
        code    = isHealthy ? (string?)null : "HEALTH_CHECK_FAILED",
        data    = new
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
        JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
        ctx.RequestAborted);
}

var healthOptions = new HealthCheckOptions { ResponseWriter = WriteJsonResponse };

app.MapHealthChecks("/health", healthOptions);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate      = check => check.Tags.Contains("live"),
    ResponseWriter = WriteJsonResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate      = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteJsonResponse
});

app.MapHealthChecks("/health/db", new HealthCheckOptions
{
    Predicate      = check => check.Tags.Contains("db"),
    ResponseWriter = WriteJsonResponse
});

app.MapHealthChecks("/health/minio", new HealthCheckOptions
{
    Predicate      = check => check.Tags.Contains("minio"),
    ResponseWriter = WriteJsonResponse
});

app.Run();