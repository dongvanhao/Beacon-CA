using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Beacon.Infrashtructure.Services.Storage;

public class MinioBucketInitializer : IHostedService
{
    private readonly IMinioClient _minio;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MinioBucketInitializer> _logger;

    public MinioBucketInitializer(
        IMinioClient minio,
        IConfiguration configuration,
        ILogger<MinioBucketInitializer> logger)
    {
        _minio = minio;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucketName = _configuration["MinIO:BucketName"];
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            _logger.LogWarning("[MinIO] BucketName not configured, skipping bucket initialization.");
            return;
        }

        try
        {
            var exists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucketName),
                cancellationToken);

            if (!exists)
            {
                await _minio.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucketName),
                    cancellationToken);
                _logger.LogInformation("[MinIO] bucket '{Bucket}' created.", bucketName);
            }
            else
            {
                _logger.LogInformation("[MinIO] bucket '{Bucket}' ready.", bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MinIO] Failed to initialize bucket '{Bucket}'. Storage endpoints may fail until MinIO is reachable.", bucketName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
