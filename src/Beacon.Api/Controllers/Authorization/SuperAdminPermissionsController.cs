using Beacon.Api.Authorization;
using Beacon.Application.Features.Authorization.Permissions.Commands.UpsertPermissionCatalog;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Authorization;

[Route("api/v1/admin/super-admin/permissions")]
[SuperAdminOnly]
public class SuperAdminPermissionsController(IMediator mediator) : BaseController
{
    #region
    /// <summary>
    /// Upsert toàn bộ permission từ file constants vào database.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/super-admin/permissions/upsert</c>
    ///
    /// <b>Auth:</b> Chỉ SuperAdmin được phép gọi.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {superAdminAccessToken}
    /// </code>
    ///
    /// <b>Request body:</b> Không có.
    ///
    /// <b>Nguồn dữ liệu:</b> API đọc danh sách quyền từ <c>PermissionCodes.All</c>
    /// trong file <c>Beacon.Shared/Constants/PermissionCodes.cs</c>.
    ///
    /// <b>Hành vi upsert:</b>
    /// - Nếu permission chưa tồn tại theo <c>name</c>, API sẽ tạo mới.
    /// - Nếu permission đã tồn tại, API cập nhật <c>description</c> và <c>group</c>.
    /// - API không xóa các permission khác đang có trong database nhưng không còn nằm trong file constants.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "total": 11,
    ///     "created": 11,
    ///     "updated": 0,
    ///     "unchanged": 0,
    ///     "permissions": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string",
    ///         "group": "string"
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Upsert thành công.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Token hợp lệ nhưng không phải SuperAdmin.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert(CancellationToken ct)
        => HandleResult(await mediator.Send(new UpsertPermissionCatalogCommand(), ct));
}
