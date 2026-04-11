using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Common
{
    public class SoftDeletableEntity : AuditableEntity
    {
        public bool IsDeleted { get; private set; } 
        public DateTime? DeletedAtUtc { get; private set; }

        public void Delete()
        {
            IsDeleted = true;
            DeletedAtUtc = DateTime.UtcNow;
        }

        public void Restore()
        {
            IsDeleted = false;
            DeletedAtUtc = null;
        }
    }
}
