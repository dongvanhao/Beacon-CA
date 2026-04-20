using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands.UpdateAvatar;
using Beacon.Application.Features.Identity.Commands.UpdateProfile;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers;

[Route("api/v1/users")]
public class UsersController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>
    /// Cập nhật thông tin hồ sơ người dùng hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Cập nhật thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ.
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng.
    /// - <c>PHONE_ALREADY_IN_USE</c>: Số điện thoại đã được sử dụng bởi tài khoản khác.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":                  "guid",
    ///   "username":            "string",
    ///   "email":               "string",
    ///   "familyName":          "string",
    ///   "givenName":           "string",
    ///   "phoneNumber":         "string?  (có thể null)",
    ///   "timeZone":            "string",
    ///   "isActive":            "bool",
    ///   "isEmailVerified":     "bool",
    ///   "lastLoginAtUtc":      "datetime?  (UTC)",
    ///   "createdAtUtc":        "datetime  (UTC)",
    ///   "avatarMediaObjectId": "guid?  (có thể null)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateProfileCommand(currentUser.UserId, request), ct));

    #region
    /// <summary>
    /// Cập nhật avatar của người dùng hiện tại từ một media đã upload.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Luồng sử dụng: client upload file qua <c>POST /api/v1/media</c> để nhận <c>mediaObjectId</c>,
    /// sau đó gọi endpoint này để gán media đó làm avatar.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Cập nhật avatar thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Dữ liệu đầu vào không hợp lệ.
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng.
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại hoặc đã bị xóa.
    /// - <c>MEDIA_FORBIDDEN</c>: Media không thuộc về người dùng hiện tại.
    /// - <c>INVALID_FILE_TYPE</c>: Media không phải file ảnh.
    ///
    /// Cấu trúc <c>data</c> khi thành công: giống với <c>PATCH /api/v1/users/me</c>.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPut("me/avatar")]
    [Authorize]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateAvatarCommand(currentUser.UserId, request.MediaObjectId), ct));
}
