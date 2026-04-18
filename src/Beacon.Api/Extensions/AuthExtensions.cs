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
        IConfiguration configuration)
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
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", p => p.RequireClaim("actor", "admin"));
        });

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        return services;
    }
}
