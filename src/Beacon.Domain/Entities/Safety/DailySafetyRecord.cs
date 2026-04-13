using Beacon.Domain.Common;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Safety;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Safety
{ //Lưu trữ thông tin về tình trạng an toàn hàng ngày của người dùng, bao gồm thời gian deadline, thời gian check-in, và trạng thái hiện tại (đã check-in, trễ hạn, v.v.) để hệ thống có thể theo dõi và xử lý kịp thời.
    public class DailySafetyRecord : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public DateOnly Date { get; private set; }
        public SafetyStatus Status { get; private set; } = SafetyStatus.Pending;

        public DateTime DeadlineAtUtc { get; private set; } //Thời điểm mà người dùng cần phải thực hiện check-in hàng ngày, có thể được sử dụng để xác định xem người dùng đã check-in đúng hạn hay chưa.
        public DateTime? CheckedInAtUtc { get; private set; } //Thời điểm người dùng đã thực hiện check-in, có thể dùng để kiểm tra xem người dùng đã check-in trước deadline hay chưa.
        public DateTime? MarkedMissedAtUtc { get; private set; } //Thời điểm người dùng bị đánh dấu là đã trễ hạn nếu họ không check-in trước deadline, có thể dùng để kiểm tra xem có cần gửi nhắc nhở hay không.
        public DateTime? ReminderSentAtUtc { get; private set; } //Thời điểm hệ thống đã gửi nhắc nhở cho người dùng về việc check-in, có thể dùng để kiểm tra xem có cần gửi thêm nhắc nhở hay không.
        public DateTime? ResolvedAtUtc { get; private set; } //Thời điểm người dùng đã được đánh dấu là an toàn sau khi đã trễ hạn hoặc đã được gửi nhắc nhở, có thể dùng để kiểm tra xem có cần tiếp tục theo dõi hay không.
        public DateTime? LastEvaluatedAtUtc { get; private set; } //Thời điểm cuối cùng hệ thống đánh giá lại trạng thái của record, có thể dùng để kiểm tra xem có cần gửi nhắc nhở hay không.

        public User User { get; private set; } = default!;
        public Checkin? Checkin { get; private set; }
        public AlertIncident? AlertIncident { get; private set; }
        protected DailySafetyRecord() { }

        public static DailySafetyRecord Create(Guid userId, DateOnly date, DateTime deadlineAtUtc)
            => new() { UserId = userId, Date = date, DeadlineAtUtc = deadlineAtUtc };

        public void MarkCheckedIn(DateTime checkedInAtUtc)
        {
            CheckedInAtUtc = checkedInAtUtc;
            Status = SafetyStatus.CheckedIn;
        }

        public void MarkMissed()
        {
            MarkedMissedAtUtc = DateTime.UtcNow;
            Status = SafetyStatus.Missed;
        }

        public void MarkAlerted() => Status = SafetyStatus.Alerted;

        public void MarkResolved()
        {
            ResolvedAtUtc = DateTime.UtcNow;
            Status = SafetyStatus.Resolved;
        }

        public void RecordReminderSent() => ReminderSentAtUtc = DateTime.UtcNow;
        public void RecordEvaluation() => LastEvaluatedAtUtc = DateTime.UtcNow;
    }
}
