using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Commands.SendFriendRequest;

public record SendFriendRequestCommand(Guid ReceiverId) : IRequest<Result<FriendRequestDto>>;
