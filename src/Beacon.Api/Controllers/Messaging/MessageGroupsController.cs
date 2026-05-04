using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Messaging.Queries.ListMessages;
using Beacon.Application.Features.Messaging.Queries.ListMyMessageGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Messaging;

[ApiController]
[Route("api/v1/message-groups")]
[Authorize]
public class MessageGroupsController(IMediator mediator) : BaseController
{
    #region
    /// <summary>Danh sách các cuộc hội thoại của tôi.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về tất cả group chat mà user hiện tại đang là thành viên, kèm preview tin nhắn cuối.
    /// Sắp xếp theo hoạt động mới nhất trước (group có tin nhắn gần đây lên đầu).
    /// Group chưa có tin nhắn nào sẽ sắp xếp theo <c>createdAtUtc</c>.
    ///
    /// **Pagination (cursor-based):**
    /// - Lần đầu: gọi không có <c>cursor</c>.
    /// - Lần tiếp: lấy <c>meta.nextCursor</c> từ response trước, truyền vào <c>cursor</c>.
    /// - Dừng khi <c>meta.hasMore = false</c>.
    ///
    /// **Query params:**
    /// - <c>cursor</c> (string ISO-8601 UTC, tuỳ chọn): Load các group cũ hơn mốc này. VD: <c>2026-05-04T10:00:00Z</c>
    /// - <c>limit</c> (int, tuỳ chọn, mặc định 20, tối đa 100): Số group mỗi trang.
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
    ///         "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "isPrivate": true,
    ///         "createdAtUtc": "2026-05-01T08:00:00Z",
    ///         "lastMessageContent": "Hẹn gặp nhé!",
    ///         "lastMessageAtUtc": "2026-05-04T10:30:00Z",
    ///         "lastMessageSenderUsername": "alice"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": "2026-05-04T10:30:00Z",
    ///       "limit": 20,
    ///       "hasMore": false
    ///     }
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Lưu ý:**
    /// - <c>lastMessageContent</c>, <c>lastMessageAtUtc</c>, <c>lastMessageSenderUsername</c> là <c>null</c> nếu group chưa có tin nhắn nào.
    /// - <c>messageGroupId</c> trong danh sách bạn bè (GET /api/v1/friends) chính là <c>groupId</c> dùng ở đây.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công.
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> ListGroups(
        [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListMyMessageGroupsQuery(cursor, limit), ct));

    #region
    /// <summary>Gửi tin nhắn vào nhóm.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm. Lấy từ <c>groupId</c> trong danh sách hội thoại hoặc <c>messageGroupId</c> trong danh sách bạn bè.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "content": "Nội dung tin nhắn (tối đa 4000 ký tự)"
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
    ///     "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "senderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "senderUsername": "alice",
    ///     "content": "Nội dung tin nhắn",
    ///     "createdAtUtc": "2026-05-04T10:30:00Z"
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 201).
    /// - <c>VALIDATION_ERROR</c>: <c>content</c> rỗng hoặc vượt quá 4000 ký tự (HTTP 400).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: User không phải thành viên của nhóm này (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> Send(
        Guid groupId, [FromBody] SendMessageRequest req, CancellationToken ct)
        => CreatedResult($"api/v1/message-groups/{groupId}/messages",
            await mediator.Send(new SendMessageCommand(groupId, req.Content), ct));

    #region
    /// <summary>Danh sách tin nhắn trong nhóm.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Dùng để load lịch sử chat của một cuộc hội thoại.
    /// Tin nhắn trả về theo thứ tự **mới nhất trước** (descending).
    /// Frontend cần render ngược lại (oldest → newest từ trên xuống dưới).
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Query params:**
    /// - <c>cursor</c> (string ISO-8601 UTC, tuỳ chọn): Load các tin nhắn **cũ hơn** mốc này. Dùng cho "load more / scroll lên trên".
    /// - <c>limit</c> (int, tuỳ chọn, mặc định 20, tối đa 100): Số tin nhắn mỗi lần load.
    ///
    /// **Cách dùng cursor để load thêm (infinite scroll):**
    /// 1. Lần đầu mở chat: gọi không có <c>cursor</c> → nhận tin nhắn mới nhất.
    /// 2. Khi user scroll lên trên: lấy <c>meta.nextCursor</c> → gọi lại với <c>cursor=nextCursor</c>.
    /// 3. Dừng khi <c>meta.hasMore = false</c>.
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
    ///         "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "senderUsername": "alice",
    ///         "content": "Hẹn gặp nhé!",
    ///         "createdAtUtc": "2026-05-04T10:30:00Z"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": "2026-05-04T10:30:00Z",
    ///       "limit": 20,
    ///       "hasMore": true
    ///     }
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: User không phải thành viên của nhóm này (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("{groupId:guid}/messages")]
    public async Task<IActionResult> ListMessages(
        Guid groupId, [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListMessagesQuery(groupId, cursor, limit), ct));
}
