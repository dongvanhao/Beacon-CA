using Beacon.Api.Authorization;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Features.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Beacon.Api.Controllers;

[Route("api/v1/admin/auth")]
public class AdminAuthController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>
    /// Đăng nhập dành cho Admin, trả về cặp Access Token + Refresh Token kèm danh sách permissions.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/auth/login</c>
    ///
    /// <b>Auth:</b> Không yêu cầu token.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "string",
    ///   "password": "string"
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
    ///     "fullName": "string",
    ///     "accessToken": "string",
    ///     "refreshToken": "string",
    ///     "accessTokenExpiresAt": "datetime (UTC)",
    ///     "permissions": ["string"]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Access token của Admin chứa claim <c>role</c>, <c>permission</c> và <c>actor=admin</c> để phân quyền RBAC.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng nhập thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Username hoặc password để trống / vượt quá độ dài cho phép.
    /// - <c>INVALID_CREDENTIALS</c>: Sai username hoặc password.
    /// - <c>ADMIN_INACTIVE</c>: Tài khoản admin đã bị vô hiệu hóa.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-admin-login")]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LoginAdminCommand(request), ct));

    #region
    /// <summary>
    /// Đăng xuất Admin và thu hồi Refresh Token hiện tại.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/auth/logout</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token.
    ///
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "refreshToken": "string"
    /// }
    /// </code>
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
    ///
    /// - <c>null</c>: Đăng xuất thành công (success = true), data = null.
    /// - <c>VALIDATION_ERROR</c>: Thiếu trường <c>refreshToken</c> trong body.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>ADMIN_TOKEN_INVALID</c>: Refresh token không tồn tại hoặc đã bị thu hồi.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("logout")]
    [AdminOnly]
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutAdminCommand(request.RefreshToken), ct));

    #region
    /// <summary>
    /// Lấy thông tin Admin đang đăng nhập từ Access Token.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>GET /api/v1/admin/auth/me</c>
    ///
    /// <b>Auth:</b> Yêu cầu Admin access token.
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
    ///     "adminId": "guid",
    ///     "username": "string",
    ///     "fullName": "string",
    ///     "isActive": true,
    ///     "lastLoginAtUtc": "datetime | null",
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "roles": ["string"],
    ///     "permissions": ["string"]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Lấy thông tin admin thành công (success = true).
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>FORBIDDEN</c>: Token hợp lệ nhưng không phải admin token.
    /// - <c>ADMIN_NOT_FOUND</c>: Không tìm thấy admin trong hệ thống.
    /// - <c>ADMIN_INACTIVE</c>: Tài khoản admin đã bị vô hiệu hóa.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpGet("me")]
    [AdminOnly]
    public async Task<IActionResult> Me(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetCurrentAdminQuery(currentUser.UserId), ct));

    #region
    /// <summary>
    /// Cap nhat thong tin Admin dang dang nhap.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>PATCH /api/v1/admin/auth/me</c>
    ///
    /// <b>Auth:</b> Yeu cau Admin access token.
    /// <b>Headers:</b>
    /// <code>
    /// Authorization: Bearer {adminAccessToken}
    /// </code>
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "username": "string | null",
    ///   "fullName": "string | null"
    /// }
    /// </code>
    ///
    /// Bo qua field hoac truyen <c>null</c> de giu nguyen gia tri hien tai.
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
    ///     "fullName": "string",
    ///     "isActive": true,
    ///     "lastLoginAtUtc": "datetime | null",
    ///     "createdAtUtc": "datetime (UTC)",
    ///     "roles": ["string"],
    ///     "permissions": ["string"]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Cac gia tri <c>code</c> co the xuat hien trong response:
    /// - <c>null</c>: Cap nhat thanh cong.
    /// - <c>VALIDATION_ERROR</c>: Body khong hop le hoac khong truyen field nao.
    /// - <c>USERNAME_ALREADY_EXISTS</c>: Username da duoc su dung boi admin khac.
    /// - <c>ADMIN_NOT_FOUND</c>: Khong tim thay admin trong he thong.
    /// - <c>ADMIN_INACTIVE</c>: Tai khoan admin da bi vo hieu hoa.
    /// - <c>UNAUTHORIZED</c>: Access token khong hop le hoac da het han.
    /// - <c>FORBIDDEN</c>: Token hop le nhung khong phai admin token.
    ///
    /// Format response loi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPatch("me")]
    [AdminOnly]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateCurrentAdminRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateCurrentAdminCommand(currentUser.UserId, request), ct));

    #region
    /// <summary>
    /// Làm mới Access Token Admin bằng Refresh Token.
    /// </summary>
    /// <remarks>
    /// <b>Endpoint:</b> <c>POST /api/v1/admin/auth/refresh-token</c>
    ///
    /// <b>Auth:</b> Không yêu cầu access token. Client chỉ cần gửi refresh token còn hiệu lực.
    ///
    /// <b>Request body:</b>
    /// <code>
    /// {
    ///   "refreshToken": "string"
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
    ///     "fullName": "string",
    ///     "accessToken": "string",
    ///     "refreshToken": "string",
    ///     "accessTokenExpiresAt": "datetime (UTC)",
    ///     "permissions": ["string"]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// Refresh token cũ sẽ bị thu hồi và thay bằng cặp token mới. Client phải lưu lại cả
    /// <c>accessToken</c> và <c>refreshToken</c> mới từ response.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Làm mới token thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Thiếu trường <c>refreshToken</c> trong body.
    /// - <c>ADMIN_TOKEN_INVALID</c>: Refresh token không tồn tại, đã hết hạn hoặc đã bị thu hồi.
    /// - <c>ADMIN_NOT_FOUND</c>: Không tìm thấy admin liên kết với token.
    /// - <c>ADMIN_INACTIVE</c>: Tài khoản admin đã bị vô hiệu hóa.
    ///
    /// Format response lỗi: <c>{ success, message, code, data, errors }</c>.
    /// </remarks>
    #endregion
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new RefreshAdminTokenCommand(request.RefreshToken), ct));
}
