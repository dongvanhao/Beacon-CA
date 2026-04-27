using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Checkins.Commands.CreateCheckin;
using Beacon.Application.Features.Checkins.Dtos;
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
}
