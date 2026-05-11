using Beacon.Application.Features.Group.Commands.RemoveFriend;
using Beacon.Application.Features.Group.Commands.UpdateFriendType;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Features.Group.Queries.SearchUsers;
using Beacon.Application.Features.Group.Queries.GetFriendDetail;
using Beacon.Application.Features.Group.Queries.ListFriends;
using Beacon.Application.Features.Group.Queries.ListFriendPresence;
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
    ///         "familyName": "Nguyễn",
    ///         "givenName": "Alice",
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
    /// - <c>avatarUrl</c>: Signed URL ảnh đại diện của người bạn, <c>null</c> nếu chưa đặt.
    /// - <c>messageGroupId</c>: Id nhóm chat riêng tư với người bạn này (<c>null</c> trong trường hợp bất thường). Dùng làm <c>groupId</c> khi gọi GET /api/v1/message-groups/{groupId}/messages.
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
    /// <summary>Trạng thái online của bạn bè.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về danh sách bạn bè kèm trạng thái online và thời điểm hoạt động gần nhất.
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
    ///         "familyName": "Nguyễn",
    ///         "givenName": "Alice",
    ///         "avatarUrl": null,
    ///         "isOnline": true,
    ///         "lastActiveAtUtc": "2026-05-01T08:00:00Z"
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
    /// - <c>isOnline</c>: <c>true</c> nếu user đang kết nối socket.
    /// - <c>lastActiveAtUtc</c>: Thời điểm hoạt động gần nhất (UTC).
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>VALIDATION_ERROR</c>: <c>limit</c> nằm ngoài 1–100 (HTTP 400).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet("presence")]
    public async Task<IActionResult> ListPresence(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListFriendPresenceQuery(cursor, limit), ct));

    #region
    /// <summary>Tìm kiếm người dùng theo tên, email hoặc số điện thoại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Query param:**
    /// - <c>search</c> (string, bắt buộc, tối đa 100 ký tự): Từ khoá tìm kiếm.
    ///
    /// **Tìm kiếm hỗ trợ (không phân biệt dấu và hoa/thường):**
    /// - Họ tên (contains): "nguyen hao" khớp "Nguyễn Hảo".
    /// - Họ + Tên liền nhau: "NguyenHao" hoặc "nguyenhao" đều khớp "Nguyễn Hảo".
    /// - Email (contains).
    /// - Số điện thoại (exact match).
    ///
    /// Không trả về chính user đang đăng nhập. Tối đa 10 kết quả. Không phân trang.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": [
    ///     {
    ///       "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///       "familyName": "Nguyễn",
    ///       "givenName": "Hảo",
    ///       "avatarUrl": "https://cdn.example.com/...",
    ///       "friendshipStatus": 0
    ///     }
    ///   ],
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>data</c>: Mảng kết quả, rỗng <c>[]</c> khi không tìm thấy — không bao giờ null.
    /// - <c>userId</c>: Id của người dùng — dùng làm <c>receiverId</c> khi gọi POST /api/v1/friend-requests.
    /// - <c>avatarUrl</c>: Signed URL avatar, <c>null</c> nếu chưa đặt.
    /// - <c>friendshipStatus</c>:
    ///   - <c>0 = None</c>: Chưa có quan hệ — có thể gửi lời mời kết bạn.
    ///   - <c>1 = Friends</c>: Đã là bạn bè.
    ///   - <c>2 = PendingSent</c>: Bạn đã gửi lời mời, đang chờ chấp nhận.
    ///   - <c>3 = PendingReceived</c>: Đối phương đã gửi lời mời cho bạn — lấy <c>id</c> từ GET /api/v1/friend-requests/received rồi gọi POST /{id}/accept.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>VALIDATION_ERROR</c>: <c>search</c> trống hoặc vượt quá 100 ký tự (HTTP 400).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string search, CancellationToken ct)
        => HandleResult(await mediator.Send(new SearchUsersQuery(search), ct));

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
    ///     "familyName": "Nguyễn",
    ///     "givenName": "Alice",
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
    /// - <c>familyName</c> / <c>givenName</c>: Họ và tên của người bạn.
    /// - <c>avatarUrl</c>: Signed URL avatar của người bạn, <c>null</c> nếu chưa đặt.
    /// - <c>type</c>: <c>0</c> = Family, <c>1</c> = CloseFriend, <c>2</c> = Normal, <c>3</c> = Custom.
    /// - <c>createdAtUtc</c>: Thời điểm hai người trở thành bạn bè.
    /// - <c>messageGroupId</c>: Id nhóm chat riêng tư với người bạn này — dùng làm <c>groupId</c> khi gọi GET /api/v1/message-groups/{groupId}/messages.
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
