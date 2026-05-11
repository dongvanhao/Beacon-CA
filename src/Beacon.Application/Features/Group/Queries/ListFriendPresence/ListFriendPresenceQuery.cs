using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListFriendPresence;

public record ListFriendPresenceQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<FriendPresenceDto>>>;
