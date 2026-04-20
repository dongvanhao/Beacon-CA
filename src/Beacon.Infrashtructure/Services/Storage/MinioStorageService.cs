using Beacon.Application.Common.Interfaces.IService;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace Beacon.Infrashtructure.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minio;
    private readonly int _presignedUrlExpirySeconds;

    public string BucketName { get; }

    public MinioStorageService(IMinioClient minio, IConfiguration configuration)
    {
        _minio = minio;
        BucketName = configuration["MinIO:BucketName"]
            ?? throw new InvalidOperationException("MinIO:BucketName is not configured.");
        _presignedUrlExpirySeconds = configuration.GetValue<int?>("MinIO:PresignedUrlExpirySeconds") ?? 900;
    }

    public async Task<StorageUploadResult> UploadAsync(
        Stream data,
        long size,
        string objectKey,
        string contentType,
        CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType);

        var response = await _minio.PutObjectAsync(args, ct);
        return new StorageUploadResult(response.Etag, response.Size);
    }

    public async Task<string> GeneratePresignedGetUrlAsync(string objectKey, CancellationToken ct = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey)
            .WithExpiry(_presignedUrlExpirySeconds);

        return await _minio.PresignedGetObjectAsync(args);
    }

    public async Task RemoveAsync(string objectKey, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey);

        await _minio.RemoveObjectAsync(args, ct);
    }
}
