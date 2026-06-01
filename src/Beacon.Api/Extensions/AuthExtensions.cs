using Beacon.Api.Authorization;
using Beacon.Api.Services;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Beacon.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddApiAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        var jwtSettings = configuration.GetSection("JwtSettings");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)),
                    ValidateIssuer    = true,
                    ValidIssuer       = jwtSettings["Issuer"],
                    ValidateAudience  = true,
                    ValidAudience     = jwtSettings["Audience"],
                    ValidateLifetime  = true,
                    ClockSkew         = TimeSpan.Zero
                };

                // WebSocket connections cannot set Authorization header —
                // SignalR clients pass the token as ?access_token= query param.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.FindFirst("actor")?.Value != "admin")
                            return;

                        var adminIdClaim = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        if (!Guid.TryParse(adminIdClaim, out var adminId))
                        {
                            context.Fail("Invalid admin token.");
                            return;
                        }

                        var adminRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IAdminRepository>();
                        var admin = await adminRepository.GetByIdWithRolesAsync(
                            adminId,
                            context.HttpContext.RequestAborted);
                        if (admin is null || !admin.IsActive)
                        {
                            context.Fail("Invalid admin token.");
                            return;
                        }

                        var activeRoles = admin.AdminRoles
                            .Where(ar => ar.Role.IsActive)
                            .Select(ar => ar.Role.Name)
                            .ToHashSet(StringComparer.Ordinal);
                        var currentPermissions = admin.AdminRoles
                            .Where(ar => ar.Role.IsActive)
                            .SelectMany(ar => ar.Role.RolePermissions)
                            .Select(rp => rp.Permission.Name)
                            .ToHashSet(StringComparer.Ordinal);

                        var tokenRoles = context.Principal.FindAll(ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToHashSet(StringComparer.Ordinal);
                        var tokenPermissions = context.Principal.FindAll("permission")
                            .Select(c => c.Value)
                            .ToHashSet(StringComparer.Ordinal);

                        if (!activeRoles.SetEquals(tokenRoles) ||
                            !currentPermissions.SetEquals(tokenPermissions))
                        {
                            context.Fail("Admin token permissions are stale.");
                        }
                    }
                };
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));
            options.AddPolicy("SuperAdminOnly", p => p.RequireClaim("actor", "admin").RequireRole("SuperAdmin"));
        });

        services.AddCors(options =>
        {
            // Dev: mở tất cả origin cho dễ debug
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });

            // Prod: chỉ whitelist origin cụ thể từ config Cors:AllowedOrigins
            var allowedOrigins = configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            options.AddPolicy("AllowSpecificOrigins", policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                else
                    policy.SetIsOriginAllowed(_ => false); // fail-closed nếu chưa config
            });
        });

        return services;
    }
}
