using Beacon.Domain.Enums.Checkins;

namespace Beacon.Application.Features.Checkins.Dtos;

public record CreateCheckinRequest(
    CheckinType Type,
    string? Note,
    decimal? Latitude,
    decimal? Longitude,
    Guid? MediaId);
