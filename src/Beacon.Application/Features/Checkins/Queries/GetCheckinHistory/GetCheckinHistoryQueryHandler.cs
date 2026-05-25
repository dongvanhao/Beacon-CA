using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using MediatR;

namespace Beacon.Application.Features.Checkins.Queries.GetCheckinHistory;

public class GetCheckinHistoryQueryHandler(
    ICheckinRepository checkinRepo,
    CheckinHistoryMapper mapper)
    : IRequestHandler<GetCheckinHistoryQuery, Result<CursorPagedResult<CheckinHistoryItemDto>>>
{
    public async Task<Result<CursorPagedResult<CheckinHistoryItemDto>>> Handle(
        GetCheckinHistoryQuery query, CancellationToken ct)
    {
        var pagedCheckins = await checkinRepo.GetPagedByUserIdAsync(
            query.UserId, query.Cursor, query.Limit, ct);

        return Result<CursorPagedResult<CheckinHistoryItemDto>>.Success(
            mapper.ToPagedDto(pagedCheckins));
    }
}
