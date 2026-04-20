using Beacon.Application.Common.Interfaces.IService;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Beacon.Infrashtructure.Services.Storage;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task<ImageMetadata> ReadMetadataAsync(Stream source, CancellationToken ct = default)
    {
        var info = await Image.IdentifyAsync(source, ct);
        return new ImageMetadata(info.Width, info.Height);
    }

    public async Task<ThumbnailResult> GenerateThumbnailAsync(
        Stream source,
        int maxDimension,
        CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(source, ct);

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxDimension, maxDimension)
        }));

        var output = new MemoryStream();
        await image.SaveAsync(output, new WebpEncoder { Quality = 75 }, ct);
        output.Position = 0;

        return new ThumbnailResult(
            Stream: output,
            Size: output.Length,
            ContentType: "image/webp",
            Width: image.Width,
            Height: image.Height);
    }
}
