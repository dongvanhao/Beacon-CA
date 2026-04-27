using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Checkins.Commands.CreateCheckin;
using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Application.Features.Checkins.Queries.GetTodayCheckinStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Checkins;

[Route("api/v1/checkins")]
[Authorize]
public class CheckinsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Người dùng thực hiện check-in an toàn hàng ngày.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Mỗi người dùng chỉ được check-in 1 lần mỗi ngày.
    /// Nếu chưa có record an toàn hôm nay, hệ thống tự tạo dựa trên cài đặt <c>SafetySetting</c>.
    /// Nếu chưa có <c>SafetySetting</c>, deadline mặc định là 23:59 UTC.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu không hợp lệ (type ngoài enum, note &gt; 1000 ký tự, lat/long sai range).
    /// - <c>MEDIA_NOT_FOUND</c>: mediaId không tồn tại.
    /// - <c>ALREADY_CHECKED_IN</c>: Đã check-in hôm nay rồi.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id": "guid",
    ///   "dailySafetyRecordId": "guid",
    ///   "checkinDate": "yyyy-MM-dd",
    ///   "checkedInAtUtc": "datetime",
    ///   "type": "Manual|Recovery|Emergency",
    ///   "note": "string|null",
    ///   "latitude": "decimal|null",
    ///   "longitude": "decimal|null",
    ///   "mediaObjectId": "guid|null"
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCheckinRequest request,
        CancellationToken ct)
        => CreatedResult("api/v1/checkins",
            await mediator.Send(new CreateCheckinCommand(currentUser.UserId, request), ct));

    #region
    /// <summary>Lấy trạng thái check-in và thời gian đếm ngược đến deadline trong ngày hôm nay.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Deadline lấy từ <c>SafetySetting</c> của user. Nếu chưa cấu hình, mặc định là 23:59 UTC.
    ///
    /// Hành vi theo trạng thái monitoring:
    /// - <c>isMonitoringEnabled = false</c>: không có countdown, không có overdue — <c>remainingSeconds</c> luôn null, <c>status</c> không bao giờ là <c>Overdue</c>.
    /// - <c>isAutoAlertEnabled = false</c>: vẫn tính overdue, nhưng hệ thống không gửi cảnh báo tự động.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "hasCheckedIn": "bool",
    ///   "status": "Pending | CheckedIn | Overdue",
    ///   "deadlineAtUtc": "datetime",
    ///   "remainingSeconds": "long | null — null khi CheckedIn hoặc monitoring tắt, âm khi Overdue",
    ///   "checkedInAtUtc": "datetime | null",
    ///   "isMonitoringEnabled": "bool — false: tắt toàn bộ countdown và overdue",
    ///   "isAutoAlertEnabled": "bool — false: vẫn overdue nhưng không gửi alert tự động"
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("today-status")]
    public async Task<IActionResult> GetTodayStatus(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetTodayCheckinStatusQuery(currentUser.UserId), ct));
}
