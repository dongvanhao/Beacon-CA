using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Setting
{
    #region Dùng Để Làm gì?
    /*
     * Đây là phần Setting về thông báo của User
     */
    #endregion
    public class NotificationPreference : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public bool IsPushEnabled { get; private set; } = true;
        public bool IsEmailEnabled { get; private set; } = false;
        public bool IsTelegramEnabled { get; private set; } = false;

        public bool SendReminders { get; private set; } = true;
        public bool SendMissedCheckInAlert { get; private set; } = true;
        public bool SendResolvedAlert { get; private set; } = true;

        public bool QuietHoursEnabled { get; private set; } = false;
        public TimeOnly? QuietHoursStartLocalTime { get; private set; }
        public TimeOnly? QuietHoursEndLocalTime { get; private set; }

        public User User { get; private set; } = default!;
        protected NotificationPreference() { }
    }
}
