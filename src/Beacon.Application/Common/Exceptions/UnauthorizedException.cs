using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Application.Common.Exceptions
{
    public class UnauthorizedException : Exception // Chưa đăng nhập hoặc token hết hạn
    {
        public UnauthorizedException(string message) : base(message)
        {
        }
    }
}
