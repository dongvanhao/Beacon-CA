using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Safety
{ //Lưu danh sách người liên hệ khẩn cấp của User, có thể là người thân, bạn bè, đồng nghiệp, v.v. để hệ thống có thể gửi thông báo khi cần thiết.
    public class EmergencyContact : SoftDeletableEntity
    {
        public Guid UserId { get; private set; }
        public string FullName { get; private set; } = default!;
        public string ContactValue { get; private set; } = default!;
        public string? Relationship { get; private set; }

        public ContactChannelType ChannelType { get; private set; } // Loại kênh liên hệ (ví dụ: Email, SMS, Điện thoại) để hệ thống biết cách gửi thông báo đến người liên hệ này.
        public int PriorityOrder { get; private set; } = 1; // Thứ tự ưu tiên khi gửi thông báo đến các liên hệ khẩn cấp, giúp hệ thống xác định người liên hệ nào nên được thông báo trước khi thông báo đến các liên hệ khác.

        public bool IsPrimary { get; private set; } = false;// Có thể dùng để đánh dấu người liên hệ này là liên hệ chính, giúp hệ thống ưu tiên gửi thông báo đến người này trước khi gửi đến các liên hệ khác.
        public bool IsActive { get; private set; } = true;// Trạng thái kích hoạt của người liên hệ khẩn cấp, giúp người dùng quản lý và cập nhật thông tin liên hệ một cách linh hoạt.
        public bool IsVerified { get; private set; } = false; // Có thể dùng để xác định xem người liên hệ này đã được xác minh (ví dụ: qua email hoặc SMS) hay chưa, giúp đảm bảo rằng thông tin liên hệ là chính xác và có thể sử dụng khi cần thiết.

        public User User { get; private set; } = default!;
        public ICollection<NotificationDelivery> NotificationDeliveries { get; private set; } = new List<NotificationDelivery>();

        protected EmergencyContact() { }

        public static EmergencyContact Create(Guid userId, string fullName, string contactValue,
            ContactChannelType channelType, string? relationship = null, int priorityOrder = 1)
            => new()
            {
                UserId = userId,
                FullName = fullName,
                ContactValue = contactValue,
                ChannelType = channelType,
                Relationship = relationship,
                PriorityOrder = priorityOrder
            };

        public void SetAsPrimary() => IsPrimary = true;
        public void Verify() => IsVerified = true;
        public void Deactivate() => IsActive = false;
    }
}
