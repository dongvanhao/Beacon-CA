using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetPostReactions;

public class GetPostReactionsQueryHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IUserRepository userRepo,
    IMediaObjectRepository mediaRepo,
    IFriendRepository friendRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<GetPostReactionsQuery, Result<PostReactionListResponse>>
{
    public async Task<Result<PostReactionListResponse>> Handle(
        GetPostReactionsQuery query, CancellationToken ct)
    {
        var post = await postRepo.GetByIdAsync(query.PostId, ct);
        if (post is null || post.IsDeleted || post.Status != PostStatus.Active)
            return Result<PostReactionListResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Post does not exist."));

        var isOwner = post.OwnerUserId == query.CurrentUserId;
        if (!isOwner)
        {
            if (post.Visibility != PostVisibility.Friends ||
                !await friendRepo.AreFriendsAsync(query.CurrentUserId, post.OwnerUserId, ct))
            {
                return Result<PostReactionListResponse>.Failure(
                    Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "You cannot view this post's reactions."));
            }
        }

        if (!isOwner)
            return await BuildCurrentUserReactionResponseAsync(query, ct);

        DateTime? cursorDt = null;
        if (!string.IsNullOrEmpty(query.Cursor) &&
            DateTime.TryParse(query.Cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            cursorDt = parsed.ToUniversalTime();

        var (pagedItems, hasMore) = await reactionRepo.GetPagedByPostIdAsync(
            query.PostId,
            query.Icon,
            cursorDt,
            query.Limit,
            ct);
        var allReactions = await reactionRepo.GetAllByPostIdAsync(query.PostId, ct);
        var summary = ReactionSummaryHelper.BuildSummary(allReactions);

        var userDict = new Dictionary<Guid, (string DisplayName, string? AvatarUrl)>();
        foreach (var userId in pagedItems.Select(r => r.UserId).Distinct())
        {
            var userInfo = await GetUserInfoAsync(userId, ct);
            if (userInfo is not null)
                userDict[userId] = userInfo.Value;
        }

        var items = pagedItems
            .Select(r =>
            {
                userDict.TryGetValue(r.UserId, out var userInfo);
                return mapper.ToReactionItemResponse(r, userInfo.DisplayName ?? string.Empty, userInfo.AvatarUrl);
            })
            .ToList();

        string? nextCursor = hasMore && pagedItems.Count > 0
            ? pagedItems.Last().CreatedAtUtc.ToString("O")
            : null;

        return Result<PostReactionListResponse>.Success(new PostReactionListResponse
        {
            Items = items,
            Summary = summary,
            NextCursor = nextCursor,
            HasMore = hasMore
        });
    }

    private async Task<Result<PostReactionListResponse>> BuildCurrentUserReactionResponseAsync(
        GetPostReactionsQuery query,
        CancellationToken ct)
    {
        var myReaction = await reactionRepo.GetByPostAndUserAsync(query.PostId, query.CurrentUserId, ct);
        var hasMatchingIcon = myReaction is not null &&
                              (query.Icon is null || ReactionIcons.Split(myReaction.Icon).Contains(query.Icon));
        var itemsSource = hasMatchingIcon ? new[] { myReaction! } : Array.Empty<PostReaction>();
        var summary = ReactionSummaryHelper.BuildSummary(itemsSource);

        var userInfo = itemsSource.Length > 0
            ? await GetUserInfoAsync(query.CurrentUserId, ct)
            : null;

        var items = itemsSource.Select(r => mapper.ToReactionItemResponse(
            r,
            userInfo?.DisplayName ?? string.Empty,
            userInfo?.AvatarUrl)).ToList();

        return Result<PostReactionListResponse>.Success(new PostReactionListResponse
        {
            Items = items,
            Summary = summary,
            NextCursor = null,
            HasMore = false
        });
    }

    private async Task<(string DisplayName, string? AvatarUrl)?> GetUserInfoAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user is null)
            return null;

        string? avatarUrl = null;
        if (user.AvatarMediaObjectId.HasValue)
        {
            var avatarMedia = await mediaRepo.GetByIdAsync(user.AvatarMediaObjectId.Value, ct);
            if (avatarMedia is not null)
                avatarUrl = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
        }

        return ($"{user.FamilyName} {user.GivenName}", avatarUrl);
    }
}
