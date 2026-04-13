using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Common.Responses
{
    #region ApiResponse<T> là đối tượng biểu diễn phản hồi của API, bao gồm thông tin về thành công hay thất bại, thông điệp và dữ liệu trả về nếu có.
    #endregion
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Code { get; set; }
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }
        public static ApiResponse<T> SuccessResponse(T? data, string message = "Success", string? code = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Code = code,
                Data = data,
                Errors = null
            };
        }

        public static ApiResponse<T> FailureResponse(string message, string? code = null, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Code = code,
                Data = default,
                Errors = errors
            };
        }
    }
}
