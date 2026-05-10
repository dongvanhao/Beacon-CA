using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.AddGroupMember;

public record AddGroupMemberCommand(Guid GroupId, Guid TargetUserId) : IRequest<Result>;
