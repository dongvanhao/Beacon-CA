using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Common
{
    public abstract class AuditableEntity : BaseEntity //Đây là class cha dùng để làm nền, không được tạo object trực tiếp.
    {
        public DateTime CreatedAtUtc { get; protected set; }
        public DateTime? UpdatedAtUtc { get; protected set; }
    }
}
