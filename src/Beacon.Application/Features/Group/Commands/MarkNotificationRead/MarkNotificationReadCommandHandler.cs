using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.MarkNotificationRead;

public class MarkNotificationReadCommandHandler(INotificationRepository repo)
    : IRequestHandler<MarkNotificationReadCommand, Result<MarkReadResponse>>
{
    public async Task<Result<MarkReadResponse>> Handle(
        MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await repo.GetByIdAsync(cmd.NotificationId, ct);
        if (notification is null)
            return Result<MarkReadResponse>.Failure(
                Error.NotFound(ErrorCodes.Notification.NOTIFICATION_NOT_FOUND, "Thông báo không tồn tại."));

        if (notification.ReceiverUserId != cmd.CurrentUserId)
            return Result<MarkReadResponse>.Failure(
                Error.Forbidden(ErrorCodes.Notification.NOTIFICATION_FORBIDDEN, "Bạn không có quyền đọc thông báo này."));

        notification.MarkRead();
        await repo.SaveChangesAsync(ct);

        var unreadCount = await repo.CountUnreadAsync(cmd.CurrentUserId, ct);
        return Result<MarkReadResponse>.Success(new MarkReadResponse(unreadCount));
    }
}
