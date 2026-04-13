using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/auth")]
public class AuthController(IMediator mediator) : BaseController
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        return CreatedResult("api/v1/auth/me", await mediator.Send(new RegisterCommand(request, userAgent), ct));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        return HandleResult(await mediator.Send(new LoginCommand(request, userAgent), ct));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutCommand(request.RefreshToken), ct));
}
