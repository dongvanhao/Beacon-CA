using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.SearchFriends;

public class SearchFriendsQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    FriendMapper mapper)
    : IRequestHandler<SearchFriendsQuery, Result<CursorPagedResult<FriendDto>>>
{
    public async Task<Result<CursorPagedResult<FriendDto>>> Handle(
        SearchFriendsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await friendRepo.SearchByUserAsync(userId, query.Search, query.Cursor, limit, ct);

        var avatarObjects = paged.Data
            .Select(f => f.GetOtherUser(userId).AvatarMediaObject)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var dtos = paged.Data.Select(f =>
        {
            var other = f.GetOtherUser(userId);
            var avatarUrl = other.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(other.AvatarMediaObjectId.Value, out var url)
                ? url : null;
            return mapper.ToDto(f, userId, other.Username, avatarUrl);
        }).ToList();

        return Result<CursorPagedResult<FriendDto>>.Success(new CursorPagedResult<FriendDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
