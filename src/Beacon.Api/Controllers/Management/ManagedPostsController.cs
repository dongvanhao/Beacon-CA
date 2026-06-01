using Beacon.Api.Authorization;
using Beacon.Application.Features.Management.Posts;
using Beacon.Application.Features.Management.Posts.Dtos;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/posts")]
public class ManagedPostsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Lay danh sach post de admin quan ly.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>posts:read</c>.
    ///
    /// Query params:
    /// - <c>page</c> (int, mac dinh 1): Trang can lay.
    /// - <c>pageSize</c> (int, mac dinh 20, toi da 100): So ban ghi moi trang.
    /// - <c>search</c> (string, tuy chon): Tim theo caption.
    /// - <c>isDeleted</c> (bool, tuy chon): <c>true</c> lay post da xoa, <c>false</c> lay post chua xoa, bo trong = tat ca.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Lay danh sach thanh cong.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "items": [
    ///     {
    ///       "id": "guid",
    ///       "ownerUserId": "guid",
    ///       "mediaId": "guid",
    ///       "media": {
    ///         "id": "guid",
    ///         "url": "presigned-url",
    ///         "type": "image | video",
    ///         "thumbnailUrl": "string | null",
    ///         "durationSeconds": 10,
    ///         "width": 1080,
    ///         "height": 1920
    ///       },
    ///       "caption": "string | null",
    ///       "visibility": "friends | private",
    ///       "status": "active | hidden",
    ///       "dailySafetyRecordId": "guid | null",
    ///       "latitude": 10.123456,
    ///       "longitude": 106.123456,
    ///       "deletedAtUtc": "datetime | null",
    ///       "deletedReason": "string | null",
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
    [HasPermission(PermissionCodes.PostManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isDeleted = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListManagedPostsQuery(page, pageSize, search, isDeleted), ct));

    /// <summary>
    /// Lay chi tiet mot post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>posts:read</c>.
    ///
    /// Path params:
    /// - <c>id</c> (guid): Id post can xem.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Lay thanh cong.
    /// - <c>POST_NOT_FOUND</c>: Khong tim thay post.
    ///
    /// Cau truc <c>data</c> khi thanh cong:
    /// <code>
    /// {
    ///   "id": "guid",
    ///   "ownerUserId": "guid",
    ///   "mediaId": "guid",
    ///   "media": {
    ///     "id": "guid",
    ///     "url": "presigned-url",
    ///     "type": "image | video",
    ///     "thumbnailUrl": "string | null",
    ///     "durationSeconds": 10,
    ///     "width": 1080,
    ///     "height": 1920
    ///   },
    ///   "caption": "string | null",
    ///   "visibility": "friends | private",
    ///   "status": "active | hidden",
    ///   "dailySafetyRecordId": "guid | null",
    ///   "latitude": 10.123456,
    ///   "longitude": 106.123456,
    ///   "deletedAtUtc": "datetime | null",
    ///   "deletedReason": "string | null",
    ///   "createdAtUtc": "datetime",
    ///   "updatedAtUtc": "datetime | null"
    /// }
    /// </code>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetManagedPostByIdQuery(id), ct));

    /// <summary>
    /// Cap nhat thong tin co ban cua post.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>posts:update</c>.
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Cap nhat thanh cong.
    /// - <c>POST_NOT_FOUND</c>: Khong tim thay post.
    /// - <c>INVALID_VISIBILITY</c>: Visibility khong hop le.
    /// - <c>INVALID_STATUS</c>: Status khong hop le.
    ///
    /// Cau truc <c>data</c> khi thanh cong: giong voi <c>GET /api/v1/admin/posts/{id}</c>.
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON (tat ca fields tuy chon):
    /// <code>
    /// {
    ///   "caption": "string | null",
    ///   "visibility": "friends | private",
    ///   "status": "active | hidden",
    ///   "latitude": 10.123456,
    ///   "longitude": 106.123456
    /// }
    /// </code>
    /// </param>
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostManagement.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateManagedPostRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateManagedPostCommand(id, request), ct));

    /// <summary>
    /// Soft delete post va luu ly do bi xoa.
    /// </summary>
    /// <remarks>
    /// Yeu cau <c>Authorization: Bearer &lt;token&gt;</c> (admin) va permission <c>posts:delete</c>.
    ///
    /// Request body co the bo trong (empty body duoc phep).
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Xoa thanh cong.
    /// - <c>POST_NOT_FOUND</c>: Khong tim thay post.
    ///
    /// Cau truc <c>data</c> khi thanh cong: <c>null</c>
    ///
    /// Format response chuan: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON (tuy chon):
    /// <code>
    /// { "reason": "string | null" }
    /// </code>
    /// </param>
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PostManagement.Delete)]
    public async Task<IActionResult> SoftDelete(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] SoftDeleteManagedPostRequest? request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(new SoftDeleteManagedPostCommand(id, request?.Reason), ct));
}
