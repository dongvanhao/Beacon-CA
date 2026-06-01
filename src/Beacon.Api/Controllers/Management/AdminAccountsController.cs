using Beacon.Api.Authorization;
using Beacon.Application.Features.AccountManagement.Admins.Commands.CreateAdmin;
using Beacon.Application.Features.AccountManagement.Admins.Commands.DeleteAdmin;
using Beacon.Application.Features.AccountManagement.Admins.Commands.UpdateAdmin;
using Beacon.Application.Features.AccountManagement.Admins.Queries.GetAdminById;
using Beacon.Application.Features.AccountManagement.Admins.Queries.ListAdmins;
using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/admins")]
public class AdminAccountsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Lay danh sach tai khoan admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/admins</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admins:read</c>.
    ///
    /// <b>Query params:</b>
    /// - <c>page</c> (int, mac dinh 1): Trang can lay.
    /// - <c>pageSize</c> (int, mac dinh 20, toi da 100): So ban ghi moi trang.
    /// - <c>search</c> (string, tuy chon): Tim theo username hoac fullName.
    ///
    /// <b>Request body:</b> Khong co.
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
    ///         "username": "admin",
    ///         "fullName": "Admin User",
    ///         "isActive": true,
    ///         "lastLoginAtUtc": "2026-05-31T10:00:00Z",
    ///         "createdAtUtc": "2026-05-01T10:00:00Z",
    ///         "roles": [
    ///           { "id": "guid", "name": "SuperAdmin", "description": null, "isActive": true }
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
    /// <b>Error codes:</b> <c>VALIDATION_ERROR</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpGet]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListAdminsQuery(page, pageSize, search), ct));

    /// <summary>
    /// Lay chi tiet mot tai khoan admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/admins/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admins:read</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id admin can xem.
    ///
    /// <b>Request body:</b> Khong co.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "id": "guid",
    ///     "username": "admin",
    ///     "fullName": "Admin User",
    ///     "isActive": true,
    ///     "lastLoginAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z",
    ///     "roles": []
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>ADMIN_NOT_FOUND</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetAdminByIdQuery(id), ct));

    /// <summary>
    /// Tao tai khoan admin moi.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/admins</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admins:create</c>.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "admin",
    ///   "fullName": "Admin User",
    ///   "password": "Password123!",
    ///   "roleIds": ["guid"]
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
    ///     "username": "admin",
    ///     "fullName": "Admin User",
    ///     "isActive": true,
    ///     "lastLoginAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z",
    ///     "roles": []
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>VALIDATION_ERROR</c>, <c>USERNAME_ALREADY_EXISTS</c>, <c>ROLE_NOT_FOUND</c>, <c>ROLE_INACTIVE</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpPost]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.Create)]
    public async Task<IActionResult> Create([FromBody] CreateAdminAccountRequest request, CancellationToken ct)
        => CreatedResult("api/v1/admin/admins",
            await mediator.Send(new CreateAdminCommand(request), ct));

    /// <summary>
    /// Cap nhat tai khoan admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>PUT /api/v1/admin/admins/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admins:update</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id admin can cap nhat.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "admin",
    ///   "fullName": "Admin User",
    ///   "password": "Password123! | null",
    ///   "isActive": true,
    ///   "roleIds": ["guid"]
    /// }
    /// </code>
    ///
    /// Neu <c>roleIds</c> la <c>null</c> thi giu nguyen role hien tai; neu la <c>[]</c> thi go toan bo role.
    ///
    /// <b>Response 200:</b>
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "Success",
    ///   "code": null,
    ///   "data": {
    ///     "id": "guid",
    ///     "username": "admin",
    ///     "fullName": "Admin User",
    ///     "isActive": true,
    ///     "lastLoginAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z",
    ///     "roles": []
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>VALIDATION_ERROR</c>, <c>ADMIN_NOT_FOUND</c>, <c>USERNAME_ALREADY_EXISTS</c>, <c>ROLE_NOT_FOUND</c>, <c>ROLE_INACTIVE</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAdminAccountRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateAdminCommand(id, request), ct));

    /// <summary>
    /// Vo hieu hoa tai khoan admin.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>DELETE /api/v1/admin/admins/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>admins:delete</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id admin can vo hieu hoa.
    ///
    /// <b>Request body:</b> Khong co.
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
    /// <b>Error codes:</b> <c>ADMIN_NOT_FOUND</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.AdminManagement.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeleteAdminCommand(id), ct));
}
