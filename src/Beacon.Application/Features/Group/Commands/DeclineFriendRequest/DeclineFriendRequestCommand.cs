using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.DeclineFriendRequest;

public record DeclineFriendRequestCommand(Guid RequestId) : IRequest<Result>;
