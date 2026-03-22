using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Beacon.Shared.Common.Responses
{
    /*
     * Dùng cho middleware khi trả lỗi. 
     * Nếu muốn trả lỗi theo chuẩn này thì có thể dùng ApiResponse<string>
     * với Success = false, Data = null, Message = lỗi chi tiết,
     * nhưng nếu muốn trả lỗi có thêm danh sách lỗi chi tiết thì dùng ErrorResponse.
     */
    public class ErrorResponse 
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string>? Errors { get; set; }
    }
}
