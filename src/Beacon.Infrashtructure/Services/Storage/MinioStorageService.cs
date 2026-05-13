using Beacon.Application.Common.Interfaces.IService;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Beacon.Infrashtructure.Services.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minio;
    private readonly IMinioClient _presignedMinio;
    private readonly MinioSettings _settings;

    public string BucketName => _settings.BucketName;

    public MinioStorageService(IMinioClient minio, IOptions<MinioSettings> options)
    {
        _minio = minio;
        _settings = options.Value;

        // Client riêng để generate presigned URL - ký bằng PublicEndpoint
        // để URL trả về client có thể truy cập được từ bên ngoài Docker.
        // (Host PHẢI khớp lúc ký vì "host" nằm trong X-Amz-SignedHeaders)
        _presignedMinio = BuildPresignedClient(_settings);
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
            .WithExpiry(_settings.PresignedUrlExpirySeconds);

        // Dùng _presignedMinio để SDK ký bằng public host ngay từ đầu.
        // Không rewrite host sau khi ký vì sẽ gây SignatureDoesNotMatch.
        return await _presignedMinio.PresignedGetObjectAsync(args);
    }

    public async Task RemoveAsync(string objectKey, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(BucketName)
            .WithObject(objectKey);

        await _minio.RemoveObjectAsync(args, ct);
    }

    /// <summary>
    /// Tạo MinioClient dùng riêng cho presigned URL.
    /// Client này trỏ vào PublicEndpoint thay vì internal endpoint (minio:9000),
    /// do đó signature được tính với host công khai và URL trả về client là valid.
    /// </summary>
    private static IMinioClient BuildPresignedClient(MinioSettings settings)
    {
        var publicEndpointUrl = settings.GetRequiredPublicEndpoint();
        var publicEndpoint = publicEndpointUrl
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        var builder = new MinioClient()
            .WithEndpoint(publicEndpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey);

        // UseSSL theo public endpoint (production dùng HTTPS)
        var publicScheme = publicEndpointUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (publicScheme) builder = builder.WithSSL();

        return builder.Build();
    }
}
