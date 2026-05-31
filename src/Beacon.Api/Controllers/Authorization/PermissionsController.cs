using Beacon.Api.Authorization;
using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Features.Authorization.Permissions.Commands.CreatePermission;
using Beacon.Application.Features.Authorization.Permissions.Commands.DeletePermission;
using Beacon.Application.Features.Authorization.Permissions.Commands.UpdatePermission;
using Beacon.Application.Features.Authorization.Permissions.Queries.GetPermissionById;
using Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissionGroups;
using Beacon.Application.Features.Authorization.Permissions.Queries.ListPermissions;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Authorization;

[Route("api/v1/admin/permissions")]
public class PermissionsController(IMediator mediator) : BaseController
{
    #region
    /// <summary>
    /// Lấy danh sách permission trong hệ thống.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/permissions</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>permissions:read</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Request body:</b> Không có.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "items": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string | null",
    ///         "group": "string | null"
    ///       }
    ///     ],
    ///     "totalCount": 1,
    ///     "page": 1,
    ///     "pageSize": 20,
    ///     "totalPages": 1,
    ///     "hasNextPage": false,
    ///     "hasPreviousPage": false
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Lấy danh sách thành công.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>permissions:read</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpGet]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? group = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(
            new ListPermissionsQuery(page, pageSize, search, group), ct));

    [HttpGet("groups")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Read)]
    public async Task<IActionResult> ListGroups(CancellationToken ct)
        => HandleResult(await mediator.Send(new ListPermissionGroupsQuery(), ct));

    #region
    /// <summary>
    /// Lấy chi tiết một permission theo id.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/permissions/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>permissions:read</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id permission cần xem.
    ///
    /// <b>Request body:</b> Không có.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "id": "guid",
    ///     "name": "string",
    ///     "description": "string | null",
    ///     "group": "string | null"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Lấy chi tiết thành công.
    /// - <c>PERMISSION_NOT_FOUND</c>: Không tìm thấy permission.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>permissions:read</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetPermissionByIdQuery(id), ct));

    #region
    /// <summary>
    /// Tạo permission mới.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/permissions</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>permissions:create</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "name": "string",
    ///   "description": "string | null",
    ///   "group": "string | null"
    /// }
    /// </code>
    ///
    /// <b>Response 201:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Created successfully",
    ///   "code": null,
    ///   "data": {
    ///     "id": "guid",
    ///     "name": "string",
    ///     "description": "string | null",
    ///     "group": "string | null"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Tạo thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>PERMISSION_ALREADY_EXISTS</c>: Permission name đã tồn tại.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>permissions:create</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Create)]
    public async Task<IActionResult> Create([FromBody] CreatePermissionRequest request, CancellationToken ct)
        => CreatedResult("api/v1/admin/permissions",
            await mediator.Send(new CreatePermissionCommand(request), ct));

    #region
    /// <summary>
    /// Cập nhật permission.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>PUT /api/v1/admin/permissions/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>permissions:update</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id permission cần cập nhật.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "name": "string",
    ///   "description": "string | null",
    ///   "group": "string | null"
    /// }
    /// </code>
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "id": "guid",
    ///     "name": "string",
    ///     "description": "string | null",
    ///     "group": "string | null"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Cập nhật thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>PERMISSION_NOT_FOUND</c>: Không tìm thấy permission.
    /// - <c>PERMISSION_ALREADY_EXISTS</c>: Permission name đã tồn tại.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>permissions:update</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePermissionRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdatePermissionCommand(id, request), ct));

    #region
    /// <summary>
    /// Xóa permission khỏi hệ thống.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>DELETE /api/v1/admin/permissions/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>permissions:delete</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id permission cần xóa.
    ///
    /// <b>Request body:</b> Không có.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": null,
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Xóa thành công.
    /// - <c>PERMISSION_NOT_FOUND</c>: Không tìm thấy permission.
    /// - <c>PERMISSION_IN_USE</c>: Permission đang được gắn với role.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>permissions:delete</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.PermissionManagement.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeletePermissionCommand(id), ct));
}
