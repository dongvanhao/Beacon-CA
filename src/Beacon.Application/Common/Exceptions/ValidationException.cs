using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Application.Common.Exceptions
{
    public class ValidationException : Exception // dữ liệu đầu vào không hợp lệ
    {
        public List<string> Errors { get; }
        public ValidationException(List<string> errors) : base ("One or more validation failures have occurred.")
        {
            Errors = errors;
        }
    }
}
