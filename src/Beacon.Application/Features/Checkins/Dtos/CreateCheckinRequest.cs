namespace Beacon.Application.Features.Checkins.Dtos;

public record CreateCheckinRequest(
    string? Note,
    decimal? Latitude,
    decimal? Longitude,
    Guid? MediaId);
