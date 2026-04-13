using Beacon.Domain.Common;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Identity
{
    public class User : AuditableEntity
    {
        public string Email { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public string FullName { get; private set; } = default!;

        public string? PhoneNumber { get; private set; }

        public string TimeZone { get; private set; } = "Asia/Ha_Noi";

        public bool IsActive { get; private set; } = true;
        public bool IsEmailVerified { get; private set; } = false;

        public Guid? AvatarMediaObjectId { get; private set; }
        public MediaObject? AvatarMediaObject { get; private set; }
        public DateTime? LastLoginAtUtc { get; private set; }

        // === CÁC BẢNG TÍNH NĂNG CHƯA LÀM TỚI (Tạm ẩn) ===
        // public SafetySetting? SafetySetting { get; private set; }
        // public NotificationPreference? NotificationPreference { get; private set; }
        // public AppPreference? AppPreference { get; private set; }

        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        public ICollection<UserDevice> Devices { get; private set; } = new List<UserDevice>();

        // public ICollection<EmergencyContact> EmergencyContacts { get; private set; } = new List<EmergencyContact>();
        // public ICollection<DailySafetyRecord> DailySafetyRecords { get; private set; } = new List<DailySafetyRecord>();
        // public ICollection<Checkin> Checkins { get; private set; } = new List<Checkin>();
        // public ICollection<AlertIncident> AlertIncidents { get; private set; } = new List<AlertIncident>();
        // public ICollection<NotificationDelivery> NotificationDeliveries { get; private set; } = new List<NotificationDelivery>();


        protected User() { }

        public static User Create(string email, string passwordHash, string fullName, string? phoneNumber = null)
            => new() { Email = email.ToLowerInvariant(), PasswordHash = passwordHash, FullName = fullName, PhoneNumber = phoneNumber };

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;
        public void UpdatePassword(string newPasswordHash) => PasswordHash = newPasswordHash;
        public void VerifyEmail() => IsEmailVerified = true;
        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
        public void UpdateAvatar(Guid mediaObjectId) => AvatarMediaObjectId = mediaObjectId;
    }
}
