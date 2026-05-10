using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.SendFriendRequest;

public class SendFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    IFriendRepository friendRepo,
    IUserRepository userRepo,
    ICurrentUserService currentUser,
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    FriendRequestMapper mapper)
    : IRequestHandler<SendFriendRequestCommand, Result<FriendRequestDto>>
{
    public async Task<Result<FriendRequestDto>> Handle(
        SendFriendRequestCommand command, CancellationToken ct)
    {
        var senderId = currentUser.UserId;

        if (senderId == command.ReceiverId)
            return Result<FriendRequestDto>.Failure(
                Error.Validation(ErrorCodes.Friend.SELF_FRIEND_REQUEST, "Không thể gửi lời mời kết bạn cho chính mình."));

        if (!await userRepo.ExistsAsync(command.ReceiverId, ct))
            return Result<FriendRequestDto>.Failure(
                Error.NotFound(ErrorCodes.Identity.USER_NOT_FOUND, "Người dùng không tồn tại."));

        if (await requestRepo.HasPendingBetweenAsync(senderId, command.ReceiverId, ct))
            return Result<FriendRequestDto>.Failure(
                Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_DUPLICATE, "Đã gửi lời mời kết bạn."));

        if (await friendRepo.AreFriendsAsync(senderId, command.ReceiverId, ct))
            return Result<FriendRequestDto>.Failure(
                Error.Conflict(ErrorCodes.Friend.ALREADY_FRIENDS, "Hai người đã là bạn bè."));

        var request = FriendRequest.Create(senderId, command.ReceiverId);

        await requestRepo.AddAsync(request, ct);
        await requestRepo.SaveChangesAsync(ct);

        var senderName = $"{currentUser.GivenName} {currentUser.FamilyName}".Trim();
        var notification = Notification.Create(
            command.ReceiverId,
            NotificationType.FriendRequest,
            "Lời mời kết bạn",
            $"{senderName} đã gửi lời mời kết bạn");
        await notifRepo.AddAsync(notification, ct);
        await notifRepo.SaveChangesAsync(ct);

        await notifier.NotifyUserAsync(
            command.ReceiverId,
            new NotificationPayload(notification.Id, nameof(NotificationType.FriendRequest), notification.Title, notification.Body, null),
            ct);

        return Result<FriendRequestDto>.Success(mapper.ToDto(request, currentUser.FamilyName, currentUser.GivenName, null));
    }
}
