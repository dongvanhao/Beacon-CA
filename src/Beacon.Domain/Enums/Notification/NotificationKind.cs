using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Notification
{
    public enum NotificationKind //NotificationKind dùng để phân loại loại thông báo trong hệ thống.
    {
        Reminder = 1, // Thông báo nhắc nhở người dùng thực hiện check-in định kỳ
        Alert = 2, // Thông báo cảnh báo khi người dùng không check-in đúng hạn hoặc có tình huống khẩn cấp
        FollowUp = 3, // Thông báo tiếp theo khi người dùng chưa xác nhận an toàn sau cảnh báo trước đó
        EmergencyAlert = 4 // Thông báo khẩn cấp gửi đến liên hệ khẩn cấp khi người dùng chưa checkin
    }
}
