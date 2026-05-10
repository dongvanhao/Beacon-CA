using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Events;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.AcceptFriendRequest;

public class AcceptFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    IFriendRepository friendRepo,
    IMediator mediator,
    ICurrentUserService currentUser,
    INotificationService notificationService)
    : IRequestHandler<AcceptFriendRequestCommand, Result>
{
    public async Task<Result> Handle(AcceptFriendRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
        if (request is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "Lời mời kết bạn không tồn tại."));

        if (request.ReceiverUserId != currentUser.UserId)
            return Result.Failure(Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "Bạn không có quyền thực hiện hành động này."));

        if (request.Status != FriendRequestStatus.Pending)
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời kết bạn không ở trạng thái chờ."));

        // FIX-06: Friend.Create() handles normalized pair — no MessageGroupId needed
        var friend = Friend.Create(request.InitiatorId, currentUser.UserId);

        // TryAddAsync: saves atomically; returns false on unique constraint violation
        if (!await friendRepo.TryAddAsync(friend, ct))
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời đã được xử lý bởi một yêu cầu khác."));

        // FIX-05: RowVersion on FriendRequest — repo throws ConflictException on concurrent update
        request.Accept();
        await requestRepo.SaveChangesAsync(ct);

        // FIX-04: publish event — CreateDirectMessageGroupHandler creates the DM group
        await mediator.Publish(new FriendRequestAcceptedEvent(
            request.Id, request.InitiatorId, currentUser.UserId, friend.Id), ct);

        var accepterName = $"{currentUser.GivenName} {currentUser.FamilyName}".Trim();
        await notificationService.CreateAndDeliverAsync(
            request.InitiatorId,
            NotificationType.FriendAccepted,
            "Lời mời kết bạn đã được chấp nhận",
            $"{accepterName} đã chấp nhận lời mời kết bạn của bạn",
            ct: ct);

        return Result.Success();
    }
}
