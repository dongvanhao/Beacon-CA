using Beacon.Application.Features.Group.Dtos;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListNotifications;

public class ListNotificationsQueryHandler(INotificationRepository repo)
    : IRequestHandler<ListNotificationsQuery, Result<NotificationListResponse>>
{
    public async Task<Result<NotificationListResponse>> Handle(
        ListNotificationsQuery q, CancellationToken ct)
    {
        var limit = Math.Min(q.Limit, 50);
        var items = await repo.ListByReceiverAsync(q.CurrentUserId, q.Cursor, limit + 1, ct);
        var unreadCount = await repo.CountUnreadAsync(q.CurrentUserId, ct);

        var hasNextPage = items.Count > limit;
        if (hasNextPage) items = items.Take(limit).ToList();

        DateTime? nextCursor = hasNextPage ? items[^1].CreatedAtUtc : null;

        var dtos = items.Select(n => new NotificationDto(
            n.Id,
            n.Type,
            n.Title,
            n.Body,
            n.Data,
            n.IsRead,
            n.ReadAtUtc,
            n.CreatedAtUtc)).ToList();

        return Result<NotificationListResponse>.Success(
            new NotificationListResponse(dtos, nextCursor, hasNextPage, unreadCount));
    }
}
