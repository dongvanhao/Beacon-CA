namespace Beacon.Application.Features.Checkins.Dtos;

public record TodayCheckinStatusDto(
    bool HasCheckedIn,
    string Status,
    DateTime DeadlineAtUtc,
    long? RemainingSeconds,
    DateTime? CheckedInAtUtc,
    bool IsMonitoringEnabled,
    bool IsAutoAlertEnabled
);
