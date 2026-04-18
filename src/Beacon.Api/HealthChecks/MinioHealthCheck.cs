using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Beacon.Api.HealthChecks;

public class MinioHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public MinioHealthCheck(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["MinIO:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
            return HealthCheckResult.Degraded("MinIO not configured yet");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            // MinIO health endpoint — available on all MinIO instances
            var response = await client.GetAsync($"{endpoint.TrimEnd('/')}/minio/health/live", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("MinIO is reachable")
                : HealthCheckResult.Unhealthy($"MinIO returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"MinIO unreachable: {ex.Message}");
        }
    }
}
