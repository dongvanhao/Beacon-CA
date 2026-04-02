using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Notification
{
    public enum NotificationChannel //Enum này dùng để biểu diễn các kênh hoặc phương thức mà hệ thống sử dụng để gửi thông báo đến người dùng hoặc người liên hệ.
    {
        Email = 1,
        Telegram = 2,
        Push = 3,
        Sms = 4
    }
}
