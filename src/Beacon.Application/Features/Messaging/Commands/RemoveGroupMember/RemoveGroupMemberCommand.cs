using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.RemoveGroupMember;

public record RemoveGroupMemberCommand(Guid GroupId, Guid TargetUserId) : IRequest<Result>;
