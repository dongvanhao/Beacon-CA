using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Results
{
    #region Error là đối tượng biểu diễn lỗi nghiệp vụ / lỗi hệ thống theo cách có cấu trúc.
    /* 
    Ví dụ:
    USER_NOT_FOUND
    EMAIL_ALREADY_EXISTS
    CHECKIN_ALREADY_EXISTS
    */
    #endregion
    public sealed class Error
    {
        public static readonly Error None = new(string.Empty, string.Empty);

        public string Code { get; }
        public string Message { get; }

        public Error(string code, string message)
        {
            Code = code;
            Message = message;
        }

    }
}
