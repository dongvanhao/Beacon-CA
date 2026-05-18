using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
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
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<GetPostReactionsQuery, Result<PostReactionListResponse>>
{
    public async Task<Result<PostReactionListResponse>> Handle(
        GetPostReactionsQuery query, CancellationToken ct)
    {
        // 1. Fetch post
        var post = await postRepo.GetByIdAsync(query.PostId, ct);
        if (post is null || post.IsDeleted || post.Status != PostStatus.Active)
            return Result<PostReactionListResponse>.Failure(
                Error.NotFound(ErrorCodes.Post.POST_NOT_FOUND, "Bài đăng không tồn tại."));

        // 2. Access control — chỉ chủ bài đăng mới xem được danh sách reactions
        if (post.OwnerUserId != query.CurrentUserId)
            return Result<PostReactionListResponse>.Failure(
                Error.Forbidden(ErrorCodes.Post.POST_ACCESS_DENIED, "Bạn không có quyền xem danh sách reactions của bài đăng này."));

        // 3. Parse cursor
        DateTime? cursorDt = null;
        if (!string.IsNullOrEmpty(query.Cursor) &&
            DateTime.TryParse(query.Cursor, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            cursorDt = parsed.ToUniversalTime();

        // 4. Paged reactions + all reactions for summary (sequential — EF Core DbContext is not thread-safe)
        var (pagedItems, hasMore) = await reactionRepo.GetPagedByPostIdAsync(query.PostId, query.Icon, cursorDt, query.Limit, ct);
        var allReactions = await reactionRepo.GetAllByPostIdAsync(query.PostId, ct);

        // 5. Summary with all 5 icons always present
        var iconCounts = ReactionIcons.Supported.ToDictionary(k => k, k => allReactions.Count(r => r.Icon == k));
        var summary = new PostReactionSummaryResponse
        {
            TotalCount = allReactions.Count,
            Icons = iconCounts
        };

        // 6. Batch load reactor user info (bounded by limit ≤ 100)
        var uniqueUserIds = pagedItems.Select(r => r.UserId).Distinct().ToList();
        var userDict = new Dictionary<Guid, (string DisplayName, string? AvatarUrl)>();

        foreach (var userId in uniqueUserIds)
        {
            var user = await userRepo.GetByIdAsync(userId, ct);
            if (user is null) continue;

            string? avatarUrl = null;
            if (user.AvatarMediaObjectId.HasValue)
            {
                var avatarMedia = await mediaRepo.GetByIdAsync(user.AvatarMediaObjectId.Value, ct);
                if (avatarMedia is not null)
                    avatarUrl = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
            }

            userDict[userId] = ($"{user.FamilyName} {user.GivenName}", avatarUrl);
        }

        // 7. Map items
        var items = pagedItems
            .Select(r =>
            {
                userDict.TryGetValue(r.UserId, out var userInfo);
                return mapper.ToReactionItemResponse(r, userInfo.DisplayName ?? string.Empty, userInfo.AvatarUrl);
            })
            .ToList();

        // 8. Next cursor = CreatedAtUtc of last item (ISO-8601 round-trip)
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
}
