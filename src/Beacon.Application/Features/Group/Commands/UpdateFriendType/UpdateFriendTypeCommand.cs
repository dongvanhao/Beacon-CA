using Beacon.Domain.Enums.Group;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.UpdateFriendType;

public record UpdateFriendTypeCommand(Guid TargetUserId, FriendType NewType) : IRequest<Result>;
