using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListNotifications;

public record ListNotificationsQuery(Guid CurrentUserId, DateTime? Cursor, int Limit)
    : IRequest<Result<NotificationListResponse>>;
