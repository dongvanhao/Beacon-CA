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
    #region
    /// <summary>
    /// Đăng ký hoặc cập nhật FCM/APNs token để nhận push notification trên thiết bị hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;accessToken&gt;</c> trong header.
    /// Thiết bị được xác định qua <c>DeviceId</c> nhúng trong Access Token — client không cần truyền thêm.
    /// Gọi endpoint này sau khi đăng nhập và mỗi khi Firebase SDK / APNs cấp token mới.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng ký token thành công (success = true), data = null.
    /// - <c>VALIDATION_ERROR</c>: Trường <c>deviceToken</c> để trống hoặc vượt quá 500 ký tự.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>TOKEN_INVALID</c>: DeviceId trong token không khớp với bất kỳ thiết bị nào trong hệ thống.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new RegisterDeviceCommand(request), ct));
}
