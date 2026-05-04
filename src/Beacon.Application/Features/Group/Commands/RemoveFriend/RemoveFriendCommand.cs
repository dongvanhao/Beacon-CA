using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.RemoveFriend;

public record RemoveFriendCommand(Guid TargetUserId) : IRequest<Result>;
