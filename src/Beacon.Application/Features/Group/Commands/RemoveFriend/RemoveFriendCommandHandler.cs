using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.RemoveFriend;

public class RemoveFriendCommandHandler(
    IFriendRepository friendRepo,
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<RemoveFriendCommand, Result>
{
    public async Task<Result> Handle(RemoveFriendCommand command, CancellationToken ct)
    {
        var friend = await friendRepo.GetByUsersAsync(currentUser.UserId, command.TargetUserId, ct);
        if (friend is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_NOT_FOUND, "Không tìm thấy thông tin bạn bè."));

        
        var group = await groupRepo.GetPrivateGroupBetweenAsync(friend.UserId1, friend.UserId2, ct);
        if (group is not null)
        {
            await groupRepo.RemoveMemberAsync(group.Id, friend.UserId1, ct);
            await groupRepo.RemoveMemberAsync(group.Id, friend.UserId2, ct);
        
            group.Delete();
        }

        // Remove the friend record
        await friendRepo.DeleteAsync(friend, ct);

        // Single SaveChangesAsync — flushes all tracked changes atomically
        await friendRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
