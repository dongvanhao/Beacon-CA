using Beacon.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Entities.Identity
{
    public class RefreshToken : AuditableEntity
    {
        public Guid UserId { get; private set; }
        public Guid? UserDeviceId { get; private set; }
        public string Token { get; private set; } = default!;

        public DateTime ExpiresAtUtc { get; private set; }
        public DateTime? RevokedAtUtc { get; private set; }

        public string? CreatedByIp { get; private set; }
        public string? RevokedByIp { get; private set; }
        public string? ReplacedByToken { get; private set; }

        public User User { get; private set; } = default!;
        public UserDevice? UserDevice { get; private set; }

        protected RefreshToken() { }
    }
}
