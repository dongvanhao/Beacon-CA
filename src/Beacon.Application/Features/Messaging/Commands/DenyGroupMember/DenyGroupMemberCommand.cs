using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.DenyGroupMember;

public record DenyGroupMemberCommand(Guid GroupId, Guid TargetUserId) : IRequest<Result>;
