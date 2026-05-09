using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.CancelFriendRequest;

public record CancelFriendRequestCommand(Guid RequestId) : IRequest<Result>;
