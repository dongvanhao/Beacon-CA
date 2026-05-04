using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.DeclineFriendRequest;

public class DeclineFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<DeclineFriendRequestCommand, Result>
{
    public async Task<Result> Handle(DeclineFriendRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
        if (request is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "Lời mời kết bạn không tồn tại."));

        if (request.ReceiverId != currentUser.UserId)
            return Result.Failure(Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "Bạn không có quyền thực hiện hành động này."));

        if (request.Status != FriendRequestStatus.Pending)
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời kết bạn không ở trạng thái chờ."));

        request.Status = FriendRequestStatus.Declined;
        await requestRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
