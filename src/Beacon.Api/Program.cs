using Beacon.Api.Authorization;
using Beacon.Api.Middleware;
using Beacon.Api.Services;
using Beacon.Application;
using Beacon.Application.Common.Behaviors;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Beacon.Domain.Entities.Identity;
using Beacon.Infrashtructure.Presistence;
using Beacon.Infrashtructure.Repository.Identity;
using Microsoft.AspNetCore.Authorization;
using Beacon.Infrashtructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
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
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IJwtService, JwtService>();

//  Authorization
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));

    options.AddPolicy("users:read",    p => p.AddRequirements(new PermissionRequirement("users:read")));
    options.AddPolicy("users:write",   p => p.AddRequirements(new PermissionRequirement("users:write")));
    options.AddPolicy("users:delete",  p => p.AddRequirements(new PermissionRequirement("users:delete")));
    options.AddPolicy("admins:manage", p => p.AddRequirements(new PermissionRequirement("admins:manage")));
    options.AddPolicy("roles:manage",  p => p.AddRequirements(new PermissionRequirement("roles:manage")));
    options.AddPolicy("safety:read",   p => p.AddRequirements(new PermissionRequirement("safety:read")));
    options.AddPolicy("safety:write",  p => p.AddRequirements(new PermissionRequirement("safety:write")));
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
            delaySec *= 2; // exponential backoff: 5s → 10s → 20s → 40s
        }
    }

    // Seed SuperAdmin — chỉ chạy khi DB chưa có Admin nào
    if (!db.Admins.Any())
    {
        var seedEmail    = app.Configuration["SeedAdmin:Email"]
            ?? throw new InvalidOperationException("SeedAdmin:Email is not configured. Set via appsettings or environment variable SeedAdmin__Email.");
        var seedPassword = app.Configuration["SeedAdmin:Password"]
            ?? throw new InvalidOperationException("SeedAdmin:Password is not configured. Set via appsettings or environment variable SeedAdmin__Password.");

        // 1. Tạo danh sách permissions chuẩn
        var permissions = new[]
        {
            Permission.Create("users:read",    "View user list",             "Users"),
            Permission.Create("users:write",   "Create/update users",        "Users"),
            Permission.Create("users:delete",  "Delete users",               "Users"),
            Permission.Create("admins:manage", "Manage admin accounts",      "Admin"),
            Permission.Create("roles:manage",  "Manage roles & permissions", "Admin"),
            Permission.Create("safety:read",   "View safety records",        "Safety"),
            Permission.Create("safety:write",  "Modify safety records",      "Safety"),
        };
        db.Permissions.AddRange(permissions);

        // 2. Tạo role SuperAdmin
        var superAdminRole = Role.Create("SuperAdmin", "Full system access");
        db.Roles.Add(superAdminRole);

        // 3. Gán tất cả permissions vào role
        foreach (var p in permissions)
            db.RolePermissions.Add(RolePermission.Create(superAdminRole.Id, p.Id));

        // 4. Tạo Admin với password được hash
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword);
        var admin = Admin.Create(seedEmail, passwordHash, "Super Admin");
        db.Admins.Add(admin);

        // 5. Gán role SuperAdmin cho admin
        db.AdminRoles.Add(AdminRole.Create(admin.Id, superAdminRole.Id));

        db.SaveChanges();
        logger.LogInformation("SuperAdmin seeded successfully: {Email}", seedEmail);
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();