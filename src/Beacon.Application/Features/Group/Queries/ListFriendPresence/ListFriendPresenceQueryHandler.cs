using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListFriendPresence;

public class ListFriendPresenceQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    IUserOnlineTracker onlineTracker,
    FriendPresenceMapper mapper)
    : IRequestHandler<ListFriendPresenceQuery, Result<CursorPagedResult<FriendPresenceDto>>>
{
    public async Task<Result<CursorPagedResult<FriendPresenceDto>>> Handle(
        ListFriendPresenceQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var userId = currentUser.UserId;
        var paged = await friendRepo.ListByUserAsync(userId, query.Cursor, limit, ct);

        var avatarObjects = paged.Data
            .Select(item => item.Friend.GetOtherUser(userId).AvatarMediaObject)
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var dtos = paged.Data.Select(item =>
        {
            var other = item.Friend.GetOtherUser(userId);
            var avatarUrl = other.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(other.AvatarMediaObjectId.Value, out var url) ? url : null;
            var isOnline = onlineTracker.IsOnline(other.Id);
            return mapper.ToDto(other, avatarUrl, isOnline);
        }).ToList();

        return Result<CursorPagedResult<FriendPresenceDto>>.Success(new CursorPagedResult<FriendPresenceDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
