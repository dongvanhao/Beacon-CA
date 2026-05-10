using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(Guid NotificationId, Guid CurrentUserId)
    : IRequest<Result<MarkReadResponse>>;
