using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberMuted;

public record UpdateGroupMemberMutedCommand(Guid GroupId, bool IsMuted) : IRequest<Result>;
