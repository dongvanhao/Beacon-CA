using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Checkins
{
    public enum CheckinType //Enum này dùng để biểu diễn kiểu hoặc nguồn phát sinh của một lần check-in.
    {
        Manual = 1, // Người dùng chủ động check-in
        Recovery = 2, // Hệ thống ghi nhận sau khi người dùng hồi phục/an toàn trở lại
        Emergency = 3 // Hệ thống ghi nhận khi phát hiện tình huống khẩn cấp
    }
}
