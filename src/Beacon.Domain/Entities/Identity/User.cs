using Beacon.Domain.Common;
using Beacon.Domain.Entities.Storage;

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

        // Contact
        public string? PhoneNumber { get; private set; }

        // System
        public string TimeZone { get; private set; } = "Asia/Ha_Noi";
        public bool IsActive { get; private set; } = true;
        public bool IsEmailVerified { get; private set; } = false;

        public Guid? AvatarMediaObjectId { get; private set; }
        public MediaObject? AvatarMediaObject { get; private set; }

        public DateTime? LastLoginAtUtc { get; private set; }

        // Relations
        public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();
        public ICollection<UserDevice> Devices { get; private set; } = new List<UserDevice>();

        protected User() { }

        public static User Create(
            string username,
            string email,
            string passwordHash,
            string familyName,
            string givenName,
            string? phoneNumber = null)
        {
            return new User
            {
                Username = username.ToLowerInvariant(),
                Email = email.ToLowerInvariant(),
                PasswordHash = passwordHash,
                FamilyName = familyName,
                GivenName = givenName,
                PhoneNumber = phoneNumber?.Trim()
            };
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
        }

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;

        public void UpdatePassword(string newPasswordHash) => PasswordHash = newPasswordHash;

        public void VerifyEmail() => IsEmailVerified = true;

        public void Deactivate() => IsActive = false;

        public void Activate() => IsActive = true;

        public void UpdateAvatar(Guid mediaObjectId) => AvatarMediaObjectId = mediaObjectId;
    }
}
