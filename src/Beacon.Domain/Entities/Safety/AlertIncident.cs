using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Enums.Checkins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Safety
{
    public class AlertIncident : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public Guid DailySafetyRecordId { get; private set; }
        public AlertIncidentType Type { get; private set; } //Loại sự cố cảnh báo, có thể là "MissedCheckin" (bỏ lỡ check-in), "LateCheckin" (check-in trễ hạn), hoặc "NoResponse" (không phản hồi), giúp hệ thống phân loại và xử lý các sự cố an toàn một cách hiệu quả.
        public AlertIncidentStatus Status { get; private set; } = AlertIncidentStatus.Pending; //Trạng thái hiện tại của sự cố cảnh báo, có thể là "Active" (đang hoạt động), "Resolved" (đã giải quyết), hoặc "Dismissed" (bị loại bỏ), giúp theo dõi và quản lý quá trình xử lý sự cố an toàn.
        
        public string? Message { get; private set; } //Thông điệp chi tiết về sự cố cảnh báo, có thể bao gồm thông tin về nguyên nhân, hậu quả, hoặc hướng dẫn xử lý, giúp người dùng và hệ thống hiểu rõ hơn về tình huống và cách phản ứng phù hợp.

        public DateTime TriggereAtUtc { get; private set; } //Thời điểm mà sự cố cảnh báo được kích hoạt, có thể dùng để theo dõi và đánh giá thời gian phản ứng của người dùng hoặc hệ thống đối với sự cố an toàn.
        public DateTime? SentAtUtc { get; private set; } //Thời điểm mà thông báo cảnh báo đã được gửi đến người dùng, có thể dùng để kiểm tra xem người dùng đã nhận được cảnh báo hay chưa và đánh giá hiệu quả của việc gửi thông báo.
        public DateTime? AcknowledgedAtUtc { get; private set; } //Thời điểm mà người dùng đã xác nhận đã nhận được cảnh báo, có thể dùng để kiểm tra xem người dùng đã phản hồi hay chưa và đánh giá mức độ quan tâm của người dùng đối với cảnh báo an toàn.
        public DateTime? ResolvedAtUtc { get; private set; } //Thời điểm mà sự cố cảnh báo đã được giải quyết, có thể dùng để kiểm tra xem người dùng đã được xác nhận an toàn hay chưa và đánh giá hiệu quả của quá trình xử lý sự cố.

        public string? FailureReason { get; private set; } //Thông tin chi tiết về lý do thất bại của sự cố cảnh báo, có thể bao gồm nguyên nhân cụ thể, tình huống xảy ra, hoặc các yếu tố liên quan khác, giúp người dùng và hệ thống hiểu rõ

        public User User { get; private set; } = default!;
        public DailySafetyRecord DailySafetyRecord { get; private set; } = default!;
        public ICollection<NotificationDelivery> NotificationDeliveries { get; private set; } = new List<NotificationDelivery>();
        protected AlertIncident() { }

        public static AlertIncident Create(Guid userId, Guid dailySafetyRecordId,
            AlertIncidentType type, string? message = null)
            => new()
            {
                UserId = userId,
                DailySafetyRecordId = dailySafetyRecordId,
                Type = type,
                Message = message,
                TriggereAtUtc = DateTime.UtcNow
            };

        public void MarkSent()
        {
            SentAtUtc = DateTime.UtcNow;
            Status = AlertIncidentStatus.Sent;
        }

        public void Acknowledge()
        {
            AcknowledgedAtUtc = DateTime.UtcNow;
            Status = AlertIncidentStatus.Acknowledged;
        }

        public void Resolve()
        {
            ResolvedAtUtc = DateTime.UtcNow;
            Status = AlertIncidentStatus.Resolved;
        }

        public void MarkFailed(string reason)
        {
            FailureReason = reason;
            Status = AlertIncidentStatus.Failed;
        }
    }
}
