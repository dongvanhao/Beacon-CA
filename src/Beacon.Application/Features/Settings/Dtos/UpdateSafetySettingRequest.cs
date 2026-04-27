namespace Beacon.Application.Features.Settings.Dtos;

public record UpdateSafetySettingRequest(
    string DailyDeadlineLocalTime,
    int GracePeriodMinutes,
    int ReminderBeforeMinutes,
    int AutoAlertDelayMinutes,
    bool IsMonitoringEnabled,
    bool IsAutoAlertEnabled
);
