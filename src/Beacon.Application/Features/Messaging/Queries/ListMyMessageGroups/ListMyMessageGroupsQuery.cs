using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Messaging.Queries.ListMyMessageGroups;

public record ListMyMessageGroupsQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<MessageGroupDto>>>;
