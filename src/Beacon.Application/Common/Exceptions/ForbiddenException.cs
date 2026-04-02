using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Application.Common.Exceptions
{
    public class ForbiddenException : Exception //có đăng nhập nhưng không có quyền truy cập tài nguyên
    {
        public ForbiddenException(string message) : base(message)
        {
        }
    }
}
