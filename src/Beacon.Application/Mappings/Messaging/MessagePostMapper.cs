using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Storage;

namespace Beacon.Application.Mappings.Messaging;

public sealed class MessagePostMapper(
    IMediaObjectRepository mediaRepo,
    IUserRepository userRepo,
    IStorageService storage,
    PostDtoMapper postMapper)
{
    public async Task<MessagePostDto?> ToDtoAsync(Post? post, CancellationToken ct)
    {
        if (post is null)
            return null;

        var media = await mediaRepo.GetByIdAsync(post.MediaId, ct);
        var mediaResponse = new MediaInPostResponse
        {
            Id = post.MediaId,
            Url = string.Empty,
            Type = "unknown"
        };

        if (media is not null)
        {
            var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);
            mediaResponse = postMapper.ToMediaResponse(media, url, thumbUrl);
        }

        var owner = await userRepo.GetByIdAsync(post.OwnerUserId, ct);
        string? ownerAvatarUrl = null;
        if (owner?.AvatarMediaObjectId is Guid avatarMediaId)
        {
            var avatarMedia = await mediaRepo.GetByIdAsync(avatarMediaId, ct);
            if (avatarMedia is not null)
                ownerAvatarUrl = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
        }

        var postResponse = postMapper.ToPostResponse(post, mediaResponse);
        return new MessagePostDto
        {
            Id = postResponse.Id,
            OwnerUserId = postResponse.OwnerUserId,
            DailySafetyRecordId = postResponse.DailySafetyRecordId,
            DailySafetyRecord = postResponse.DailySafetyRecord,
            Latitude = postResponse.Latitude,
            Longitude = postResponse.Longitude,
            Owner = new OwnerInPostResponse
            {
                Id = post.OwnerUserId,
                DisplayName = owner is null ? string.Empty : $"{owner.FamilyName} {owner.GivenName}".Trim(),
                AvatarUrl = ownerAvatarUrl
            },
            Media = postResponse.Media,
            Caption = postResponse.Caption,
            Visibility = postResponse.Visibility,
            Status = postResponse.Status,
            CreatedAtUtc = postResponse.CreatedAtUtc,
            UpdatedAtUtc = postResponse.UpdatedAtUtc
        };
    }
}
