using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums
{
    public enum ContactChannelType //Enum này dùng để biểu diễn kênh liên lạc hoặc phương thức gửi thông báo đến người liên hệ.
    {
        Email = 1,
        Telegram = 2,
        Sms = 3,
        Phone =4
    }
}
