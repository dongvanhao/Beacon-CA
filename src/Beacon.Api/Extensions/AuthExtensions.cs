using Beacon.Api.Authorization;
using Beacon.Api.Services;
using Beacon.Application.Common.Interfaces.IService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
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
            });

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));
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
