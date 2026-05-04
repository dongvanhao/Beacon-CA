using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListFriends;

public class ListFriendsQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    FriendMapper mapper)
    : IRequestHandler<ListFriendsQuery, Result<CursorPagedResult<FriendDto>>>
{
    public async Task<Result<CursorPagedResult<FriendDto>>> Handle(
        ListFriendsQuery query, CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 100);
        var userId = currentUser.UserId;
        var paged = await friendRepo.ListByUserAsync(userId, query.Cursor, limit, ct);

        var dtos = paged.Data
            .Select(f =>
            {
                var other = f.GetOtherUser(userId);
                return mapper.ToDto(f, userId, other.FamilyName, other.GivenName, null);
            })
            .ToList();

        return Result<CursorPagedResult<FriendDto>>.Success(new CursorPagedResult<FriendDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
