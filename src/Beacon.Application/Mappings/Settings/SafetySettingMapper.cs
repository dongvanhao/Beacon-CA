using Beacon.Application.Features.Settings.Dtos;
using Beacon.Domain.Entities.Setting;

namespace Beacon.Application.Mappings.Settings;

public sealed class SafetySettingMapper
{
    public SafetySettingDto ToDto(SafetySetting s) => new(
        DailyDeadlineLocalTime: s.DailyDeadlineLocalTime.ToString("HH:mm"),
        GracePeriodMinutes:     s.GracePeriodMinutes,
        ReminderBeforeMinutes:  s.ReminderBeforeMinutes,
        AutoAlertDelayMinutes:  s.AutoAlertDelayMinutes,
        IsMonitoringEnabled:    s.IsMonitoringEnabled,
        IsAutoAlertEnabled:     s.IsAutoAlertEnabled,
        IsDefault:              false);

    public SafetySettingDto ToDefaultDto() => new(
        DailyDeadlineLocalTime: "23:59",
        GracePeriodMinutes:     15,
        ReminderBeforeMinutes:  30,
        AutoAlertDelayMinutes:  15,
        IsMonitoringEnabled:    true,
        IsAutoAlertEnabled:     true,
        IsDefault:              true);
}
