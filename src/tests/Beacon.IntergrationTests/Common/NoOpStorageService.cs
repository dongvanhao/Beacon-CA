using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.IntergrationTests.Common;

public class NoOpStorageService : IStorageService
{
    public string BucketName => "test-bucket";

    public Task<StorageUploadResult> UploadAsync(Stream data, long size, string objectKey, string contentType, CancellationToken ct = default)
        => Task.FromResult(new StorageUploadResult("test-etag", size));

    public Task<string> GeneratePresignedGetUrlAsync(string objectKey, CancellationToken ct = default)
        => Task.FromResult($"http://test-storage/{objectKey}");

    public Task RemoveAsync(string objectKey, CancellationToken ct = default)
        => Task.CompletedTask;
}
