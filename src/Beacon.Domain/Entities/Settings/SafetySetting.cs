using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Setting;

public class SafetySetting : AuditableEntity
{
    public Guid UserId { get; private set; }

    public TimeOnly DailyDeadlineLocalTime { get; private set; }
    public int GracePeriodMinutes { get; private set; } = 15;
    public int ReminderBeforeMinutes { get; private set; } = 30;
    public int AutoAlertDelayMinutes { get; private set; } = 15;

    public bool IsMonitoringEnabled { get; private set; } = true;
    public bool IsAutoAlertEnabled { get; private set; } = true;

    public User User { get; private set; } = default!;

    protected SafetySetting() { }
}
