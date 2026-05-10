using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateTypingStatus;

public record UpdateTypingStatusCommand(Guid GroupId, Guid UserId, bool IsTyping)
    : IRequest<Result>;
