using Beacon.Application.Common.Exceptions;
using Beacon.Shared.Common.Responses;
using System.ComponentModel.DataAnnotations;
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
        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = exception switch
            {
                ValidationException => (int)HttpStatusCode.BadRequest,
                NotFoundException => (int)HttpStatusCode.NotFound,
                ConflictException => (int)HttpStatusCode.Conflict,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                ForbiddenException => (int)HttpStatusCode.Forbidden,
                _ => (int)HttpStatusCode.InternalServerError
            };
            ApiResponse<object> response;

            if (exception is ValidationException vex)
            {
                response = ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: "VALIDATION_ERROR",
                    errors: vex.Errors
                );
            }
            else
            {
                response = ApiResponse<object>.FailureResponse(
                    message: exception.Message,
                    code: exception.GetType().Name
                );
            }

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);

        }
    }
}
