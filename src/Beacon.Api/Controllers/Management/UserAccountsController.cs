using Beacon.Api.Authorization;
using Beacon.Application.Features.AccountManagement.Dtos;
using Beacon.Application.Features.AccountManagement.Users.Commands.CreateUser;
using Beacon.Application.Features.AccountManagement.Users.Commands.DeleteUser;
using Beacon.Application.Features.AccountManagement.Users.Commands.UpdateUser;
using Beacon.Application.Features.AccountManagement.Users.Queries.GetUserById;
using Beacon.Application.Features.AccountManagement.Users.Queries.ListUsers;
using Beacon.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Management;

[Route("api/v1/admin/users")]
public class UserAccountsController(IMediator mediator) : BaseController
{
    /// <summary>
    /// Lay danh sach tai khoan user.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/users</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>users:read</c>.
    ///
    /// <b>Query params:</b>
    /// - <c>page</c> (int, mac dinh 1): Trang can lay.
    /// - <c>pageSize</c> (int, mac dinh 20, toi da 100): So ban ghi moi trang.
    /// - <c>search</c> (string, tuy chon): Tim theo ten, username, email hoac so dien thoai.
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
    ///         "username": "user01",
    ///         "email": "user@example.com",
    ///         "familyName": "Nguyen",
    ///         "givenName": "An",
    ///         "phoneNumber": "0900000000",
    ///         "timeZone": "Asia/Ha_Noi",
    ///         "isActive": true,
    ///         "isEmailVerified": false,
    ///         "avatarMediaObjectId": null,
    ///         "lastLoginAtUtc": null,
    ///         "lastActiveAtUtc": null,
    ///         "createdAtUtc": "2026-05-01T10:00:00Z"
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
    [HasPermission(PermissionCodes.UserManagement.Read)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListUsersQuery(page, pageSize, search), ct));

    /// <summary>
    /// Lay chi tiet mot tai khoan user.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/users/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>users:read</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id user can xem.
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
    ///     "username": "user01",
    ///     "email": "user@example.com",
    ///     "familyName": "Nguyen",
    ///     "givenName": "An",
    ///     "phoneNumber": null,
    ///     "timeZone": "Asia/Ha_Noi",
    ///     "isActive": true,
    ///     "isEmailVerified": false,
    ///     "avatarMediaObjectId": null,
    ///     "lastLoginAtUtc": null,
    ///     "lastActiveAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>USER_NOT_FOUND</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpGet("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.UserManagement.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetUserByIdQuery(id), ct));

    /// <summary>
    /// Tao tai khoan user moi.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/users</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>users:create</c>.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "user01",
    ///   "email": "user@example.com",
    ///   "password": "Password123!",
    ///   "familyName": "Nguyen",
    ///   "givenName": "An",
    ///   "phoneNumber": "0900000000",
    ///   "timeZone": "Asia/Ha_Noi",
    ///   "isActive": true,
    ///   "isEmailVerified": false
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
    ///     "username": "user01",
    ///     "email": "user@example.com",
    ///     "familyName": "Nguyen",
    ///     "givenName": "An",
    ///     "phoneNumber": "0900000000",
    ///     "timeZone": "Asia/Ha_Noi",
    ///     "isActive": true,
    ///     "isEmailVerified": false,
    ///     "avatarMediaObjectId": null,
    ///     "lastLoginAtUtc": null,
    ///     "lastActiveAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>VALIDATION_ERROR</c>, <c>USERNAME_ALREADY_EXISTS</c>, <c>EMAIL_ALREADY_EXISTS</c>, <c>PHONE_ALREADY_EXISTS</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpPost]
    [AdminOnly]
    [HasPermission(PermissionCodes.UserManagement.Create)]
    public async Task<IActionResult> Create([FromBody] CreateUserAccountRequest request, CancellationToken ct)
        => CreatedResult("api/v1/admin/users",
            await mediator.Send(new CreateUserCommand(request), ct));

    /// <summary>
    /// Cap nhat tai khoan user.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>PUT /api/v1/admin/users/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>users:update</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id user can cap nhat.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "user01",
    ///   "email": "user@example.com",
    ///   "familyName": "Nguyen",
    ///   "givenName": "An",
    ///   "phoneNumber": "0900000000",
    ///   "timeZone": "Asia/Ha_Noi",
    ///   "password": "Password123! | null",
    ///   "isActive": true,
    ///   "isEmailVerified": false
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
    ///     "username": "user01",
    ///     "email": "user@example.com",
    ///     "familyName": "Nguyen",
    ///     "givenName": "An",
    ///     "phoneNumber": "0900000000",
    ///     "timeZone": "Asia/Ha_Noi",
    ///     "isActive": true,
    ///     "isEmailVerified": false,
    ///     "avatarMediaObjectId": null,
    ///     "lastLoginAtUtc": null,
    ///     "lastActiveAtUtc": null,
    ///     "createdAtUtc": "2026-05-01T10:00:00Z"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// <b>Error codes:</b> <c>VALIDATION_ERROR</c>, <c>USER_NOT_FOUND</c>, <c>USERNAME_ALREADY_EXISTS</c>, <c>EMAIL_ALREADY_IN_USE</c>, <c>PHONE_ALREADY_IN_USE</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.UserManagement.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserAccountRequest request,
        CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateUserCommand(id, request), ct));

    /// <summary>
    /// Vo hieu hoa tai khoan user.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>DELETE /api/v1/admin/users/{id}</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token va permission <c>users:delete</c>.
    ///
    /// <b>Path params:</b>
    /// - <c>id</c> (guid): Id user can vo hieu hoa.
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
    /// <b>Error codes:</b> <c>USER_NOT_FOUND</c>, <c>UNAUTHORIZED</c>, <c>FORBIDDEN</c>.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [AdminOnly]
    [HasPermission(PermissionCodes.UserManagement.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeleteUserCommand(id), ct));
}
