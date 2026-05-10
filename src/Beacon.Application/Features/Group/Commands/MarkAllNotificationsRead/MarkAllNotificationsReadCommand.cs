using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand(Guid CurrentUserId)
    : IRequest<Result<MarkReadResponse>>;
