using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Identity.Commands.UpdateAvatar;

public class UpdateAvatarCommandHandler(
    IUserRepository userRepository,
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    IImageProcessor imageProcessor,
    UserProfileMapper mapper,
    ILogger<UpdateAvatarCommandHandler> logger)
    : IRequestHandler<UpdateAvatarCommand, Result<UserProfileDto>>
{
    private const int ThumbnailMaxDimension = 400;

    public async Task<Result<UserProfileDto>> Handle(UpdateAvatarCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return Result<UserProfileDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        var file = command.File;

        // ── 1. Tạo object key ─────────────────────────────────────────────
        var ext = Path.GetExtension(file.FileName);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var fileId = Guid.NewGuid().ToString("N");
        var objectKey = $"{datePrefix}/{fileId}{ext}";
        string? thumbnailKey = null;
        int? width = null;
        int? height = null;

        // ── 2. Sinh thumbnail ─────────────────────────────────────────────
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
            logger.LogWarning(ex, "Thumbnail generation failed for avatar {ObjectKey}; continuing without thumbnail.", objectKey);
            thumbnailKey = null;
        }

        // ── 3. Upload file gốc ────────────────────────────────────────────
        StorageUploadResult uploadResult;
        try
        {
            await using var mainStream = file.OpenReadStream();
            uploadResult = await storage.UploadAsync(mainStream, file.Length, objectKey, file.ContentType, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Avatar upload failed for {ObjectKey}.", objectKey);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result<UserProfileDto>.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể upload ảnh avatar lên storage."));
        }

        // ── 4. Lưu MediaObject mới ────────────────────────────────────────
        var newMedia = MediaObject.Create(
            bucketName: storage.BucketName,
            objectKey: objectKey,
            originalFileName: file.FileName,
            contentType: file.ContentType,
            fileSizeBytes: uploadResult.Size,
            mediaType: MediaType.Image,
            uploadedByUserId: command.UserId,
            thumbnailObjectKey: thumbnailKey,
            width: width,
            height: height,
            etag: uploadResult.ETag);

        try
        {
            await mediaRepository.AddAsync(newMedia, ct);
            await mediaRepository.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DB save failed after avatar upload; rolling back {ObjectKey}.", objectKey);
            await SafeRemoveAsync(objectKey, ct);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result<UserProfileDto>.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể lưu metadata avatar."));
        }

        // ── 5. Soft-delete avatar cũ + gán avatar mới ────────────────────
        if (user.AvatarMediaObjectId is not null)
        {
            var oldMedia = await mediaRepository.GetByIdAsync(user.AvatarMediaObjectId.Value, ct);
            if (oldMedia is not null)
            {
                oldMedia.Delete();
                await mediaRepository.SaveChangesAsync(ct);
            }
        }

        user.UpdateAvatar(newMedia.Id);
        await userRepository.SaveChangesAsync(ct);

        // ── 6. Trả về profile với avatarUrl ───────────────────────────────
        var avatarUrl = (await storage.GetMediaUrlsAsync(newMedia, ct)).Url;
        return Result<UserProfileDto>.Success(mapper.ToProfileDto(user, avatarUrl));
    }

    private async Task SafeRemoveAsync(string key, CancellationToken ct)
    {
        try { await storage.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Rollback remove failed for {ObjectKey}.", key); }
    }
}
