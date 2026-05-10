using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Commands.LeaveGroup;

public record LeaveGroupCommand(Guid GroupId) : IRequest<Result>;
