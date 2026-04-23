using Beacon.Api.Authorization;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Shared.Common.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/admin/auth")]
[Authorize]
public class AdminAuthController(IMediator mediator) : BaseController
{
    #region
    /// <summary>
    /// Đăng nhập dành cho Admin, trả về cặp Access Token + Refresh Token kèm danh sách permissions.
    /// </summary>
    /// <remarks>
    /// Access token của Admin chứa claim <c>roles</c> và <c>permissions</c> để phân quyền RBAC.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng nhập thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Username hoặc password để trống / vượt quá độ dài cho phép.
    /// - <c>INVALID_CREDENTIALS</c>: Sai username hoặc password.
    /// - <c>ADMIN_INACTIVE</c>: Tài khoản admin đã bị vô hiệu hóa.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "adminId":            "guid",
    ///   "username":           "string",
    ///   "fullName":           "string",
    ///   "accessToken":        "string  (JWT, hết hạn sau 15 phút)",
    ///   "refreshToken":       "string  (hết hạn sau 7 ngày)",
    ///   "accessTokenExpiresAt": "datetime (UTC)",
    ///   "permissions":        ["string"]  (ví dụ: ["users:read", "users:write"])
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AdminAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLoginRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LoginAdminCommand(request), ct));

    #region
    /// <summary>
    /// Đăng xuất Admin và thu hồi Refresh Token hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;adminAccessToken&gt;</c> trong header.
    /// Endpoint này được bảo vệ bởi <c>[AdminOnly]</c> — chỉ admin token hợp lệ mới được phép gọi.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng xuất thành công (success = true), data = null.
    /// - <c>VALIDATION_ERROR</c>: Thiếu trường <c>refreshToken</c> trong body.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>ADMIN_TOKEN_INVALID</c>: Refresh token không tồn tại hoặc đã bị thu hồi.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("logout")]
    [AdminOnly]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout([FromBody] AdminLogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutAdminCommand(request.RefreshToken), ct));
}
