using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetFeed;

public class GetFeedQueryHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IMediaObjectRepository mediaRepo,
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<GetFeedQuery, Result<FeedResponse>>
{
    public async Task<Result<FeedResponse>> Handle(GetFeedQuery query, CancellationToken ct)
    {
        // 1. Clamp limit
        var limit = Math.Clamp(query.Limit, 1, 50);

        // 2. Decode cursor
        var (cursorCreatedAt, cursorId) = FeedCursorHelper.Decode(query.Cursor);

        // 3. Get friend IDs
        var friendIds = await friendRepo.ListFriendIdsAsync(query.CurrentUserId, ct);

        // 4. Get posts (fetch limit+1 to detect hasMore)
        var posts = await postRepo.GetFeedAsync(
            query.CurrentUserId, friendIds, cursorCreatedAt, cursorId, limit + 1, ct);

        // 5. Determine hasMore and trim
        var hasMore = posts.Count > limit;
        if (hasMore) posts = posts.Take(limit).ToList();

        if (posts.Count == 0)
        {
            return Result<FeedResponse>.Success(new FeedResponse { Items = new(), NextCursor = null });
        }

        // 6. Batch load reactions
        var postIds = posts.Select(p => p.Id).ToList();
        var allReactions = await reactionRepo.GetByPostIdsAsync(postIds, ct);
        var myReactions = await reactionRepo.GetByPostIdsForUserAsync(postIds, query.CurrentUserId, ct);

        var reactionsByPostId = allReactions.GroupBy(r => r.PostId).ToDictionary(g => g.Key, g => g.ToList());
        var myReactionByPostId = myReactions.ToDictionary(r => r.PostId, r => r);

        // 7. Batch load media (fetch individually — no batch method on IMediaObjectRepository)
        var mediaIds = posts.Select(p => p.MediaId).Distinct().ToList();
        var mediaDict = new Dictionary<Guid, MediaObject>();
        foreach (var mediaId in mediaIds)
        {
            var m = await mediaRepo.GetByIdAsync(mediaId, ct);
            if (m != null) mediaDict[mediaId] = m;
        }

        // 8. Batch URL generation for post medias
        var mediaList = mediaDict.Values.ToList();
        var urlResults = await storage.GetMediaUrlsBatchAsync(mediaList, ct);
        var mediaUrlDict = urlResults.ToDictionary(r => r.Media.Id, r => (r.Url, r.ThumbUrl));

        // 9. Batch load owners (fetch individually per unique owner)
        var ownerIds = posts.Select(p => p.OwnerUserId).Distinct().ToList();
        var ownerDict = new Dictionary<Guid, User>();
        foreach (var ownerId in ownerIds)
        {
            var u = await userRepo.GetByIdAsync(ownerId, ct);
            if (u != null) ownerDict[ownerId] = u;
        }

        // 10. Fetch owner avatar URLs
        var ownerAvatarUrlDict = new Dictionary<Guid, string?>();
        foreach (var (ownerId, user) in ownerDict)
        {
            if (user.AvatarMediaObjectId != null)
            {
                var avatarMedia = await mediaRepo.GetByIdAsync(user.AvatarMediaObjectId.Value, ct);
                if (avatarMedia != null)
                    ownerAvatarUrlDict[ownerId] = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
                else
                    ownerAvatarUrlDict[ownerId] = null;
            }
            else
            {
                ownerAvatarUrlDict[ownerId] = null;
            }
        }

        // 11. Map each post to FeedPostResponse
        var feedItems = new List<FeedPostResponse>(posts.Count);
        foreach (var post in posts)
        {
            MediaInPostResponse mediaResponse;
            if (mediaDict.TryGetValue(post.MediaId, out var postMedia) &&
                mediaUrlDict.TryGetValue(postMedia.Id, out var urls))
            {
                mediaResponse = mapper.ToMediaResponse(postMedia, urls.Url, urls.ThumbUrl);
            }
            else
            {
                mediaResponse = new MediaInPostResponse { Id = post.MediaId, Url = string.Empty, Type = "unknown" };
            }

            ownerDict.TryGetValue(post.OwnerUserId, out var ownerEntity);
            ownerAvatarUrlDict.TryGetValue(post.OwnerUserId, out var avatarUrl);

            var ownerResponse = new OwnerInPostResponse
            {
                Id = post.OwnerUserId,
                DisplayName = ownerEntity != null ? $"{ownerEntity.FamilyName} {ownerEntity.GivenName}" : string.Empty,
                AvatarUrl = avatarUrl
            };

            var postReactions = reactionsByPostId.TryGetValue(post.Id, out var pr) ? pr : new List<PostReaction>();
            var myReaction = myReactionByPostId.TryGetValue(post.Id, out var mr) ? mr : null;

            feedItems.Add(new FeedPostResponse
            {
                Id = post.Id,
                OwnerUserId = post.OwnerUserId,
                Owner = ownerResponse,
                Media = mediaResponse,
                Caption = post.Caption,
                Visibility = post.Visibility.ToString().ToLowerInvariant(),
                CreatedAtUtc = post.CreatedAtUtc,
                ReactionSummary = ReactionSummaryHelper.BuildSummary(postReactions),
                MyReaction = myReaction is null ? null : new MyReactionResponse { Icon = myReaction.Icon }
            });
        }

        // 12. Next cursor
        string? nextCursor = null;
        if (hasMore)
        {
            var last = posts.Last();
            nextCursor = FeedCursorHelper.Encode(last.CreatedAtUtc, last.Id);
        }

        return Result<FeedResponse>.Success(new FeedResponse { Items = feedItems, NextCursor = nextCursor });
    }
}
