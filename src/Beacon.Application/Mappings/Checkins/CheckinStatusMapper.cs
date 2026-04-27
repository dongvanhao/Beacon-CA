using Beacon.Application.Features.Checkins.Dtos;

namespace Beacon.Application.Mappings.Checkins;

public sealed class CheckinStatusMapper
{
    public TodayCheckinStatusDto ToStatusDto(
        DateTime deadlineAtUtc,
        DateTime? checkedInAtUtc,
        bool hasCheckedIn,
        bool isMonitoringEnabled,
        bool isAutoAlertEnabled)
    {
        if (hasCheckedIn)
            return new(true, CheckinDailyStatus.CheckedIn, deadlineAtUtc, null, checkedInAtUtc,
                isMonitoringEnabled, isAutoAlertEnabled);

        // Monitoring tắt: không countdown, không overdue
        if (!isMonitoringEnabled)
            return new(false, CheckinDailyStatus.Pending, deadlineAtUtc, null, null,
                isMonitoringEnabled, isAutoAlertEnabled);

        var remainingSeconds = (long)(deadlineAtUtc - DateTime.UtcNow).TotalSeconds;
        var status = remainingSeconds >= 0 ? CheckinDailyStatus.Pending : CheckinDailyStatus.Overdue;
        return new(false, status, deadlineAtUtc, remainingSeconds, null, isMonitoringEnabled, isAutoAlertEnabled);
    }
}
