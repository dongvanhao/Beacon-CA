using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Domain.Common
{
    public abstract class BaseEntity //Đây là class cha dùng để làm nền, không được tạo object trực tiếp.
    {
        public Guid Id { get; protected set; } // Protected set để chỉ cho phép set Id trong class con hoặc trong constructor, tránh việc set Id từ bên ngoài.
    }
}
