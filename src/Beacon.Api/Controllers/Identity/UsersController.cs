using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands.UpdateAvatar;
using Beacon.Application.Features.Identity.Commands.UpdateProfile;
using Beacon.Application.Features.Identity.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// - <c>EMAIL_ALREADY_IN_USE</c>: Email đã được sử dụng bởi tài khoản khác.
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
    ///   "avatarMediaObjectId": "guid?  (có thể null)",
    ///   "avatarUrl":           "string | null  (presigned URL từ MinIO, hết hạn 15 phút; null nếu chưa set avatar)"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="request">
    /// Body JSON chứa thông tin hồ sơ cần cập nhật:
    /// <code>
    /// {
    ///   "familyName":  "string",           // Họ (bắt buộc)
    ///   "givenName":   "string",           // Tên (bắt buộc)
    ///   "email":       "string",           // Email mới (bắt buộc)
    ///   "phoneNumber": "string | null"     // Số điện thoại (tuỳ chọn)
    /// }
    /// </code>
    /// </param>
    #endregion
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateProfileCommand(currentUser.UserId, request), ct));

    #region
    /// <summary>
    /// Upload và gán avatar cho người dùng hiện tại.
    /// </summary>
    /// <remarks>
    /// Yêu cầu <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Body: <c>multipart/form-data</c> với field <c>file</c> (ảnh jpeg/png/webp/gif, tối đa 10MB).
    ///
    /// Luồng xử lý: endpoint tự động upload ảnh lên MinIO, sinh thumbnail, lưu metadata
    /// và gán làm avatar trong một request duy nhất. Avatar cũ (nếu có) sẽ bị soft-delete.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Cập nhật avatar thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: File rỗng, sai MIME type, hoặc vượt quá 10MB.
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng.
    /// - <c>UPLOAD_FAILED</c>: Lỗi khi upload lên storage hoặc lưu metadata.
    ///
    /// Cấu trúc <c>data</c> khi thành công: giống với <c>PATCH /api/v1/users/me</c>,
    /// bao gồm <c>avatarUrl</c> là presigned URL của avatar vừa được gán (hết hạn 15 phút).
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    /// <param name="file">
    /// File ảnh avatar (<c>multipart/form-data</c>, field name: <c>file</c>).
    /// Chấp nhận: image/jpeg, image/png, image/webp, image/gif. Tối đa 10MB.
    /// </param>
    #endregion
    [HttpPut("me/avatar")]
    [Authorize]
    [RequestSizeLimit(11L * 1024 * 1024)]
    public async Task<IActionResult> UpdateAvatar(IFormFile file, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateAvatarCommand(file, currentUser.UserId), ct));
}
