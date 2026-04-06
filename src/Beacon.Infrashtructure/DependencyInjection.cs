using System.Reflection;
using System.Text;
using Beacon.Application.Common.Options;
using Beacon.Infrashtructure.Presistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Beacon.Infrashtructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

            var jwtOptionsSection = configuration.GetSection(JwtOptions.SectionName);
            services.Configure<JwtOptions>(jwtOptionsSection);

            var jwtOptions = jwtOptionsSection.Get<JwtOptions>()
                ?? throw new InvalidOperationException("Jwt configuration section is missing.");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                });

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddHttpContextAccessor();
            services.AddScopedByConvention();

            return services;
        }

        private static IServiceCollection AddScopedByConvention(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var implementationTypes = assembly
                .GetTypes()
                .Where(type => type.IsClass
                    && !type.IsAbstract
                    && (type.Name.EndsWith("Repository", StringComparison.Ordinal)
                        || type.Name.EndsWith("Service", StringComparison.Ordinal)));

            foreach (var implementationType in implementationTypes)
            {
                var serviceInterfaces = implementationType
                    .GetInterfaces()
                    .Where(i => i.Namespace != null
                        && i.Namespace.StartsWith("Beacon.Application.Common.Interfaces", StringComparison.Ordinal));

                foreach (var serviceInterface in serviceInterfaces)
                {
                    services.AddScoped(serviceInterface, implementationType);
                }
            }

            return services;
        }
    }
}
