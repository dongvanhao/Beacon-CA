using Beacon.Domain.Entities.Storage;

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

/// <summary>
/// Extension methods tái sử dụng logic lấy presigned URL cho media.
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>
    /// Lấy (url, thumbUrl) cho một MediaObject.
    /// Dùng chung cho GetMediaById, Upload, v.v.
    /// </summary>
    public static async Task<(string Url, string? ThumbUrl)> GetMediaUrlsAsync(
        this IStorageService storage,
        MediaObject media,
        CancellationToken ct = default)
    {
        var url = await storage.GeneratePresignedGetUrlAsync(media.ObjectKey, ct);
        var thumbUrl = string.IsNullOrWhiteSpace(media.ThumbnailObjectKey)
            ? null
            : await storage.GeneratePresignedGetUrlAsync(media.ThumbnailObjectKey, ct);
        return (url, thumbUrl);
    }

    /// <summary>
    /// Lấy (url, thumbUrl) cho một list MediaObject song song.
    /// Dùng chung cho ListMedia và bất kỳ query nào trả nhiều media.
    /// </summary>
    public static async Task<IReadOnlyList<(MediaObject Media, string Url, string? ThumbUrl)>> GetMediaUrlsBatchAsync(
        this IStorageService storage,
        IEnumerable<MediaObject> medias,
        CancellationToken ct = default)
    {
        var results = await Task.WhenAll(medias.Select(async m =>
        {
            var (url, thumbUrl) = await storage.GetMediaUrlsAsync(m, ct);
            return (Media: m, Url: url, ThumbUrl: thumbUrl);
        }));
        return results;
    }
}
