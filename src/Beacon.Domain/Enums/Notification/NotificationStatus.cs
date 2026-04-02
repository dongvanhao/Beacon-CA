using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Notification
{
    public enum NotificationStatus //Enum này dùng để biểu diễn trạng thái của một thông báo từ lúc tạo ra đến lúc hoàn tất hoặc thất bại.
    {
        Pending = 1, // Thông báo mới được tạo và chưa được gửi đi
        Sent = 2, // Thông báo đã được gửi đến người nhận nhưng chưa được xác nhận
        Failed = 3 // Gửi thông báo thất bại
    }
}
