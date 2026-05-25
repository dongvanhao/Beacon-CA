using Beacon.Application.Features.Group.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Group.Queries.ListFriends;

public record ListFriendsQuery(DateTime? Cursor, int Limit = 20, string? Search = null)
    : IRequest<Result<CursorPagedResult<FriendDto>>>;
