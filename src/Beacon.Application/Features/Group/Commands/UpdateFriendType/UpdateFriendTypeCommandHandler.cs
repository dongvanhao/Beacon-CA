using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.UpdateFriendType;

public class UpdateFriendTypeCommandHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateFriendTypeCommand, Result>
{
    public async Task<Result> Handle(UpdateFriendTypeCommand command, CancellationToken ct)
    {
        var friend = await friendRepo.GetByUsersAsync(currentUser.UserId, command.TargetUserId, ct);
        if (friend is null)
            return Result.Failure(
                Error.NotFound(ErrorCodes.Friend.FRIEND_NOT_FOUND, "Không tìm thấy thông tin bạn bè."));

        friend.Type = command.NewType;
        await friendRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
