using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListSentFriendRequests;

public class ListSentFriendRequestsQueryHandler(
    IFriendRequestRepository requestRepo,
    ICurrentUserService currentUser,
    FriendRequestMapper mapper)
    : IRequestHandler<ListSentFriendRequestsQuery, Result<CursorPagedResult<FriendRequestDto>>>
{
    public async Task<Result<CursorPagedResult<FriendRequestDto>>> Handle(
        ListSentFriendRequestsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await requestRepo.ListSentAsync(currentUser.UserId, query.Cursor, limit, ct);

        var senderUsername = currentUser.Username;
        var dtos = paged.Data.Select(r =>
            mapper.ToDto(r, senderUsername, null)).ToList();

        return Result<CursorPagedResult<FriendRequestDto>>.Success(new CursorPagedResult<FriendRequestDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
