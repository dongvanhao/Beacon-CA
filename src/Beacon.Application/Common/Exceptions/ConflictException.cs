using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Application.Common.Exceptions
{
    public class ConflictException : Exception // Dữ liệu đã tồn tại, xung đột với dữ liệu hiện có
    {
        public ConflictException(string message) : base(message)
        {
        }
    }
}
