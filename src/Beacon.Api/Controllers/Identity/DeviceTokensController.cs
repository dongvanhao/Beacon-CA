using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands.RegisterDeviceToken;
using Beacon.Application.Features.Identity.Commands.RevokeDeviceToken;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Identity;

[Route("api/v1/device-tokens")]
[Authorize]
public class DeviceTokensController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Đăng ký hoặc cập nhật FCM/APNs push token cho thiết bị hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Upsert logic:
    /// - Token chưa tồn tại → tạo mới.
    /// - Token đã tồn tại, cùng user → cập nhật LastUsedAtUtc.
    /// - Token đã tồn tại, khác user → chuyển owner sang user hiện tại.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Token rỗng hoặc Platform không hợp lệ.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var command = new RegisterDeviceTokenCommand(
            currentUser.UserId,
            request.Token,
            request.Platform,
            request.DeviceId,
            request.DeviceName,
            request.AppVersion);
        return HandleResult(await mediator.Send(command, ct));
    }

    #region
    /// <summary>Thu hồi FCM/APNs push token (idempotent).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về success kể cả khi token không tồn tại.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Token rỗng.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete]
    public async Task<IActionResult> Revoke([FromBody] RevokeDeviceTokenRequest request, CancellationToken ct)
    {
        var command = new RevokeDeviceTokenCommand(currentUser.UserId, request.Token);
        return HandleResult(await mediator.Send(command, ct));
    }
}
