using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.GetFriendDetail;

public record GetFriendDetailQuery(Guid TargetUserId) : IRequest<Result<FriendDto>>;
