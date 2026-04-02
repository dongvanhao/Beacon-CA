using Beacon.Application.Common.Exceptions;
using Beacon.Shared.Common.Responses;
using System.Net;
using System.Text.Json;
using ValidationException = Beacon.Application.Common.Exceptions.ValidationException;

namespace Beacon.Api.Middleware
{
    //Đây là middleware bắt toàn bộ exception chưa được xử lý và trả JSON lỗi chuẩn.
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        public readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware
            (RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }
        #region Hàm InvokeAsync
        /*
         * Cho Request chạy tiếp bằng _next(context) trong try-catch để bắt mọi exception chưa được xử lý.
         * Nếu toàn bộ đoạn phía sau không lỗi -> Middleware không làm gì thêm
         * Nếu có lỗi ở bất kỳ đâu bên dưới -> nhảy vào catch
         */
        #endregion
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }
        #region Hàm HandleExceptionAsync
        /* Dựa vào kiểu exception để xác định status code và message trả về.
        * Tạo ApiResponse chuẩn cho từng loại lỗi.
        * Trả về JSON response cho client.
        * Nhiệm vụ:
            - set content type
            - set status code
            - tạo object response
            - serialize sang JSON
            - ghi ra response body
        */
        #endregion
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception) // Đây là hàm chuyên xử lý lỗi sau khi đã bắt được 
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                ValidationException => StatusCodes.Status400BadRequest,
                NotFoundException => StatusCodes.Status404NotFound,
                ConflictException => StatusCodes.Status409Conflict,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ForbiddenException => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError
            };

            context.Response.StatusCode = statusCode;

            ApiResponse<object> response = exception switch
            {
                ValidationException vex => ApiResponse<object>.FailureResponse(
                    message: vex.Message,
                    code: "VALIDATION_ERROR",
                    errors: vex.Errors),

                NotFoundException => ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: "NOT_FOUND"),

                ConflictException => ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: "CONFLICT"),

                UnauthorizedException => ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: "UNAUTHORIZED"),

                ForbiddenException => ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: "FORBIDDEN"),

                _ => ApiResponse<object>.FailureResponse(
                    message: "Internal server error.",
                    code: "INTERNAL_SERVER_ERROR")
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
