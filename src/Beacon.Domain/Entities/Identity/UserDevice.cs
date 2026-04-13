using Beacon.Domain.Common;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Enums.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Identity
{
    public class UserDevice : SoftDeletableEntity
    {
        public Guid UserId { get; private set; }

        public DevicePlatform Platform { get; private set; }
        public string DeviceName { get; private set; } = default!;
        public string DeviceToken { get; private set; } = default!; //Token dùng cho push notification, có thể là FCM token hoặc APNs token tùy thuộc vào nền tảng của thiết bị.

        public bool IsActive { get; private set; } = true; //Trạng thái hoạt động của thiết bị, có thể được sử dụng để xác định xem thiết bị có còn được sử dụng để nhận thông báo hay không.
        public DateTime? LastSeenAtUtc { get; private set; } //Thời gian thiết bị được sử dụng lần cuối, có thể được cập nhật mỗi khi người dùng tương tác với hệ thống hoặc khi thiết bị gửi thông tin về trạng thái của nó.

        public User User { get; private set; } = default!;
        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        // === CÁC BẢNG TÍNH NĂNG CHƯA LÀM TỚI (Tạm ẩn) ===
        // public ICollection<NotificationDelivery> NotificationDeliveries { get; private set; } = new List<NotificationDelivery>();

        protected UserDevice() { }

        public static UserDevice Create(Guid userId, DevicePlatform platform, string deviceName, string deviceToken)
            => new() { UserId = userId, Platform = platform, DeviceName = deviceName, DeviceToken = deviceToken };

        public void UpdateToken(string newToken) => DeviceToken = newToken;
        public void RecordActivity() => LastSeenAtUtc = DateTime.UtcNow;
        public void Deactivate() => IsActive = false;
    }
}
