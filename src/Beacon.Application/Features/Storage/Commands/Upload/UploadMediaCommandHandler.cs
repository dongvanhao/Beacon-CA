using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Storage.Dtos;
using Beacon.Application.Mappings.Storage;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Storage.Commands.Upload;

public class UploadMediaCommandHandler(
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    IImageProcessor imageProcessor,
    MediaDtoMapper mapper,
    ILogger<UploadMediaCommandHandler> logger)
    : IRequestHandler<UploadMediaCommand, Result<MediaDto>>
{
    private const int ThumbnailMaxDimension = 400;

    public async Task<Result<MediaDto>> Handle(UploadMediaCommand command, CancellationToken ct)
    {
        var file = command.File;
        var mediaType = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? MediaType.Image
            : MediaType.Video;

        var ext = Path.GetExtension(file.FileName);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var fileId = Guid.NewGuid().ToString("N");
        var objectKey = $"{datePrefix}/{fileId}{ext}";
        string? thumbnailKey = null;
        int? width = null;
        int? height = null;

        if (mediaType == MediaType.Image)
        {
            try
            {
                await using var probeStream = file.OpenReadStream();
                var thumb = await imageProcessor.GenerateThumbnailAsync(probeStream, ThumbnailMaxDimension, ct);

                width = thumb.Width;
                height = thumb.Height;

                thumbnailKey = $"{datePrefix}/thumbs/{fileId}.webp";
                await using (thumb.Stream)
                {
                    await storage.UploadAsync(thumb.Stream, thumb.Size, thumbnailKey, thumb.ContentType, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Thumbnail generation failed for {ObjectKey}; continuing without thumbnail.", objectKey);
                thumbnailKey = null;
            }
        }

        StorageUploadResult uploadResult;
        try
        {
            await using var mainStream = file.OpenReadStream();
            uploadResult = await storage.UploadAsync(mainStream, file.Length, objectKey, file.ContentType, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for {ObjectKey}.", objectKey);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result<MediaDto>.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể upload file lên storage."));
        }

        var media = MediaObject.Create(
            bucketName: storage.BucketName,
            objectKey: objectKey,
            originalFileName: file.FileName,
            contentType: file.ContentType,
            fileSizeBytes: uploadResult.Size,
            mediaType: mediaType,
            uploadedByUserId: command.CurrentUserId,
            thumbnailObjectKey: thumbnailKey,
            width: width,
            height: height,
            etag: uploadResult.ETag);

        try
        {
            await mediaRepository.AddAsync(media, ct);
            await mediaRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DB save failed after upload; rolling back object {ObjectKey}.", objectKey);
            await SafeRemoveAsync(objectKey, ct);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result<MediaDto>.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể lưu metadata media."));
        }

        var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);

        return Result<MediaDto>.Success(mapper.ToDto(media, url, thumbUrl));
    }

    private async Task SafeRemoveAsync(string key, CancellationToken ct)
    {
        try { await storage.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Rollback remove failed for {ObjectKey}.", key); }
    }
}
