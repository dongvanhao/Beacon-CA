namespace Beacon.Application.Features.Settings.Dtos;

public class UpdateSafetySettingRequest
{
    public string? DailyDeadlineLocalTime { get; set; }
    public int? GracePeriodMinutes { get; set; }
    public int? ReminderBeforeMinutes { get; set; }
    public int? AutoAlertDelayMinutes { get; set; }
    public bool? IsMonitoringEnabled { get; set; }
    public bool? IsAutoAlertEnabled { get; set; }
}
