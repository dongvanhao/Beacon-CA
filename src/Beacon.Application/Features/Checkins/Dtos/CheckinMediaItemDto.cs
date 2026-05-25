namespace Beacon.Application.Features.Checkins.Dtos;

public record CheckinMediaItemDto(
    Guid MediaObjectId,
    bool IsPrimary,
    int SortOrder,
    string? Caption);
