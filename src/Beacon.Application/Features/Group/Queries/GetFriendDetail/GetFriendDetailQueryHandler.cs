using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.GetFriendDetail;

public class GetFriendDetailQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    FriendMapper mapper)
    : IRequestHandler<GetFriendDetailQuery, Result<FriendDto>>
{
    public async Task<Result<FriendDto>> Handle(GetFriendDetailQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var friend = await friendRepo.GetByUsersAsync(userId, query.TargetUserId, ct);

        if (friend is null)
            return Result<FriendDto>.Failure(
                Error.NotFound(ErrorCodes.Friend.FRIEND_NOT_FOUND, "Không tìm thấy thông tin bạn bè."));

        var other = friend.GetOtherUser(userId);
        return Result<FriendDto>.Success(mapper.ToDto(friend, userId, other.FamilyName, other.GivenName, null));
    }
}
