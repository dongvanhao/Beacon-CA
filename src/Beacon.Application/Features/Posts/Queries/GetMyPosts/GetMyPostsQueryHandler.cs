using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Features.Posts.Helpers;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Posts.Queries.GetMyPosts;

public class GetMyPostsQueryHandler(
    IPostRepository postRepo,
    IPostReactionRepository reactionRepo,
    IMediaObjectRepository mediaRepo,
    IUserRepository userRepo,
    IStorageService storage,
    PostDtoMapper mapper)
    : IRequestHandler<GetMyPostsQuery, Result<FeedResponse>>
{
    public async Task<Result<FeedResponse>> Handle(GetMyPostsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 50);
        var (cursorCreatedAt, cursorId) = FeedCursorHelper.Decode(query.Cursor);

        var posts = await postRepo.GetMyPostsAsync(
            query.CurrentUserId, cursorCreatedAt, cursorId, limit + 1, ct);

        var hasMore = posts.Count > limit;
        if (hasMore) posts = posts.Take(limit).ToList();

        if (posts.Count == 0)
            return Result<FeedResponse>.Success(new FeedResponse { Items = new(), NextCursor = null });

        var postIds = posts.Select(p => p.Id).ToList();
        var allReactions = await reactionRepo.GetByPostIdsAsync(postIds, ct);
        var myReactions = await reactionRepo.GetByPostIdsForUserAsync(postIds, query.CurrentUserId, ct);

        var reactionsByPostId = allReactions.GroupBy(r => r.PostId).ToDictionary(g => g.Key, g => g.ToList());
        var myReactionByPostId = myReactions.ToDictionary(r => r.PostId, r => r);

        var mediaIds = posts.Select(p => p.MediaId).Distinct().ToList();
        var mediaDict = new Dictionary<Guid, MediaObject>();
        foreach (var mediaId in mediaIds)
        {
            var m = await mediaRepo.GetByIdAsync(mediaId, ct);
            if (m != null) mediaDict[mediaId] = m;
        }

        var urlResults = await storage.GetMediaUrlsBatchAsync(mediaDict.Values.ToList(), ct);
        var mediaUrlDict = urlResults.ToDictionary(r => r.Media.Id, r => (r.Url, r.ThumbUrl));

        var owner = await userRepo.GetByIdAsync(query.CurrentUserId, ct);
        string? avatarUrl = null;
        if (owner?.AvatarMediaObjectId != null)
        {
            var avatarMedia = await mediaRepo.GetByIdAsync(owner.AvatarMediaObjectId.Value, ct);
            if (avatarMedia != null)
                avatarUrl = await storage.GeneratePresignedGetUrlAsync(avatarMedia.ObjectKey, ct);
        }

        var ownerResponse = new OwnerInPostResponse
        {
            Id = query.CurrentUserId,
            DisplayName = owner != null ? $"{owner.FamilyName} {owner.GivenName}" : string.Empty,
            AvatarUrl = avatarUrl
        };

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

            var postReactions = reactionsByPostId.TryGetValue(post.Id, out var pr) ? pr : new List<PostReaction>();
            var myReaction = myReactionByPostId.TryGetValue(post.Id, out var mr) ? mr : null;

            feedItems.Add(new FeedPostResponse
            {
                Id = post.Id,
                OwnerUserId = post.OwnerUserId,
                DailySafetyRecordId = post.DailySafetyRecordId,
                DailySafetyRecord = mapper.ToDailySafetyRecordResponse(post.DailySafetyRecord),
                Latitude = post.Latitude,
                Longitude = post.Longitude,
                Owner = ownerResponse,
                Media = mediaResponse,
                Caption = post.Caption,
                Visibility = post.Visibility.ToString().ToLowerInvariant(),
                CreatedAtUtc = post.CreatedAtUtc,
                ReactionSummary = ReactionSummaryHelper.BuildSummary(postReactions),
                MyReaction = myReaction is null ? null : new MyReactionResponse { Icon = myReaction.Icon }
            });
        }

        string? nextCursor = null;
        if (hasMore)
        {
            var last = posts.Last();
            nextCursor = FeedCursorHelper.Encode(last.CreatedAtUtc, last.Id);
        }

        return Result<FeedResponse>.Success(new FeedResponse { Items = feedItems, NextCursor = nextCursor });
    }
}
