using Beacon.Api.Attributes;
using Beacon.Application.Features.Auth.Dtos;
using Beacon.Application.Features.Auth.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseController
    {
        private readonly LoginUseCase _loginUseCase;

        public AuthController(LoginUseCase loginUseCase)
        {
            _loginUseCase = loginUseCase;
        }

        [PublicApi]
        [HttpPost("login/admin")]
        public async Task<IActionResult> LoginAdminAsync([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _loginUseCase.ExecuteAdminAsync(request, cancellationToken);
            return HandleResult(result, "Admin login successful.");
        }

        [PublicApi]
        [HttpPost("login/user")]
        public async Task<IActionResult> LoginUserAsync([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _loginUseCase.ExecuteUserAsync(request, cancellationToken);
            return HandleResult(result, "User login successful.");
        }
    }
}
