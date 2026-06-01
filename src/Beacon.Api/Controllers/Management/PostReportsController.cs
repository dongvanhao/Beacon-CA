using Beacon.Api.Authorization;
using Beacon.Application.Features.Management.PostReports;
using Beacon.Application.Features.Management.PostReports.Dtos;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/post-reports")]
public class PostReportsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Lay danh sach bao cao post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>post-reports:read</c>.
    ///
    /// Query params:
    /// - <c>page</c> (int, mac dinh 1): Trang can lay.
    /// - <c>pageSize</c> (int, mac dinh 20, toi da 100): So ban ghi moi trang.
    /// - <c>search</c> (string, tuy chon): Tim theo reason hoac description.
    /// - <c>status</c> (string, tuy chon): <c>pending</c>, <c>reviewed</c>, <c>resolved</c>, <c>rejected</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Lay danh sach thanh cong.
    /// - <c>INVALID_POST_REPORT_STATUS</c>: Status khong hop le.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "items": [
    ///     {
    ///       "id": "guid",
    ///       "postId": "guid",
    ///       "post": {
    ///         "id": "guid",
    ///         "ownerUserId": "guid",
    ///         "mediaId": "guid",
    ///         "media": {
    ///           "id": "guid",
    ///           "url": "presigned-url",
    ///           "type": "image | video",
    ///           "thumbnailUrl": "string | null",
    ///           "durationSeconds": 10,
    ///           "width": 1080,
    ///           "height": 1920
    ///         },
    ///         "caption": "string | null",
    ///         "visibility": "friends | private",
    ///         "status": "active | hidden",
    ///         "dailySafetyRecordId": "guid | null",
    ///         "latitude": 10.123456,
    ///         "longitude": 106.123456,
    ///         "deletedAtUtc": "datetime | null",
    ///         "deletedReason": "string | null",
    ///         "createdAtUtc": "datetime",
    ///         "updatedAtUtc": "datetime | null"
    ///       },
    ///       "reporterUserId": "guid",
    ///       "reason": "string",
    ///       "description": "string | null",
    ///       "status": "pending | reviewed | resolved | rejected",
    ///       "reviewedByAdminId": "guid | null",
    ///       "reviewedAtUtc": "datetime | null",
    ///       "resolutionNote": "string | null",
    ///       "createdAtUtc": "datetime",
    ///       "updatedAtUtc": "datetime | null"
    ///     }
    ///   ],
    ///   "totalCount": 1,
    ///   "page": 1,
    ///   "pageSize": 20
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostReportManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListPostReportsQuery(page, pageSize, search, status), ct));

    /// <summary>
    /// Lay chi tiet mot bao cao post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>post-reports:read</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Lay thanh cong.
    /// - <c>POST_REPORT_NOT_FOUND</c>: Khong tim thay bao cao.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "id": "guid",
    ///   "postId": "guid",
    ///   "post": {
    ///     "id": "guid",
    ///     "ownerUserId": "guid",
    ///     "mediaId": "guid",
    ///     "media": {
    ///       "id": "guid",
    ///       "url": "presigned-url",
    ///       "type": "image | video",
    ///       "thumbnailUrl": "string | null",
    ///       "durationSeconds": 10,
    ///       "width": 1080,
    ///       "height": 1920
    ///     },
    ///     "caption": "string | null",
    ///     "visibility": "friends | private",
    ///     "status": "active | hidden",
    ///     "dailySafetyRecordId": "guid | null",
    ///     "latitude": 10.123456,
    ///     "longitude": 106.123456,
    ///     "deletedAtUtc": "datetime | null",
    ///     "deletedReason": "string | null",
    ///     "createdAtUtc": "datetime",
    ///     "updatedAtUtc": "datetime | null"
    ///   },
    ///   "reporterUserId": "guid",
    ///   "reason": "string",
    ///   "description": "string | null",
    ///   "status": "pending | reviewed | resolved | rejected",
    ///   "reviewedByAdminId": "guid | null",
    ///   "reviewedAtUtc": "datetime | null",
    ///   "resolutionNote": "string | null",
    ///   "createdAtUtc": "datetime",
    ///   "updatedAtUtc": "datetime | null"
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostReportManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetPostReportByIdQuery(id), ct));

    /// <summary>
    /// Tao bao cao post moi.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>post-reports:create</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Tao bao cao thanh cong.
    /// - <c>VALIDATION_ERROR</c>: Reason rong hoac du lieu dau vao khong hop le.
    /// - <c>POST_NOT_FOUND</c>: Khong tim thay post.
    /// - <c>USER_NOT_FOUND</c>: Khong tim thay reporter user.
    /// - <c>INVALID_POST_REPORT_STATUS</c>: Status khong hop le.
    ///
    /// Cau truc <c>data</c> khi thanh cong: giong voi <c>GET /api/v1/admin/post-reports/{id}</c>.
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON:
    /// <code>
    /// {
    ///   "postId": "guid",
    ///   "reporterUserId": "guid",
    ///   "reason": "string",
    ///   "description": "string | null",
    ///   "status": "pending | reviewed | resolved | rejected"
    /// }
    /// </code>
    /// </param>
    [HttpPost]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostReportManagement.Create)]
    public async Task<IActionResult> Create([FromBody] CreatePostReportRequest request, CancellationToken ct)
        => CreatedResult("api/v1/admin/post-reports",
            await mediator.Send(new CreatePostReportCommand(request), ct));

    /// <summary>
    /// Cap nhat bao cao post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>post-reports:update</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Cap nhat thanh cong.
    /// - <c>POST_REPORT_NOT_FOUND</c>: Khong tim thay bao cao.
    /// - <c>INVALID_POST_REPORT_STATUS</c>: Status khong hop le.
    ///
    /// Cau truc <c>data</c> khi thanh cong: giong voi <c>GET /api/v1/admin/post-reports/{id}</c>.
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON (tat ca fields tuy chon):
    /// <code>
    /// {
    ///   "reason": "string | null",
    ///   "description": "string | null",
    ///   "status": "pending | reviewed | resolved | rejected",
    ///   "resolutionNote": "string | null"
    /// }
    /// </code>
    /// </param>
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostReportManagement.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePostReportRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdatePostReportCommand(id, request), ct));

    /// <summary>
    /// Xoa bao cao post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>post-reports:delete</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Xoa thanh cong.
    /// - <c>POST_REPORT_NOT_FOUND</c>: Khong tim thay bao cao.
    ///
    /// Cau truc <c>data</c> khi thanh cong: <c>null</c>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostReportManagement.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeletePostReportCommand(id), ct));
}
