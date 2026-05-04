using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.FindUserByPhone;

public class FindUserByPhoneQueryHandler(
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IFriendRequestRepository friendRequestRepo,
    ICurrentUserService currentUser,
    IStorageService storage)
    : IRequestHandler<FindUserByPhoneQuery, Result<UserSearchDto>>
{
    public async Task<Result<UserSearchDto>> Handle(FindUserByPhoneQuery query, CancellationToken ct)
    {
        var target = await userRepo.GetByPhoneAsync(query.Search, ct);

        if (target is null || target.Id == currentUser.UserId)
            return Result<UserSearchDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Không tìm thấy người dùng."));

        var status = await ResolveStatusAsync(target.Id, ct);

        string? avatarUrl = null;
        if (target.AvatarMediaObject is not null)
            avatarUrl = await storage.GeneratePresignedGetUrlAsync(target.AvatarMediaObject.ObjectKey, ct);

        return Result<UserSearchDto>.Success(new UserSearchDto(target.Id, target.Username, avatarUrl, status));
    }

    private async Task<FriendshipStatus> ResolveStatusAsync(Guid targetId, CancellationToken ct)
    {
        if (await friendRepo.AreFriendsAsync(currentUser.UserId, targetId, ct))
            return FriendshipStatus.Friends;

        var pending = await friendRequestRepo.GetPendingBetweenAsync(currentUser.UserId, targetId, ct);
        if (pending is null) return FriendshipStatus.None;

        return pending.SenderId == currentUser.UserId
            ? FriendshipStatus.PendingSent
            : FriendshipStatus.PendingReceived;
    }
}
