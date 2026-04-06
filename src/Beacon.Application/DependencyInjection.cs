using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Beacon.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddUseCasesByConvention();
            return services;
        }

        private static IServiceCollection AddUseCasesByConvention(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var useCaseTypes = assembly
                .GetTypes()
                .Where(type => type.IsClass
                    && !type.IsAbstract
                    && type.Name.EndsWith("UseCase", StringComparison.Ordinal));

            foreach (var type in useCaseTypes)
            {
                services.AddScoped(type);
            }

            return services;
        }
    }
}
