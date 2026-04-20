namespace Beacon.Application.Common.Interfaces.IService;

public interface IStorageService
{
    Task<StorageUploadResult> UploadAsync(
        Stream data,
        long size,
        string objectKey,
        string contentType,
        CancellationToken ct = default);

    Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        CancellationToken ct = default);

    Task RemoveAsync(string objectKey, CancellationToken ct = default);

    string BucketName { get; }
}

public sealed record StorageUploadResult(string? ETag, long Size);
