using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/devices")]
[Authorize]
public class DevicesController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Đăng ký FCM/APNs token để nhận push notification.
    /// Gọi sau khi login, khi Firebase SDK trả về token mới.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new RegisterDeviceCommand(request), ct));
}
