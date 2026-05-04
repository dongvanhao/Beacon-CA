using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.AcceptFriendRequest;

public record AcceptFriendRequestCommand(Guid RequestId) : IRequest<Result>;
