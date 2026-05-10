using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.MarkGroupMessagesSeen;

public record MarkGroupMessagesSeenCommand(Guid GroupId, Guid UserId, Guid LastSeenMessageId)
    : IRequest<Result>;
