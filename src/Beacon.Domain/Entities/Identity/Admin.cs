using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Identity
{
    public class Admin : AuditableEntity
    {
        public string Username { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public string FullName { get; private set; } = default!;
        public bool IsActive { get; private set; } = true;
        public DateTime? LastLoginAtUtc { get; private set; }

        public ICollection<AdminRole> AdminRoles { get; private set; } = new List<AdminRole>();
        public ICollection<RefreshTokenAdmin> RefreshTokens { get; private set; } = new List<RefreshTokenAdmin>();

        protected Admin() { }

        public static Admin Create(string username, string passwordHash, string fullName)
            => new()
            {
                Username = username.Trim().ToLowerInvariant(),
                PasswordHash = passwordHash,
                FullName = fullName.Trim()
            };

        public void Update(string username, string fullName)
        {
            Username = username.Trim().ToLowerInvariant();
            FullName = fullName.Trim();
        }

        public void RecordLogin() => LastLoginAtUtc = DateTime.UtcNow;
        public void Deactivate() => IsActive = false;
        public void Activate() => IsActive = true;
        public void UpdatePassword(string newPasswordHash) => PasswordHash = newPasswordHash;
    }
}
