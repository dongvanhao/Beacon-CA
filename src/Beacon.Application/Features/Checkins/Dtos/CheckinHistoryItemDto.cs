namespace Beacon.Application.Features.Checkins.Dtos;

public record CheckinHistoryItemDto(
    Guid Id,
    Guid DailySafetyRecordId,
    DateOnly CheckinDate,
    DateTime CheckedInAtUtc,
    string Type,
    string? Note,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<CheckinMediaItemDto> MediaItems);
