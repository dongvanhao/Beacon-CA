using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Domain.Entities.Checkins;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Application.Mappings.Checkins;

public sealed class CheckinHistoryMapper
{
    public CheckinHistoryItemDto ToDto(Checkin checkin) => new(
        checkin.Id,
        checkin.DailySafetyRecordId,
        checkin.CheckinDate,
        checkin.CheckedInAtUtc,
        checkin.Type.ToString(),
        checkin.Note,
        checkin.Latitude,
        checkin.Longitude,
        checkin.MediaItems
            .Select(m => new CheckinMediaItemDto(m.MediaObjectId, m.IsPrimary, m.SortOrder, m.Caption))
            .ToList());

    public CursorPagedResult<CheckinHistoryItemDto> ToPagedDto(CursorPagedResult<Checkin> pagedCheckins) =>
        new()
        {
            Data = pagedCheckins.Data.Select(ToDto).ToList(),
            Meta = pagedCheckins.Meta
        };
}
