using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Queries.GetCheckinHistory;

public record GetCheckinHistoryQuery(Guid UserId, DateTimeOffset? Cursor, int Limit)
    : IRequest<Result<CursorPagedResult<CheckinHistoryItemDto>>>;
