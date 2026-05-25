using Beacon.Domain.Enums.Messaging;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.TransferOwnership;

public record TransferOwnershipCommand(Guid GroupId, Guid TargetUserId, GroupMemberRole Role) : IRequest<Result>;
