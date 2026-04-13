using Beacon.Shared.Common.Responses;
using Beacon.Shared.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseController : ControllerBase //BaseController giúp tránh lặp logic convert Result<T> -> ApiResponse<T> ở mọi controller.
    {
        protected IActionResult HandleResult<T>(Result<T> result, string successMessage = "Success")
        {
            if (result.IsSuccess)
                return Ok(ApiResponse<T>.SuccessResponse(result.Value, successMessage));

            return MappErrorToResponse(ApiResponse<T>.FailureResponse(result.Error.Message, result.Error.Code), result.Error.Type);
        }

        protected IActionResult HandleResult(Result result, string successMessage = "Success")
        {
            if (result.IsSuccess)
            {
                return Ok(ApiResponse<object>.SuccessResponse(null, successMessage));
            }

            return MappErrorToResponse(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code), result.Error.Type);
        }

        protected IActionResult CreatedResult<T>(string location, Result<T> result, string successMessage = "Created successfully")
        {
            if (result.IsSuccess)
            {
                return Created(location, ApiResponse<T>.SuccessResponse(result.Value, successMessage));
            }

            return MappErrorToResponse(ApiResponse<T>.FailureResponse(result.Error.Message, result.Error.Code), result.Error.Type);
        }

        private IActionResult MappErrorToResponse<T>(ApiResponse<T> response, ErrorType errorType)
        {
            return errorType switch
            {
                ErrorType.Validation => BadRequest(response),
                ErrorType.NotFound => NotFound(response),
                ErrorType.Conflict => Conflict(response),
                ErrorType.Unauthorized => Unauthorized(response),
                ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
                ErrorType.Failure => BadRequest(response),
                _ => BadRequest(response),
            };
        }
    }
}
