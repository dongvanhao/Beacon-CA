using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Notification
{
    public class NotificationDelivery : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public Guid? AlertIncidentId { get; private set; } //Nếu có, liên kết đến AlertIncident để biết thông tin về sự cố an toàn liên quan đến thông báo, bao gồm mô tả sự cố, mức độ nghiêm trọng, v.v.
        public Guid? EmergencyContactId { get; private set; } //Nếu có, liên kết đến EmergencyContact để biết thông tin về người nhận thông báo, bao gồm tên, số điện thoại, v.v.
        public Guid? UserDeviceId { get; private set; } //Nếu có, liên kết đến UserDevice để biết thông tin về thiết bị của người dùng mà thông báo được gửi đến, bao gồm loại thiết bị, hệ điều hành, v.v.

        public NotificationKind Kind { get; private set; } //Loại thông báo, có thể là nhắc nhở check-in, cảnh báo trễ hạn, v.v., giúp hệ thống xác định cách xử lý và hiển thị thông báo cho người dùng.
        public NotificationChannel Channel { get; private set; } //Kênh gửi thông báo, có thể là email, SMS, push notification, v.v., giúp hệ thống xác định cách gửi thông báo đến người dùng.
        public NotificationStatus Status { get; private set; } //Trạng thái của thông báo, có thể là đang chờ gửi, đã gửi, thất bại, v.v., giúp hệ thống theo dõi và quản lý quá trình gửi thông báo.
        public string Recipient { get; private set; } = default!; //Thông tin về người nhận thông báo, có thể là số điện thoại, địa chỉ email, v.v., giúp hệ thống biết được nơi gửi thông báo đến.
        public string Title { get; private set; } = default!; //Tiêu đề của thông báo, giúp người nhận nhanh chóng hiểu được nội dung chính của thông báo khi nhận được.
        public string Body { get; private set; } = default!; //Nội dung chi tiết của thông báo, giúp người nhận hiểu rõ hơn về lý do và hành động cần thực hiện khi nhận được thông báo.

        public int AttempCount { get; private set; } = 0; //Số lần đã cố gắng gửi thông báo, giúp hệ thống theo dõi và quyết định khi nào nên dừng cố gắng gửi thông báo nếu liên tục thất bại.
        
        public DateTime? SentAtUtc { get; private set; } //Thời điểm thông báo đã được gửi đi, giúp hệ thống theo dõi và quản lý lịch sử gửi thông báo.
        public DateTime? FailedAtUtc { get; private set; } //Thời điểm thông báo đã gửi thất bại, giúp hệ thống theo dõi và quản lý lịch sử gửi thông báo, cũng như quyết định khi nào nên dừng cố gắng gửi thông báo nếu liên tục thất bại.

        public string? FailureReason { get; private set; } //Lý do thất bại khi gửi thông báo, giúp hệ thống hiểu được nguyên nhân của việc gửi thông báo thất bại và có thể cải thiện quá trình gửi thông báo trong tương lai.
        public string? ProviderMessageId { get; private set; } //ID của thông báo do nhà cung cấp dịch vụ gửi thông báo trả về, giúp hệ thống theo dõi và quản lý lịch sử gửi thông báo, cũng như hỗ trợ trong việc xử lý khiếu nại hoặc vấn đề liên quan đến thông báo.

        public User User { get; private set; } = default!;
        public AlertIncident? AlertIncident { get; private set; }
        public EmergencyContact? EmergencyContact { get; private set; }
        public UserDevice? UserDevice { get; private set; }

        protected NotificationDelivery() { }

        public static NotificationDelivery Create(Guid userId, NotificationKind kind,
            NotificationChannel channel, string recipient, string title, string body,
            Guid? alertIncidentId = null, Guid? emergencyContactId = null, Guid? userDeviceId = null)
            => new()
            {
                UserId = userId,
                Kind = kind,
                Channel = channel,
                Status = NotificationStatus.Pending,
                Recipient = recipient,
                Title = title,
                Body = body,
                AlertIncidentId = alertIncidentId,
                EmergencyContactId = emergencyContactId,
                UserDeviceId = userDeviceId
            };

        public void MarkSent(string? providerMessageId = null)
        {
            SentAtUtc = DateTime.UtcNow;
            Status = NotificationStatus.Sent;
            ProviderMessageId = providerMessageId;
        }

        public void MarkFailed(string reason)
        {
            FailedAtUtc = DateTime.UtcNow;
            FailureReason = reason;
            Status = NotificationStatus.Failed;
        }

        public void IncrementAttempt() => AttempCount++;
    }
}
