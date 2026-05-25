using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Helpers;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupAvatar;

public class UpdateGroupAvatarCommandHandler(
    IMessageGroupRepository groupRepo,
    IMediaObjectRepository mediaRepository,
    IStorageService storage,
    IImageProcessor imageProcessor,
    ICurrentUserService currentUser,
    ISender sender,
    ILogger<UpdateGroupAvatarCommandHandler> logger)
    : IRequestHandler<UpdateGroupAvatarCommand, Result>
{
    private const int ThumbnailMaxDimension = 400;

    public async Task<Result> Handle(UpdateGroupAvatarCommand command, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
        if (group is null || group.IsDeleted)
            return Result.Failure(Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat."));

        if (group.Type == MessageGroupType.Direct)
            return Result.Failure(Error.Validation(ErrorCodes.Validation.VALIDATION_ERROR, "Không thể đổi avatar chat 1-1 qua endpoint này."));

        var isMember = group.Members.Any(m => m.UserId == currentUser.UserId
            && m.Status == MessageGroupMemberStatus.Joined);
        if (!isMember)
            return Result.Failure(Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không phải thành viên của nhóm này."));

        var file = command.File;
        var ext = Path.GetExtension(file.FileName);
        var datePrefix = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var fileId = Guid.NewGuid().ToString("N");
        var objectKey = $"{datePrefix}/{fileId}{ext}";
        string? thumbnailKey = null;
        int? width = null;
        int? height = null;

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
            logger.LogWarning(ex, "Thumbnail generation failed for group avatar {ObjectKey}; continuing without thumbnail.", objectKey);
            thumbnailKey = null;
        }

        StorageUploadResult uploadResult;
        try
        {
            await using var mainStream = file.OpenReadStream();
            uploadResult = await storage.UploadAsync(mainStream, file.Length, objectKey, file.ContentType, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Group avatar upload failed for {ObjectKey}.", objectKey);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể upload ảnh avatar nhóm lên storage."));
        }

        var newMedia = MediaObject.Create(
            bucketName: storage.BucketName,
            objectKey: objectKey,
            originalFileName: file.FileName,
            contentType: file.ContentType,
            fileSizeBytes: uploadResult.Size,
            mediaType: MediaType.Image,
            uploadedByUserId: currentUser.UserId,
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
            logger.LogError(ex, "DB save failed after group avatar upload; rolling back {ObjectKey}.", objectKey);
            await SafeRemoveAsync(objectKey, ct);
            if (thumbnailKey is not null)
                await SafeRemoveAsync(thumbnailKey, ct);

            return Result.Failure(
                Error.Failure(ErrorCodes.Storage.UPLOAD_FAILED, "Không thể lưu metadata avatar nhóm."));
        }

        if (group.AvatarMediaObjectId is not null)
        {
            var oldMedia = await mediaRepository.GetByIdAsync(group.AvatarMediaObjectId.Value, ct);
            if (oldMedia is not null)
            {
                oldMedia.Delete();
                await mediaRepository.SaveChangesAsync(ct);
            }
        }

        group.AvatarMediaObjectId = newMedia.Id;
        await groupRepo.SaveChangesAsync(ct);

        var avatarUrl = await storage.GeneratePresignedGetUrlAsync(newMedia.ObjectKey, ct);
        var actorName = FormatName(currentUser.FamilyName, currentUser.GivenName, "Một thành viên");
        var sendResult = await sender.Send(new SendMessageCommand(
            command.GroupId,
            $"{actorName} đã cập nhật ảnh đại diện nhóm".Trim(),
            null,
            null,
            MessageType.GroupAvatarChanged,
            MessageMetadataHelper.Serialize(new
            {
                actorUserId = currentUser.UserId,
                avatarUrl
            })), ct);
        if (sendResult.IsFailure)
            return Result.Failure(sendResult.Error);

        return Result.Success();
    }

    private async Task SafeRemoveAsync(string key, CancellationToken ct)
    {
        try { await storage.RemoveAsync(key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Rollback remove failed for {ObjectKey}.", key); }
    }

    private static string FormatName(string? familyName, string? givenName, string fallback)
    {
        var name = $"{familyName} {givenName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }
}
