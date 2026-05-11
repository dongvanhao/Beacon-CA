using Beacon.Domain.Common;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Storage;
using Beacon.Shared.Helpers;

namespace Beacon.Domain.Entities.Identity
{
    public class User : AuditableEntity
    {
        // Identity
        public string Username { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;

        // Name structure
        public string FamilyName { get; private set; } = default!;
        public string GivenName { get; private set; } = default!;

        /// <summary>Chuỗi đã bỏ dấu dùng cho full-text search: "nguyen hao" từ "Nguyễn Hảo".</summary>
        public string SearchIndex { get; private set; } = string.Empty;

        // Contact
        public string? PhoneNumber { get; private set; }

        // System
        public string TimeZone { get; private set; } = "Asia/Ha_Noi";
        public bool IsActive { get; private set; } = true;
        public bool IsEmailVerified { get; private set; } = false;

        public Guid? AvatarMediaObjectId { get; private set; }
        public MediaObject? AvatarMediaObject { get; private set; }

        public DateTime? LastLoginAtUtc { get; private set; }
        public DateTime? LastActiveAtUtc { get; private set; }

        // Relations
        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        public ICollection<UserDevice> Devices { get; private set; } = new List<UserDevice>();
        public Setting.SafetySetting? SafetySetting { get; private set; }
        public ICollection<DailySafetyRecord> DailySafetyRecords { get; private set; } = new List<DailySafetyRecord>();
        public ICollection<Checkin> Checkins { get; private set; } = new List<Checkin>();

        protected User() { }

        public static User Create(
            string username,
            string email,
            string passwordHash,
            string familyName,
            string givenName,
            string? phoneNumber = null)
        {
            var user = new User
            {
                Username = username.ToLowerInvariant(),
                Email = email.ToLowerInvariant(),
                PasswordHash = passwordHash,
                FamilyName = familyName,
                GivenName = givenName,
                PhoneNumber = phoneNumber?.Trim()
            };
            user.UpdateSearchIndex();
            return user;
        }

        public void UpdateProfile(string familyName, string givenName, string? phoneNumber, string email)
        {
            if (string.IsNullOrWhiteSpace(familyName)) throw new ArgumentException("FamilyName không được rỗng.");
            if (string.IsNullOrWhiteSpace(givenName))  throw new ArgumentException("GivenName không được rỗng.");
            if (string.IsNullOrWhiteSpace(email))      throw new ArgumentException("Email không được rỗng.");
            FamilyName  = familyName;
            GivenName   = givenName;
            PhoneNumber = phoneNumber?.Trim();
            Email       = email.Trim().ToLowerInvariant();
            UpdateSearchIndex();
        }

        /// <summary>Cập nhật SearchIndex từ FamilyName và GivenName hiện tại.</summary>
        public void UpdateSearchIndex()
            => SearchIndex = StringNormalizer.RemoveDiacritics($"{FamilyName} {GivenName}");

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

        public void RecordActivity() => LastActiveAtUtc = DateTime.UtcNow;

        public void UpdatePassword(string newPasswordHash) => PasswordHash = newPasswordHash;

        public void VerifyEmail() => IsEmailVerified = true;

        public void Deactivate() => IsActive = false;

        public void Activate() => IsActive = true;

        public void UpdateAvatar(Guid mediaObjectId) => AvatarMediaObjectId = mediaObjectId;
    }
}
