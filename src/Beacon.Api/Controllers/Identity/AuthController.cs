using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Features.Identity.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/auth")]
public class AuthController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>
    /// Đăng ký tài khoản người dùng mới.
    /// </summary>
    /// <remarks>
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng ký thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ (username sai format, password yếu, v.v.).
    /// - <c>USERNAME_ALREADY_EXISTS</c>: Tên đăng nhập đã được sử dụng.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "userId":             "guid",
    ///   "username":           "string",
    ///   "fullName":           "string",
    ///   "accessToken":        "string  (JWT, hết hạn sau 15 phút)",
    ///   "refreshToken":       "string  (hết hạn sau 7 ngày)",
    ///   "accessTokenExpiresAt": "datetime (UTC)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        return CreatedResult("api/v1/auth/me", await mediator.Send(new RegisterCommand(request, userAgent), ct));
    }

    #region
    /// <summary>
    /// Đăng nhập bằng username và password, trả về cặp Access Token + Refresh Token.
    /// </summary>
    /// <remarks>
    /// Thiết bị được tự động nhận diện qua <c>User-Agent</c> header — client không cần gửi thêm thông tin thiết bị.
    /// Mỗi lần đăng nhập sẽ thu hồi toàn bộ refresh token cũ (single-session policy).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng nhập thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Username hoặc password để trống / vượt quá độ dài cho phép.
    /// - <c>INVALID_CREDENTIALS</c>: Sai username hoặc password.
    /// - <c>ACCOUNT_INACTIVE</c>: Tài khoản đã bị vô hiệu hóa.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "userId":             "guid",
    ///   "username":           "string",
    ///   "fullName":           "string",
    ///   "accessToken":        "string  (JWT, hết hạn sau 15 phút)",
    ///   "refreshToken":       "string  (hết hạn sau 7 ngày)",
    ///   "accessTokenExpiresAt": "datetime (UTC)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        return HandleResult(await mediator.Send(new LoginCommand(request, userAgent), ct));
    }

    #region
    /// <summary>
    /// Đăng xuất và thu hồi Refresh Token hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;accessToken&gt;</c> trong header.
    /// Sau khi gọi thành công, refresh token được truyền lên sẽ không thể dùng lại.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Đăng xuất thành công (success = true), data = null.
    /// - <c>VALIDATION_ERROR</c>: Thiếu trường <c>refreshToken</c> trong body.
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>TOKEN_INVALID</c>: Refresh token không tồn tại hoặc đã bị thu hồi trước đó.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new LogoutCommand(request.RefreshToken), ct));

    #region 
    /// <summary>
    /// Lấy thông tin profile của người dùng đang đăng nhập từ Access Token.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;accessToken&gt;</c> trong header.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Lấy thông tin thành công (success = true).
    /// - <c>UNAUTHORIZED</c>: Access token không hợp lệ hoặc đã hết hạn.
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng trong hệ thống.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":              "guid",
    ///   "username":        "string",
    ///   "fullName":        "string",
    ///   "phoneNumber":     "string | null",
    ///   "timeZone":        "string  (ví dụ: Asia/Ha_Noi)",
    ///   "isActive":        "boolean",
    ///   "isEmailVerified": "boolean",
    ///   "lastLoginAtUtc":  "datetime | null",
    ///   "createdAtUtc":    "datetime"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetCurrentUserQuery(currentUser.UserId), ct));

    #region 
    /// <summary>
    /// Làm mới Access Token bằng Refresh Token (token rotation).
    /// </summary>
    /// <remarks>
    /// Refresh token cũ sẽ bị thu hồi ngay lập tức và thay thế bằng cặp token mới.
    /// Client phải lưu lại cả <c>accessToken</c> và <c>refreshToken</c> mới từ response.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Làm mới token thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Thiếu trường <c>refreshToken</c> trong body.
    /// - <c>TOKEN_INVALID</c>: Refresh token không tồn tại, đã hết hạn hoặc đã bị thu hồi.
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng liên kết với token.
    /// - <c>ACCOUNT_INACTIVE</c>: Tài khoản đã bị vô hiệu hóa.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "userId":             "guid",
    ///   "username":           "string",
    ///   "fullName":           "string",
    ///   "accessToken":        "string  (JWT mới, hết hạn sau 15 phút)",
    ///   "refreshToken":       "string  (token mới, hết hạn sau 7 ngày)",
    ///   "accessTokenExpiresAt": "datetime (UTC)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct));
}
