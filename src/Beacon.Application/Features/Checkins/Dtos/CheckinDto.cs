namespace Beacon.Application.Features.Checkins.Dtos;

public record CheckinDto(
    Guid Id,
    Guid DailySafetyRecordId,
    DateOnly CheckinDate,
    DateTime CheckedInAtUtc,
    string Type,
    string? Note,
    decimal? Latitude,
    decimal? Longitude,
    Guid? MediaObjectId);
