namespace Beacon.Application.Common.Interfaces.IService;

public interface IImageProcessor
{
    Task<ImageMetadata> ReadMetadataAsync(Stream source, CancellationToken ct = default);

    Task<ThumbnailResult> GenerateThumbnailAsync(
        Stream source,
        int maxDimension,
        CancellationToken ct = default);
}

public sealed record ImageMetadata(int Width, int Height);

public sealed record ThumbnailResult(
    Stream Stream,
    long Size,
    string ContentType,
    int Width,
    int Height);
