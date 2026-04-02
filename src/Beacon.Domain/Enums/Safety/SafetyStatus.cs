using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Safety
{
    public enum SafetyStatus
    {
        Pending = 1, //Đã tạo lịch/check-in nhưng chưa đến hạn hoặc đang chờ người dùng check-in
        CheckedIn = 2,//Người dùng đã check-in thành công
        Missed = 3,//Đến hạn check-in nhưng người dùng không check-in, có thể coi là nguy hiểm
        Alerted = 4,//Đã gửi cảnh báo đến người thân hoặc cơ quan chức năng do người dùng không check-in đúng hạn
        Resolved = 5//Người dùng đã được xác nhận an toàn sau khi cảnh báo, có thể do người thân liên hệ hoặc người dùng tự cập nhật tình trạng

    }
}
