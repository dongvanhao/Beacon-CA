using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListReceivedFriendRequests;

public class ListReceivedFriendRequestsQueryHandler(
    IFriendRequestRepository requestRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    FriendRequestMapper mapper)
    : IRequestHandler<ListReceivedFriendRequestsQuery, Result<CursorPagedResult<FriendRequestDto>>>
{
    public async Task<Result<CursorPagedResult<FriendRequestDto>>> Handle(
        ListReceivedFriendRequestsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await requestRepo.ListReceivedAsync(currentUser.UserId, query.Cursor, limit, ct);

        var avatarObjects = paged.Data
            .Where(r => r.Initiator.AvatarMediaObject is not null)
            .Select(r => r.Initiator.AvatarMediaObject!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var dtos = paged.Data.Select(r =>
        {
            var avatarUrl = r.Initiator.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(r.Initiator.AvatarMediaObjectId.Value, out var url) ? url : null;
            return mapper.ToDto(r, r.Initiator.FamilyName, r.Initiator.GivenName, avatarUrl);
        }).ToList();

        return Result<CursorPagedResult<FriendRequestDto>>.Success(new CursorPagedResult<FriendRequestDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
