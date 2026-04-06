using Beacon.Shared.Common;
using Microsoft.AspNetCore.Http;

namespace Beacon.Api.Common
{
    public static class ErrorHttpStatusMapper
    {
        public static int Map(string errorCode)
        {
            return errorCode switch
            {
                ErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
                ErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
                ErrorCodes.NotFound => StatusCodes.Status404NotFound,
                ErrorCodes.Conflict => StatusCodes.Status409Conflict,
                ErrorCodes.DatabaseUnavailable => StatusCodes.Status503ServiceUnavailable,
                ErrorCodes.DatabaseTimeout => StatusCodes.Status504GatewayTimeout,
                ErrorCodes.DatabaseException => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status400BadRequest
            };
        }
    }
}
