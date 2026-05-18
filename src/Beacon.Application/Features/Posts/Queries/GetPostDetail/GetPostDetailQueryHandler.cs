using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;
using Beacon.Domain.Entities.Posts;

namespace Beacon.Application.Features.Posts.Queries.GetPostDetail;

public class GetPostDetailQueryHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IMediaObjectRepository mediaRepo,
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<GetPostDetailQuery, Result<PostDetailResponse>>
{
    public async Task<Result<PostDetailResponse>> Handle(GetPostDetailQuery query, CancellationToken ct)
    {
        // 1. Fetch post
        var post = await postRepo.GetByIdAsync(query.PostId, ct);
        if (post is null || post.IsDeleted)
            return Result<PostDetailResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        // 2. Check status
        if (post.Status != PostStatus.Active)
            return Result<PostDetailResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        // 3. Access control
        if (post.OwnerUserId != query.CurrentUserId)
        {
            if (post.Visibility == PostVisibility.Friends)
            {
                var areFriends = await friendRepo.AreFriendsAsync(query.CurrentUserId, post.OwnerUserId, ct);
                if (!areFriends)
                    return Result<PostDetailResponse>.Failure(
                        Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "Bạn không có quyền xem bài đăng này."));
            }
            else
            {
                // Private post, not owner
                return Result<PostDetailResponse>.Failure(
                    Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "Bài đăng này là riêng tư."));
            }
        }

        // 4. Fetch owner
        var owner = await userRepo.GetByIdAsync(post.OwnerUserId, ct);
        string? ownerAvatarUrl = null;
        if (owner?.AvatarMediaObjectId != null)
        {
            var avatarMedia = await mediaRepo.GetByIdAsync(owner.AvatarMediaObjectId.Value, ct);
            if (avatarMedia != null)
                ownerAvatarUrl = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
        }

        var ownerResponse = new OwnerInPostResponse
        {
            Id = post.OwnerUserId,
            DisplayName = owner != null ? $"{owner.FamilyName} {owner.GivenName}" : string.Empty,
            AvatarUrl = ownerAvatarUrl
        };

        // 5. Fetch post media
        var media = await mediaRepo.GetByIdAsync(post.MediaId, ct);
        MediaInPostResponse mediaResponse;
        if (media != null)
        {
            var (url, thumbUrl) = await storage.GetMediaUrlsAsync(media, ct);
            mediaResponse = mapper.ToMediaResponse(media, url, thumbUrl);
        }
        else
        {
            mediaResponse = new MediaInPostResponse { Id = post.MediaId, Url = string.Empty, Type = "unknown" };
        }

        // 6. Fetch reactions
        var reactions = await reactionRepo.GetByPostIdsAsync(new[] { post.Id }, ct);
        var myReaction = await reactionRepo.GetByPostAndUserAsync(post.Id, query.CurrentUserId, ct);

        // 7. Build and return response
        var postResponse = mapper.ToPostResponse(post, mediaResponse);
        return Result<PostDetailResponse>.Success(new PostDetailResponse
        {
            Id = postResponse.Id,
            OwnerUserId = postResponse.OwnerUserId,
            DailySafetyRecordId = postResponse.DailySafetyRecordId,
            DailySafetyRecord = postResponse.DailySafetyRecord,
            Latitude = postResponse.Latitude,
            Longitude = postResponse.Longitude,
            Media = postResponse.Media,
            Caption = postResponse.Caption,
            Visibility = postResponse.Visibility,
            Status = postResponse.Status,
            CreatedAtUtc = postResponse.CreatedAtUtc,
            UpdatedAtUtc = postResponse.UpdatedAtUtc,
            Owner = ownerResponse,
            ReactionSummary = ReactionSummaryHelper.BuildSummary(reactions),
            MyReaction = myReaction is null ? null : new MyReactionResponse { Icon = myReaction.Icon }
        });
    }
}
