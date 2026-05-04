using Beacon.Application.Features.Group.Commands.RemoveFriend;
using Beacon.Application.Features.Group.Commands.UpdateFriendType;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Features.Group.Queries.FindUserByPhone;
using Beacon.Application.Features.Group.Queries.GetFriendDetail;
using Beacon.Application.Features.Group.Queries.ListFriends;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Group;

[ApiController]
[Route("api/v1/friends")]
[Authorize]
public class FriendsController(IMediator mediator) : BaseController
{
    #region
    /// <summary>Danh sách bạn bè của tôi.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về tất cả bạn bè của user hiện tại, sắp xếp theo thời gian kết bạn mới nhất trước.
    ///
    /// **Pagination (cursor-based):**
    /// - Lần đầu: gọi không có <c>cursor</c>.
    /// - Lần tiếp: lấy <c>meta.nextCursor</c> từ response trước, truyền vào <c>cursor</c>.
    /// - Dừng khi <c>meta.hasMore = false</c>.
    ///
    /// **Query params:**
    /// - <c>cursor</c> (string ISO-8601 UTC, tuỳ chọn): Load bạn bè kết bạn cũ hơn mốc này.
    /// - <c>limit</c> (int, tuỳ chọn, mặc định 20, tối đa 100): Số bản ghi mỗi trang.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "data": [
    ///       {
    ///         "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "username": "alice",
    ///         "avatarUrl": null,
    ///         "type": 2,
    ///         "createdAtUtc": "2026-05-01T08:00:00Z",
    ///         "messageGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": "2026-05-01T08:00:00Z",
    ///       "limit": 20,
    ///       "hasMore": false
    ///     }
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>userId</c>: Id của người bạn (không phải của user hiện tại).
    /// - <c>type</c>: Loại bạn bè — <c>0</c> = Family, <c>1</c> = CloseFriend, <c>2</c> = Normal, <c>3</c> = Custom.
    /// - <c>messageGroupId</c>: Id nhóm chat riêng tư với người bạn này. Dùng để gọi GET /api/v1/message-groups/{groupId}/messages.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListFriendsQuery(cursor, limit), ct));

    #region
    /// <summary>Tìm người dùng theo số điện thoại để gửi lời mời kết bạn.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Tìm kiếm chính xác theo số điện thoại trong toàn bộ hệ thống.
    /// Trả về thông tin người dùng kèm trạng thái quan hệ với user hiện tại.
    /// Không trả kết quả nếu số điện thoại là của chính user đang đăng nhập.
    ///
    /// **Query params:**
    /// - <c>search</c> (string, bắt buộc, tối thiểu 3 ký tự): Từ khoá tìm kiếm — hiện tại khớp theo số điện thoại (exact match).
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "username": "alice",
    ///     "avatarUrl": null,
    ///     "friendshipStatus": 0
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giá trị <c>friendshipStatus</c>:**
    /// - <c>0 = None</c>: Chưa có quan hệ — dùng <c>userId</c> làm <c>receiverId</c> để gọi <c>POST /api/v1/friend-requests</c>.
    /// - <c>1 = Friends</c>: Đã là bạn bè.
    /// - <c>2 = PendingSent</c>: Bạn đã gửi lời mời, đang chờ đối phương chấp nhận.
    /// - <c>3 = PendingReceived</c>: Đối phương đã gửi lời mời cho bạn — gọi <c>POST /api/v1/friend-requests/{id}/accept</c>.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>VALIDATION_ERROR</c>: <c>search</c> trống hoặc ngắn hơn 3 ký tự (HTTP 400).
    /// - <c>USER_NOT_FOUND</c>: Không tìm thấy người dùng với số điện thoại này (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string search, CancellationToken ct)
        => HandleResult(await mediator.Send(new FindUserByPhoneQuery(search), ct));

    #region
    /// <summary>Lấy thông tin chi tiết một người bạn.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Path param:**
    /// - <c>userId</c> (guid, bắt buộc): Id của người bạn muốn xem.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "username": "alice",
    ///     "avatarUrl": null,
    ///     "type": 2,
    ///     "createdAtUtc": "2026-05-01T08:00:00Z",
    ///     "messageGroupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>type</c>: <c>0</c> = Family, <c>1</c> = CloseFriend, <c>2</c> = Normal, <c>3</c> = Custom.
    /// - <c>messageGroupId</c>: Dùng để mở chat với người bạn này.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>FRIEND_NOT_FOUND</c>: <c>userId</c> không phải bạn bè của user hiện tại (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetDetail(Guid userId, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetFriendDetailQuery(userId), ct));

    #region
    /// <summary>Cập nhật loại bạn bè.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Path param:**
    /// - <c>userId</c> (guid, bắt buộc): Id của người bạn muốn cập nhật.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "type": 0
    /// }
    /// </code>
    ///
    /// **Giá trị <c>type</c>:**
    /// - <c>0</c> = Family (Gia đình)
    /// - <c>1</c> = CloseFriend (Bạn thân)
    /// - <c>2</c> = Normal (Bạn bè thường)
    /// - <c>3</c> = Custom (Tuỳ chỉnh)
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": null,
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>VALIDATION_ERROR</c>: Giá trị <c>type</c> không nằm trong 0–3 (HTTP 400).
    /// - <c>FRIEND_NOT_FOUND</c>: <c>userId</c> không phải bạn bè của user hiện tại (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPatch("{userId:guid}/type")]
    public async Task<IActionResult> UpdateType(
        Guid userId, [FromBody] UpdateFriendTypeRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateFriendTypeCommand(userId, req.Type), ct));

    #region
    /// <summary>Xóa bạn bè.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Path param:**
    /// - <c>userId</c> (guid, bắt buộc): Id của người bạn muốn xóa.
    ///
    /// **Side effect:** Cả hai người dùng bị xóa khỏi nhóm chat chung — không thể gửi tin nhắn sau khi xóa.
    /// Lịch sử tin nhắn vẫn được lưu trên server nhưng không thể truy cập qua API.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": null,
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>FRIEND_NOT_FOUND</c>: <c>userId</c> không phải bạn bè của user hiện tại (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken ct)
        => HandleResult(await mediator.Send(new RemoveFriendCommand(userId), ct));
}
