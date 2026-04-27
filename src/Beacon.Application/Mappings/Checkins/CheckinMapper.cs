using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Domain.Entities.Checkins;

namespace Beacon.Application.Mappings.Checkins;

public sealed class CheckinMapper
{
    public CheckinDto ToDto(Checkin checkin, Guid? mediaObjectId = null) => new(
        checkin.Id,
        checkin.DailySafetyRecordId,
        checkin.CheckinDate,
        checkin.CheckedInAtUtc,
        checkin.Type.ToString(),
        checkin.Note,
        checkin.Latitude,
        checkin.Longitude,
        mediaObjectId);
}
