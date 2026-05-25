using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.UpdateGroupMemberCustomName;

public record UpdateGroupMemberCustomNameCommand(Guid GroupId, Guid TargetUserId, string? CustomName)
    : IRequest<Result>;
