using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.FindUserByPhone;

public class FindUserByPhoneQueryHandler(
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IFriendRequestRepository friendRequestRepo,
    ICurrentUserService currentUser,
    IStorageService storage)
    : IRequestHandler<FindUserByPhoneQuery, Result<List<UserSearchDto>>>
{
    public async Task<Result<List<UserSearchDto>>> Handle(FindUserByPhoneQuery query, CancellationToken ct)
    {
        var users = await userRepo.SearchByNameOrPhoneAsync(
            query.Search, currentUser.UserId, query.Limit, ct);

        var results = new List<UserSearchDto>(users.Count);
        foreach (var user in users)
        {
            var status = await ResolveStatusAsync(user.Id, ct);

            string? avatarUrl = null;
            if (user.AvatarMediaObject is not null)
                avatarUrl = await storage.GeneratePresignedGetUrlAsync(user.AvatarMediaObject.ObjectKey, ct);

            results.Add(new UserSearchDto(user.Id, user.FamilyName, user.GivenName, avatarUrl, status));
        }

        return Result<List<UserSearchDto>>.Success(results);
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
