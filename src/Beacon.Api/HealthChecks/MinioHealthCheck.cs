using Beacon.Shared.Constants;
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
            return HealthCheckResult.Degraded(
                "MinIO not configured yet",
                data: new Dictionary<string, object> { ["code"] = ErrorCodes.HealthCheck.MINIO_NOT_CONFIGURED });

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            var response = await client.GetAsync($"{endpoint.TrimEnd('/')}/minio/health/live", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("MinIO is reachable")
                : HealthCheckResult.Unhealthy(
                    $"MinIO returned {(int)response.StatusCode}",
                    data: new Dictionary<string, object> { ["code"] = ErrorCodes.HealthCheck.MINIO_BAD_STATUS });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                $"MinIO unreachable: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object> { ["code"] = ErrorCodes.HealthCheck.MINIO_UNREACHABLE });
        }
    }
}
