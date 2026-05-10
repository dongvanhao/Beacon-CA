using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.SearchUsers;

public class SearchUsersQueryHandler(
    IUserRepository userRepo,
    IFriendRepository friendRepo,
    IFriendRequestRepository friendRequestRepo,
    ICurrentUserService currentUser,
    IStorageService storage)
    : IRequestHandler<SearchUsersQuery, Result<List<UserSearchDto>>>
{
    public async Task<Result<List<UserSearchDto>>> Handle(SearchUsersQuery query, CancellationToken ct)
    {
        var users = await userRepo.SearchByNameOrPhoneAsync(
            query.Search, currentUser.UserId, query.Limit, ct);

        if (users.Count == 0) return Result<List<UserSearchDto>>.Success([]);

        var targetIds = users.Select(u => u.Id).ToList();

        var friendIds = await friendRepo.GetFriendIdsAsync(currentUser.UserId, targetIds, ct);
        var pendingRequests = await friendRequestRepo.GetPendingBetweenBatchAsync(currentUser.UserId, targetIds, ct);

        var avatarObjects = users
            .Where(u => u.AvatarMediaObject is not null)
            .Select(u => u.AvatarMediaObject!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var results = users.Select(user =>
        {
            var status = ResolveStatus(user.Id, friendIds, pendingRequests);
            var avatarUrl = user.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(user.AvatarMediaObjectId.Value, out var url) ? url : null;
            return new UserSearchDto(user.Id, user.FamilyName, user.GivenName, avatarUrl, status);
        }).ToList();

        return Result<List<UserSearchDto>>.Success(results);
    }

    private FriendshipStatus ResolveStatus(
        Guid targetId,
        HashSet<Guid> friendIds,
        Dictionary<Guid, FriendRequest> pendingRequests)
    {
        if (friendIds.Contains(targetId)) return FriendshipStatus.Friends;
        if (!pendingRequests.TryGetValue(targetId, out var req)) return FriendshipStatus.None;
        return req.InitiatorId == currentUser.UserId ? FriendshipStatus.PendingSent : FriendshipStatus.PendingReceived;
    }
}
