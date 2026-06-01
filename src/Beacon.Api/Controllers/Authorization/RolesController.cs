using Beacon.Api.Authorization;
using Beacon.Application.Features.Authorization.Dtos;
using Beacon.Application.Features.Authorization.Roles.Commands.AssignPermissionToRole;
using Beacon.Application.Features.Authorization.Roles.Commands.AssignRoleToAdmin;
using Beacon.Application.Features.Authorization.Roles.Commands.CreateRole;
using Beacon.Application.Features.Authorization.Roles.Commands.DeleteRole;
using Beacon.Application.Features.Authorization.Roles.Commands.UpdateRole;
using Beacon.Application.Features.Authorization.Roles.Queries.GetRoleById;
using Beacon.Application.Features.Authorization.Roles.Queries.ListRoles;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Authorization;

[Route("api/v1/admin/roles")]
public class RolesController(IMediator mediator) : BaseController
{
    #region
    /// <summary>
    /// Lấy danh sách role trong hệ thống.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/roles</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:read</c>.
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
    ///         "isActive": true,
    ///         "createdAtUtc": "datetime (UTC)",
    ///         "permissions": [
    ///           {
    ///             "id": "guid",
    ///             "name": "string",
    ///             "description": "string | null",
    ///             "group": "string | null"
    ///           }
    ///         ]
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
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:read</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpGet]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListRolesQuery(page, pageSize, search), ct));

    #region
    /// <summary>
    /// Lấy chi tiết một role theo id.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/roles/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:read</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id role cần xem.
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
    ///     "isActive": true,
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "permissions": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string | null",
    ///         "group": "string | null"
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Lấy chi tiết thành công.
    /// - <c>ROLE_NOT_FOUND</c>: Không tìm thấy role.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:read</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetRoleByIdQuery(id), ct));

    #region
    /// <summary>
    /// Tạo role mới.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/roles</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:create</c>.
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
    ///   "permissionIds": ["guid"]
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
    ///     "isActive": true,
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "permissions": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string | null",
    ///         "group": "string | null"
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Tạo thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>ROLE_ALREADY_EXISTS</c>: Role name đã tồn tại.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:create</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.Create)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct)
        => CreatedResult("api/v1/admin/roles",
            await mediator.Send(new CreateRoleCommand(request), ct));

    #region
    /// <summary>
    /// Cập nhật role.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>PUT /api/v1/admin/roles/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:update</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id role cần cập nhật.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "name": "string",
    ///   "description": "string | null",
    ///   "isActive": true
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
    ///     "isActive": true,
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "permissions": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string | null",
    ///         "group": "string | null"
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Cập nhật thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>ROLE_NOT_FOUND</c>: Không tìm thấy role.
    /// - <c>ROLE_ALREADY_EXISTS</c>: Role name đã tồn tại.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:update</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateRoleCommand(id, request), ct));

    #region
    /// <summary>
    /// Xóa role khỏi hệ thống.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>DELETE /api/v1/admin/roles/{id}</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:delete</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>id</c>: Id role cần xóa.
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
    /// - <c>ROLE_NOT_FOUND</c>: Không tìm thấy role.
    /// - <c>ROLE_IN_USE</c>: Role đang được gắn với admin.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:delete</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeleteRoleCommand(id), ct));

    #region
    /// <summary>
    /// Gắn permission vào role.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/roles/{roleId}/permissions</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>roles:assign-permission</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>roleId</c>: Id role cần gắn permission.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "permissionId": "guid"
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
    ///     "isActive": true,
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "permissions": [
    ///       {
    ///         "id": "guid",
    ///         "name": "string",
    ///         "description": "string | null",
    ///         "group": "string | null"
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Gắn permission thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>ROLE_NOT_FOUND</c>: Không tìm thấy role.
    /// - <c>ROLE_INACTIVE</c>: Role đã bị vô hiệu hóa.
    /// - <c>PERMISSION_NOT_FOUND</c>: Không tìm thấy permission.
    /// - <c>ROLE_PERMISSION_ALREADY_EXISTS</c>: Role đã có permission này.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>roles:assign-permission</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("{roleId:guid}/permissions")]
    [AdminOnly]
    [HasPermission(PermissionCodes.RoleManagement.AssignPermission)]
    public async Task<IActionResult> AssignPermission(
        Guid roleId,
        [FromBody] AssignPermissionToRoleRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(
            new AssignPermissionToRoleCommand(roleId, request.PermissionId), ct));

    #region
    /// <summary>
    /// Gắn role vào admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/roles/{roleId}/admins</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token và permission <c>admins:assign-role</c>.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Path params:</b>
    /// - <c>roleId</c>: Id role cần gắn vào admin.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "adminId": "guid"
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
    ///     "adminId": "guid",
    ///     "username": "string",
    ///     "roleId": "guid",
    ///     "roleName": "string",
    ///     "assignedAtUtc": "datetime (UTC)"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    /// - <c>null</c>: Gắn role thành công.
    /// - <c>VALIDATION_ERROR</c>: Body không hợp lệ.
    /// - <c>ROLE_NOT_FOUND</c>: Không tìm thấy role.
    /// - <c>ROLE_INACTIVE</c>: Role đã bị vô hiệu hóa.
    /// - <c>ADMIN_NOT_FOUND</c>: Không tìm thấy admin.
    /// - <c>ADMIN_INACTIVE</c>: Admin đã bị vô hiệu hóa.
    /// - <c>ADMIN_ROLE_ALREADY_EXISTS</c>: Admin đã có role này.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Admin không có permission <c>admins:assign-role</c>.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("{roleId:guid}/admins")]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.AssignRole)]
    public async Task<IActionResult> AssignToAdmin(
        Guid roleId,
        [FromBody] AssignRoleToAdminRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(
            new AssignRoleToAdminCommand(roleId, request.AdminId), ct));
}
