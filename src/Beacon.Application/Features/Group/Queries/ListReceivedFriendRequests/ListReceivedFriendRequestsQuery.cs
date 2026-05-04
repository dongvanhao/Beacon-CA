using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListReceivedFriendRequests;

public record ListReceivedFriendRequestsQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<FriendRequestDto>>>;
