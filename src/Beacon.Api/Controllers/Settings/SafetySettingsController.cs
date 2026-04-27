using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Settings.Commands.UpdateSafetySetting;
using Beacon.Application.Features.Settings.Dtos;
using Beacon.Application.Features.Settings.Queries.GetSafetySetting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Settings;

[Route("api/v1/safety/settings")]
[Authorize]
public class SafetySettingsController(IMediator mediator, ICurrentUserService currentUser)
    : BaseController
{
    #region
    /// <summary>Lấy cài đặt an toàn của người dùng hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về default values nếu người dùng chưa cấu hình.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "dailyDeadlineLocalTime": "HH:mm",
    ///   "gracePeriodMinutes": 15,
    ///   "reminderBeforeMinutes": 30,
    ///   "autoAlertDelayMinutes": 15,
    ///   "isMonitoringEnabled": true,
    ///   "isAutoAlertEnabled": true
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetSafetySettingQuery(currentUser.UserId), ct));

    #region
    /// <summary>Cập nhật cài đặt an toàn của người dùng hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Tạo mới nếu chưa có record, cập nhật nếu đã tồn tại (upsert).
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu không hợp lệ (format HH:mm, giá trị ngoài range).
    ///
    /// Cấu trúc <c>data</c> khi thành công: xem GET /api/v1/safety/settings.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch]
    public async Task<IActionResult> Update(
        [FromBody] UpdateSafetySettingRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(
            new UpdateSafetySettingCommand(currentUser.UserId, request), ct));
}
