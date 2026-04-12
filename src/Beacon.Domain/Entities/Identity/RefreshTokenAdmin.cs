using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Identity
{
    public class RefreshTokenAdmin : AuditableEntity
    {
        public Guid AdminId { get; private set; }
        public string Token { get; private set; } = default!;

        public DateTime ExpiresAtUtc { get; private set; }
        public DateTime? RevokedAtUtc { get; private set; }

        public string? CreatedByIp { get; private set; }
        public string? RevokedByIp { get; private set; }
        public string? ReplacedByToken { get; private set; }

        public Admin Admin { get; private set; } = default!;

        protected RefreshTokenAdmin() { }

        public static RefreshTokenAdmin Create(Guid adminId, string token, DateTime expiresAtUtc,
            string? createdByIp = null)
            => new()
            {
                AdminId = adminId,
                Token = token,
                ExpiresAtUtc = expiresAtUtc,
                CreatedByIp = createdByIp
            };

        public void Revoke(string? revokedByIp = null, string? replacedByToken = null)
        {
            RevokedAtUtc = DateTime.UtcNow;
            RevokedByIp = revokedByIp;
            ReplacedByToken = replacedByToken;
        }

        public bool IsExpired => ExpiresAtUtc < DateTime.UtcNow;
        public bool IsRevoked => RevokedAtUtc is not null;
        public bool IsActive => !IsExpired && !IsRevoked;
    }
}
