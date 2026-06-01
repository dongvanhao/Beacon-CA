using Beacon.Api.Authorization;
using Beacon.Application.Features.Management.Statistics;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/statistics")]
public class StatisticsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Thong ke tai khoan user.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Thong ke thanh cong.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "totalUsers": 100,
    ///   "activeUsers": 80,
    ///   "inactiveUsers": 20,
    ///   "onlineUsers": 5
    /// }
    /// </code>
    ///
    /// Ghi chu: <c>onlineUsers</c> dem user dang co connection SignalR trong <c>IUserOnlineTracker</c>.
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("users")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetUserStatisticsQuery(), ct));

    /// <summary>
    /// Thong ke tai khoan admin.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Thong ke thanh cong.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "totalAdmins": 10,
    ///   "activeAdmins": 9,
    ///   "inactiveAdmins": 1
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("admins")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetAdmins(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetAdminStatisticsQuery(), ct));

    /// <summary>
    /// Thong ke post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Thong ke thanh cong.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "totalPosts": 250,
    ///   "deletedPosts": 30,
    ///   "notDeletedPosts": 220,
    ///   "activePosts": 200,
    ///   "hiddenPosts": 20
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("posts")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetPosts(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetPostStatisticsQuery(), ct));

    /// <summary>
    /// Thong ke so post duoc tao trong 10 ngay gan nhat.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// API tra ve 10 moc ngay lien tiep tinh theo UTC, bao gom ca hom nay; ngay khong co post se co <c>totalPosts = 0</c>.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "items": [
    ///     { "date": "2026-05-22", "totalPosts": 3 },
    ///     { "date": "2026-05-23", "totalPosts": 0 }
    ///   ]
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("posts/recent-10-days")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetRecentPosts(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetRecentPostStatisticsQuery(), ct));

    /// <summary>
    /// Thong ke bao cao post theo status.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Thong ke thanh cong.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "totalReports": 50,
    ///   "pendingReports": 10,
    ///   "reviewedReports": 15,
    ///   "resolvedReports": 20,
    ///   "rejectedReports": 5
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("reports")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetReports(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetReportStatisticsQuery(), ct));

    /// <summary>
    /// Thong ke admin hoat dong theo log trong khoang thoi gian truyen vao.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>statistics:read</c>.
    ///
    /// Query:
    /// - <c>fromUtc</c> (datetime UTC, tuy chon): Moc bat dau. Neu khong truyen, mac dinh la <c>toUtc - 10 ngay</c>.
    /// - <c>toUtc</c> (datetime UTC, tuy chon): Moc ket thuc. Neu khong truyen, mac dinh la thoi diem hien tai UTC.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "fromUtc": "2026-05-01T00:00:00Z",
    ///   "toUtc": "2026-05-31T23:59:59Z",
    ///   "activeAdminCount": 2,
    ///   "totalActionCount": 15,
    ///   "items": [
    ///     {
    ///       "adminId": "guid",
    ///       "adminUsername": "admin",
    ///       "actionCount": 10,
    ///       "lastActivityAtUtc": "2026-05-31T10:00:00Z"
    ///     }
    ///   ]
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("admins/activity")]
    [AdminOnly]
    [HasPermission(PermissionCodes.Statistics.Read)]
    public async Task<IActionResult> GetAdminActivity(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new GetAdminActivityStatisticsQuery(fromUtc, toUtc), ct));
}
