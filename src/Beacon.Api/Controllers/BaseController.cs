using Beacon.Api.Common;
using Beacon.Shared.Common.Responses;
using Beacon.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseController : ControllerBase //BaseController giúp tránh lặp logic convert Result<T> -> ApiResponse<T> ở mọi controller.
    {
        protected IActionResult HandleResult(Result result, string successMessage = "Success")
        {
            if (result.IsSuccess)
            {
                return Ok(ApiResponse<object>.SuccessResponse(null, successMessage));
            }

            var statusCode = ErrorHttpStatusMapper.Map(result.Error.Code);
            return StatusCode(statusCode,
                ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        protected IActionResult HandleResult<T>(Result<T> result, string successMessage = "Success")
        {
            if (result.IsSuccess)
            {
                return Ok(ApiResponse<T>.SuccessResponse(result.Value, successMessage));
            }

            var statusCode = ErrorHttpStatusMapper.Map(result.Error.Code);
            return StatusCode(statusCode,
                ApiResponse<T>.FailureResponse(result.Error.Message, result.Error.Code));
        }

        protected IActionResult CreatedResult<T>(Result<T> result, string actionName, object routeValues, string successMessage = "Created successfully")
        {
            if (result.IsSuccess)
            {
                return CreatedAtAction(actionName, routeValues,
                    ApiResponse<T>.SuccessResponse(result.Value, successMessage));
            }

            var statusCode = ErrorHttpStatusMapper.Map(result.Error.Code);
            return StatusCode(statusCode,
                ApiResponse<T>.FailureResponse(result.Error.Message, result.Error.Code));
        }
    }
}
