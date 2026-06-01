using Microsoft.Extensions.Configuration;

namespace Beacon.Infrashtructure.Configuration;

public static class DatabaseProviderOptions
{
    public const string SqlServer = "SqlServer";
    public const string InMemory = "InMemory";

    public static string ResolveProvider(IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"];
        return string.IsNullOrWhiteSpace(provider)
            ? SqlServer
            : provider.Trim();
    }

    public static bool IsInMemory(IConfiguration configuration)
        => string.Equals(ResolveProvider(configuration), InMemory, StringComparison.OrdinalIgnoreCase);

    public static bool IsSqlServer(IConfiguration configuration)
        => string.Equals(ResolveProvider(configuration), SqlServer, StringComparison.OrdinalIgnoreCase);
}
