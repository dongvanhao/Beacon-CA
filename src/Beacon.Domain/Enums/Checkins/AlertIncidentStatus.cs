using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Checkins
{
    public enum AlertIncidentStatus //Enum này dùng để biểu diễn trạng thái xử lý của một cảnh báo/sự cố từ lúc tạo ra đến lúc hoàn tất hoặc thất bại.
    {
        Pending = 1,// Cảnh báo mới được tạo và chưa được gửi đi
        Sent = 2,// Cảnh báo đã được gửi đến người nhận nhưng chưa được xác nhận
        Acknowledged = 3,// Người nhận đã xác nhận đã nhận được cảnh báo
        Resolved = 4, // Sự việc đã được xác nhận là đã xử lý xong / người dùng đã an toàn
        Failed = 5 // Gửi cảnh báo thất bại
    }
}
