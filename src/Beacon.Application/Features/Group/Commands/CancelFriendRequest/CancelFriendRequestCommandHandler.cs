using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.CancelFriendRequest;

public class CancelFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelFriendRequestCommand, Result>
{
    public async Task<Result> Handle(CancelFriendRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
        if (request is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "Lời mời kết bạn không tồn tại."));

        if (request.InitiatorId != currentUser.UserId)
            return Result.Failure(Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "Chỉ người gửi mới có thể hủy lời mời."));

        if (request.Status != FriendRequestStatus.Pending)
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời không còn ở trạng thái chờ."));

        request.Cancel();
        await requestRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}
