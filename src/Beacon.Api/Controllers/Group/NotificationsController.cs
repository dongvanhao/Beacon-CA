using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Commands.MarkAllNotificationsRead;
using Beacon.Application.Features.Group.Commands.MarkNotificationRead;
using Beacon.Application.Features.Group.Queries.ListNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Group;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Danh sách thông báo của tôi (cursor-based).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Query params:**
    /// - <c>cursor</c> (ISO-8601 UTC, tuỳ chọn): Load thông báo cũ hơn mốc này.
    /// - <c>limit</c> (int, tuỳ chọn, mặc định 20, tối đa 50): Số bản ghi mỗi trang.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: <c>limit</c> ngoài khoảng 1–50 (HTTP 400).
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "items": [ { "id": "guid", "type": 1, "title": "...", "body": "...", "data": null, "isRead": false, "readAtUtc": null, "createdAtUtc": "..." } ],
    ///   "nextCursor": "2026-05-01T08:00:00Z",
    ///   "hasNextPage": true,
    ///   "unreadCount": 5
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(
            new ListNotificationsQuery(currentUser.UserId, cursor, limit), ct));

    #region
    /// <summary>Đánh dấu một thông báo đã đọc.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công. <c>data.unreadCount</c> là số thông báo chưa đọc còn lại.
    /// - <c>NOTIFICATION_NOT_FOUND</c>: Thông báo không tồn tại (HTTP 404).
    /// - <c>NOTIFICATION_FORBIDDEN</c>: Thông báo không thuộc về user hiện tại (HTTP 403).
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new MarkNotificationReadCommand(id, currentUser.UserId), ct));

    #region
    /// <summary>Đánh dấu tất cả thông báo đã đọc.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Idempotent — gọi nhiều lần không gây lỗi.
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công. <c>data.unreadCount</c> luôn là <c>0</c>.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => HandleResult(await mediator.Send(
            new MarkAllNotificationsReadCommand(currentUser.UserId), ct));
}
