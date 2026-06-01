using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Infrashtructure.Services.Storage;

public sealed class NoOpStorageService : IStorageService
{
    public string BucketName => "beacon-noop-storage";

    public Task<StorageUploadResult> UploadAsync(
        Stream data,
        long size,
        string objectKey,
        string contentType,
        CancellationToken ct = default)
        => Task.FromResult(new StorageUploadResult("noop-etag", size));

    public Task<string> GeneratePresignedGetUrlAsync(
        string objectKey,
        CancellationToken ct = default)
        => Task.FromResult($"https://noop-storage.local/{Uri.EscapeDataString(objectKey)}");

    public Task RemoveAsync(string objectKey, CancellationToken ct = default)
        => Task.CompletedTask;
}
