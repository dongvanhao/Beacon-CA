using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.ApproveGroupMember;

public record ApproveGroupMemberCommand(Guid GroupId, Guid TargetUserId) : IRequest<Result>;
