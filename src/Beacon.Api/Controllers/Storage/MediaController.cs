using Beacon.Api.Authorization;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Storage.Commands.HardDelete;
using Beacon.Application.Features.Storage.Commands.SoftDelete;
using Beacon.Application.Features.Storage.Commands.Upload;
using Beacon.Application.Features.Storage.Dtos;
using Beacon.Application.Features.Storage.Queries.GetMediaById;
using Beacon.Application.Features.Storage.Queries.ListMedia;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Storage;

[Route("api/v1/media")]
public class MediaController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>
    /// Upload một file media (ảnh/video) vào bucket private.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Body: <c>multipart/form-data</c> với field <c>file</c>.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Upload thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: File rỗng, sai MIME type, hoặc vượt quá dung lượng (ảnh ≤ 10MB, video ≤ 100MB).
    /// - <c>INVALID_FILE_TYPE</c>: Loại file không được hỗ trợ.
    /// - <c>FILE_TOO_LARGE</c>: File vượt quá dung lượng cho phép.
    /// - <c>UPLOAD_FAILED</c>: Lỗi khi upload lên storage hoặc lưu metadata.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "id":           "guid",
    ///   "url":          "string  (presigned URL, hết hạn 15 phút)",
    ///   "thumbnailUrl": "string? (null nếu là video)",
    ///   "objectKey":    "string  (ví dụ: 2026-04-19/abc123.png)",
    ///   "type":         "string  (image | video)",
    ///   "mimeType":     "string",
    ///   "size":         "long    (byte)",
    ///   "width":        "int?",
    ///   "height":       "int?",
    ///   "createdAt":    "datetime (UTC)",
    ///   "createdBy":    "guid"
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(110L * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] UploadMediaRequest request, CancellationToken ct)
    {
        var command = new UploadMediaCommand(request.File, currentUser.UserId);
        return CreatedResult("api/v1/media", await mediator.Send(command, ct));
    }

    #region
    /// <summary>
    /// Lấy thông tin chi tiết một media + presigned URL để xem.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    /// Chỉ chủ sở hữu media mới được xem (owner check theo <c>UploadProviderByUserId</c>).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công (success = true).
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại hoặc đã bị soft delete.
    /// - <c>MEDIA_FORBIDDEN</c>: Người dùng không phải chủ sở hữu media.
    ///
    /// Cấu trúc <c>data</c> khi thành công: xem endpoint POST /api/v1/media.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetMediaByIdQuery(id, currentUser.UserId), ct));

    #region
    /// <summary>
    /// Liệt kê media của người dùng hiện tại theo cursor pagination (mới nhất → cũ nhất).
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    ///
    /// Query:
    /// - <c>cursor</c> (optional, ISO datetime UTC): trả các media có <c>createdAt</c> nhỏ hơn giá trị này.
    /// - <c>limit</c> (mặc định 20, tối đa 100).
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Thành công (success = true).
    /// - <c>VALIDATION_ERROR</c>: Limit không hợp lệ (ngoài 1-100).
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "data": [ /* danh sách MediaDto, xem endpoint POST */ ],
    ///   "meta": {
    ///     "nextCursor": "datetime? (null khi hasMore = false)",
    ///     "limit":      "int",
    ///     "hasMore":    "bool"
    ///   }
    /// }
    /// </code>
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? cursor,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        if (limit <= 0) limit = 20;
        return HandleResult(await mediator.Send(new ListMediaQuery(currentUser.UserId, cursor, limit), ct));
    }

    #region
    /// <summary>
    /// Soft delete một media — chỉ đánh dấu <c>IsDeleted = true</c>, object trên MinIO vẫn còn.
    /// </summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>.
    /// Chỉ chủ sở hữu media mới được soft delete.
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Xóa thành công (success = true, data = null).
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại hoặc đã bị xóa.
    /// - <c>MEDIA_FORBIDDEN</c>: Người dùng không phải chủ sở hữu media.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete("{id:guid}/soft")]
    [Authorize]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new SoftDeleteMediaCommand(id, currentUser.UserId), ct));

    #region
    /// <summary>
    /// Hard delete một media — xóa vĩnh viễn object trên MinIO + metadata trong DB.
    /// </summary>
    /// <remarks>
    /// Chỉ admin token mới được phép gọi, yêu cầu cả:
    /// - <c>Authorization: Bearer &lt;admin-token&gt;</c>
    /// - Permission <c>media:hard-delete</c>
    ///
    /// Các giá trị <c>code</c> có thể xuất hiện trong response:
    ///
    /// - <c>null</c>: Xóa thành công (success = true, data = null).
    /// - <c>MEDIA_NOT_FOUND</c>: Media không tồn tại.
    /// - <c>STORAGE_UNAVAILABLE</c>: Không xóa được file trên MinIO.
    ///
    /// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpDelete("{id:guid}/hard")]
    [AdminOnly]
    [HasPermission("media:hard-delete")]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new HardDeleteMediaCommand(id), ct));
}
