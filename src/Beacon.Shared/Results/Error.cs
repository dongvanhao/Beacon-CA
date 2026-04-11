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

    public enum ErrorType { Validation = 0, NotFound = 1, Conflict = 2, Unauthorized = 3, Forbidden = 4, Failure = 5}
    public sealed class Error
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

        public string Code { get; }
        public string Message { get; }
        public ErrorType Type { get; }

        public Error(string code, string message, ErrorType type)
        {
            Code = code;
            Message = message;
            Type = type;
        }

        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
        public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
        public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
        public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    }
}
