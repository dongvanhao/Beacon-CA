using Beacon.Application.Features.Group.Commands.AcceptFriendRequest;
using Beacon.Application.Features.Group.Commands.DeclineFriendRequest;
using Beacon.Application.Features.Group.Commands.SendFriendRequest;
using Beacon.Application.Features.Group.Dtos;
using Beacon.Application.Features.Group.Queries.ListReceivedFriendRequests;
using Beacon.Application.Features.Group.Queries.ListSentFriendRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Group;

[ApiController]
[Route("api/v1/friend-requests")]
[Authorize]
public class FriendRequestsController(IMediator mediator) : BaseController
{
    #region
    /// <summary>Gửi lời mời kết bạn.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "receiverId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Response khi thành công (HTTP 201):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "senderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "senderFamilyName": "Nguyễn",
    ///     "senderGivenName": "Alice",
    ///     "senderAvatarUrl": null,
    ///     "createdAtUtc": "2026-05-04T10:00:00Z"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 201).
    /// - <c>VALIDATION_ERROR</c>: <c>receiverId</c> rỗng hoặc không hợp lệ (HTTP 400).
    /// - <c>SELF_FRIEND_REQUEST</c>: Không thể gửi lời mời cho chính mình (HTTP 400).
    /// - <c>FRIEND_REQUEST_DUPLICATE</c>: Đã có lời mời đang chờ xử lý giữa hai người (HTTP 409).
    /// - <c>ALREADY_FRIENDS</c>: Hai người đã là bạn bè (HTTP 409).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Send(
        [FromBody] SendFriendRequestRequest req, CancellationToken ct)
        => CreatedResult("api/v1/friend-requests",
            await mediator.Send(new SendFriendRequestCommand(req.ReceiverId), ct));

    #region
    /// <summary>Chấp nhận lời mời kết bạn.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ người **nhận** lời mời mới có thể chấp nhận.
    ///
    /// **Path param:**
    /// - <c>id</c> (guid, bắt buộc): Id của lời mời kết bạn. Lấy từ <c>data[].id</c> trong GET /api/v1/friend-requests/received.
    ///
    /// **Side effect khi thành công:**
    /// - Tạo quan hệ bạn bè giữa hai người.
    /// - Tự động tạo nhóm chat riêng tư và thêm cả hai vào nhóm.
    /// - Lời mời chuyển sang trạng thái <c>Accepted</c>.
    /// - Frontend nên reload danh sách bạn bè và hội thoại sau khi gọi API này.
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
    /// - <c>FRIEND_REQUEST_NOT_FOUND</c>: Không tìm thấy lời mời với <c>id</c> này (HTTP 404).
    /// - <c>FRIEND_REQUEST_FORBIDDEN</c>: User hiện tại không phải người nhận lời mời (HTTP 403).
    /// - <c>FRIEND_REQUEST_NOT_PENDING</c>: Lời mời đã được chấp nhận hoặc từ chối trước đó (HTTP 409).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new AcceptFriendRequestCommand(id), ct));

    #region
    /// <summary>Từ chối lời mời kết bạn.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ người **nhận** lời mời mới có thể từ chối.
    ///
    /// **Path param:**
    /// - <c>id</c> (guid, bắt buộc): Id của lời mời kết bạn. Lấy từ <c>data[].id</c> trong GET /api/v1/friend-requests/received.
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
    /// - <c>FRIEND_REQUEST_NOT_FOUND</c>: Không tìm thấy lời mời với <c>id</c> này (HTTP 404).
    /// - <c>FRIEND_REQUEST_FORBIDDEN</c>: User hiện tại không phải người nhận lời mời (HTTP 403).
    /// - <c>FRIEND_REQUEST_NOT_PENDING</c>: Lời mời đã được xử lý trước đó (HTTP 409).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new DeclineFriendRequestCommand(id), ct));

    #region
    /// <summary>Danh sách lời mời kết bạn đã nhận.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ trả về lời mời có trạng thái <c>Pending</c> (chưa xử lý).
    /// Sắp xếp theo thời gian nhận mới nhất trước.
    ///
    /// **Pagination (cursor-based):**
    /// - Lần đầu: gọi không có <c>cursor</c>.
    /// - Lần tiếp: lấy <c>meta.nextCursor</c> từ response trước.
    /// - Dừng khi <c>meta.hasMore = false</c>.
    ///
    /// **Query params:**
    /// - <c>cursor</c> (string ISO-8601 UTC, tuỳ chọn): Load lời mời cũ hơn mốc này.
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
    ///         "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderFamilyName": "Trần",
    ///         "senderGivenName": "Bob",
    ///         "senderAvatarUrl": null,
    ///         "createdAtUtc": "2026-05-04T09:00:00Z"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": "2026-05-04T09:00:00Z",
    ///       "limit": 20,
    ///       "hasMore": false
    ///     }
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>id</c>: Id của lời mời — dùng để gọi POST /accept hoặc POST /decline.
    /// - <c>senderId</c>: Id của người gửi lời mời.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("received")]
    public async Task<IActionResult> ListReceived(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListReceivedFriendRequestsQuery(cursor, limit), ct));

    #region
    /// <summary>Danh sách lời mời kết bạn đã gửi.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ trả về lời mời có trạng thái <c>Pending</c> (chưa được phản hồi).
    /// Sắp xếp theo thời gian gửi mới nhất trước.
    ///
    /// **Pagination (cursor-based):**
    /// - Lần đầu: gọi không có <c>cursor</c>.
    /// - Lần tiếp: lấy <c>meta.nextCursor</c> từ response trước.
    /// - Dừng khi <c>meta.hasMore = false</c>.
    ///
    /// **Query params:**
    /// - <c>cursor</c> (string ISO-8601 UTC, tuỳ chọn): Load lời mời cũ hơn mốc này.
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
    ///         "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderFamilyName": "Nguyễn",
    ///         "senderGivenName": "Alice",
    ///         "senderAvatarUrl": null,
    ///         "createdAtUtc": "2026-05-04T08:00:00Z"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": "2026-05-04T08:00:00Z",
    ///       "limit": 20,
    ///       "hasMore": false
    ///     }
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>id</c>: Id của lời mời — có thể dùng để hiển thị trạng thái "đang chờ".
    /// - <c>senderFamilyName</c> / <c>senderGivenName</c>: Họ và tên của chính user hiện tại.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("sent")]
    public async Task<IActionResult> ListSent(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListSentFriendRequestsQuery(cursor, limit), ct));
}
