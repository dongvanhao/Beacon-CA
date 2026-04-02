using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Enums.Checkins
{
    public  enum AlertIncidentType //Enum này dùng để biểu diễn loại nguyên nhân hoặc bối cảnh phát sinh cảnh báo.
    {
        MissedCheckin = 1, // Cảnh báo phát sinh do người dùng không check-in đúng hạn
        ManualEmergency = 2, // Cảnh báo phát sinh do người dùng chủ động gửi tín hiệu khẩn cấp
        FollowUp = 3 // Cảnh báo tiếp diễn khi người dùng chưa xác nhận an toàn sau cảnh báo trước đó
    }
}
