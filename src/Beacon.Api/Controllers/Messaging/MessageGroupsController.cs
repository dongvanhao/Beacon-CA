using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.AddGroupMember;
using Beacon.Application.Features.Messaging.Commands.CreateGroup;
using Beacon.Application.Features.Messaging.Commands.LeaveGroup;
using Beacon.Application.Features.Messaging.Commands.MarkGroupMessagesSeen;
using Beacon.Application.Features.Messaging.Commands.RemoveGroupMember;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Commands.TransferOwnership;
using Beacon.Application.Features.Messaging.Commands.UpdateGroup;
using Beacon.Application.Features.Messaging.Commands.UpdateTypingStatus;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Messaging.Queries.GetMessageGroupDetail;
using Beacon.Application.Features.Messaging.Queries.ListMessages;
using Beacon.Application.Features.Messaging.Queries.ListMyMessageGroups;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Beacon.Api.Controllers.Messaging;

[ApiController]
[Route("api/v1/message-groups")]
[Authorize]
public class MessageGroupsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Danh sách các cuộc hội thoại của tôi.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về tất cả group chat mà user hiện tại đang là thành viên, kèm preview tin nhắn cuối.
    /// Mỗi group có kèm trạng thái seen của user hiện tại: <c>lastMessageId</c>, <c>lastSeenMessageId</c>,
    /// <c>isSeenLatest</c> và <c>unreadCount</c>.
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
    ///         "type": 0,
    ///         "peerUserId": "bbbbbbbb-0000-0000-0000-000000000002",
    ///         "createdAtUtc": "2026-05-01T08:00:00Z",
    ///         "lastMessageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "lastMessageContent": "Hẹn gặp nhé!",
    ///         "lastMessageAtUtc": "2026-05-04T10:30:00Z",
    ///         "lastMessageSenderFamilyName": "Nguyễn",
    ///         "lastMessageSenderGivenName": "Alice",
    ///         "lastSeenMessageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "isSeenLatest": true,
    ///         "unreadCount": 0,
    ///         "displayName": "Trần Bob",
    ///         "displayAvatarUrl": null
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
    /// **Giải thích các trường:**
    /// - <c>displayName</c>: Tên hiển thị đã resolve. DIRECT = tên user còn lại; GROUP = tên group, fallback bằng tên một số thành viên, cuối cùng là "Nhóm chat".
    /// - <c>displayAvatarUrl</c>: Ảnh hiển thị đã resolve. DIRECT = avatar user còn lại; GROUP = avatar group, fallback <c>null</c>.
    /// - <c>isPrivate</c>: <c>true</c> nếu là chat 1-1 (giữa 2 bạn bè), <c>false</c> nếu là nhóm nhiều người.
    /// - <c>lastMessageId</c>: Id của tin nhắn mới nhất trong group, <c>null</c> nếu group chưa có tin nhắn.
    /// - <c>lastSeenMessageId</c>: Id tin nhắn cuối cùng user hiện tại đã mark seen trong group.
    /// - <c>isSeenLatest</c>: <c>true</c> nếu user hiện tại đã seen tin nhắn mới nhất hoặc group chưa có tin nhắn.
    /// - <c>unreadCount</c>: Số tin nhắn chưa đọc của user hiện tại trong group.
    ///
    /// **Lưu ý:**
    /// - <c>lastMessageId</c>, <c>lastMessageContent</c>, <c>lastMessageAtUtc</c>, <c>lastMessageSenderFamilyName</c>, <c>lastMessageSenderGivenName</c> là <c>null</c> nếu group chưa có tin nhắn nào.
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
    /// <summary>Xem thông tin chi tiết nhóm chat kèm danh sách thành viên.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Trả về metadata của nhóm và toàn bộ danh sách thành viên, bao gồm <c>lastSeenMessageId</c>
    /// của từng thành viên trong nhóm.
    /// Chỉ thành viên của nhóm mới được xem.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm. Lấy từ <c>groupId</c> trong danh sách hội thoại
    ///   hoặc <c>messageGroupId</c> trong danh sách bạn bè.
    ///
    /// **Response khi thành công (HTTP 200) — ví dụ chat 1-1, UserA (Alice) đang xem:**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "type": 0,
    ///     "createdAtUtc": "2026-05-01T08:00:00Z",
    ///     "displayName": "Trần Bob",
    ///     "displayAvatarUrl": null,
    ///     "members": [
    ///       {
    ///         "userId": "aaaaaaaa-0000-0000-0000-000000000001",
    ///         "familyName": "Nguyễn",
    ///         "givenName": "Alice",
    ///         "avatarUrl": "https://cdn.example.com/avatars/alice.jpg",
    ///         "role": 1,
    ///         "lastSeenMessageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    ///       },
    ///       {
    ///         "userId": "bbbbbbbb-0000-0000-0000-000000000002",
    ///         "familyName": "Trần",
    ///         "givenName": "Bob",
    ///         "avatarUrl": null,
    ///         "role": 0,
    ///         "lastSeenMessageId": null
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Giải thích các trường:**
    /// - <c>displayName</c>: Tên hiển thị đã resolve. DIRECT = tên user còn lại; GROUP = tên group, fallback bằng tên một số thành viên, cuối cùng là "Nhóm chat".
    /// - <c>displayAvatarUrl</c>: Ảnh hiển thị đã resolve. DIRECT = avatar user còn lại; GROUP = avatar group, fallback <c>null</c>.
    /// - <c>isPrivate</c>: <c>true</c> nếu là chat 1-1 (giữa 2 bạn bè), <c>false</c> nếu là nhóm nhiều người.
    /// - <c>members</c>: Toàn bộ thành viên của nhóm, **bao gồm cả user hiện tại**.
    /// - <c>familyName</c> / <c>givenName</c>: Họ và tên của thành viên.
    /// - <c>avatarUrl</c>: Signed URL ảnh đại diện của thành viên, <c>null</c> nếu chưa có.
    /// - <c>role</c>: Vai trò trong nhóm — <c>0</c> = Member, <c>1</c> = Owner. Dùng để hiển thị icon Owner và ẩn/hiện nút quản trị.
    /// - <c>lastSeenMessageId</c>: Id tin nhắn cuối cùng thành viên đó đã đọc trong group, <c>null</c> nếu chưa mark seen.
    ///
    /// **Lưu ý:** Frontend nên dùng <c>displayName</c> / <c>displayAvatarUrl</c> để render header chat.
    /// Với chat 1-1, cả API list và detail đều trả <c>displayName</c>/<c>displayAvatarUrl</c> của user còn lại.
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200).
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên nhóm (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpGet("{groupId:guid}")]
    public async Task<IActionResult> GetDetail(Guid groupId, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetMessageGroupDetailQuery(groupId), ct));

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
    ///   "content": "Nội dung tin nhắn (tối đa 4000 ký tự)",
    ///   "clientMessageId": "550e8400-e29b-41d4-a716-446655440000"
    /// }
    /// </code>
    ///
    /// **Lưu ý <c>clientMessageId</c>:** Tuỳ chọn. Frontend tự sinh UUID cho mỗi lần gửi.
    /// Nếu request bị retry do mạng, server dedup theo <c>clientMessageId</c> và trả về tin nhắn
    /// đã tạo thay vì tạo lại. Bỏ qua hoặc truyền <c>null</c> nếu không cần idempotency.
    /// Sau khi tạo tin nhắn, server push realtime <c>ReceiveNewMessage</c> vào room <c>message_group:{groupId}</c>
    /// và room <c>user:{userId}</c> của toàn bộ thành viên trong group. Server cũng push
    /// <c>ReceiveUnreadMessageCount(groupId, unreadCount)</c> vào room user của từng thành viên.
    /// Tin nhắn mới được tự đánh dấu seen cho sender.
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
    ///     "senderFamilyName": "Nguyễn",
    ///     "senderGivenName": "Alice",
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
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: <c>groupId</c> không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: User không phải thành viên của nhóm này (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> Send(
        Guid groupId, [FromBody] SendMessageRequest req, CancellationToken ct)
        => CreatedResult($"api/v1/message-groups/{groupId}/messages",
            await mediator.Send(new SendMessageCommand(groupId, req.Content, req.ClientMessageId, req.PostId), ct));

    [HttpPost("messages")]
    public async Task<IActionResult> SendByPost(
        [FromBody] SendMessageRequest req, CancellationToken ct)
        => CreatedResult("api/v1/message-groups/messages",
            await mediator.Send(new SendMessageCommand(null, req.Content, req.ClientMessageId, req.PostId), ct));

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
    /// - <c>cursor</c> (long, tuỳ chọn): Sequence number của tin nhắn. Load các tin nhắn có sequence **nhỏ hơn** giá trị này — dùng để "load more / scroll lên trên".
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
    ///         "senderFamilyName": "Nguyễn",
    ///         "senderGivenName": "Alice",
    ///         "content": "Hẹn gặp nhé!",
    ///         "createdAtUtc": "2026-05-04T10:30:00Z"
    ///       }
    ///     ],
    ///     "meta": {
    ///       "nextCursor": 1234567890,
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
        Guid groupId, [FromQuery] long? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListMessagesQuery(groupId, cursor, limit), ct));

    #region
    /// <summary>Tạo nhóm chat mới.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Tạo nhóm chat nhiều người. Người tạo sẽ tự động trở thành Owner.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "name": "Tên nhóm (bắt buộc, tối đa 100 ký tự)",
    ///   "avatarMediaObjectId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Lưu ý:** <c>avatarMediaObjectId</c> là Id của media đã upload qua POST /api/v1/media, tuỳ chọn.
    ///
    /// **Response khi thành công (HTTP 201):**
    /// <code>
    /// {
    ///   "success": true,
    ///   "message": "...",
    ///   "code": null,
    ///   "data": {
    ///     "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///     "isPrivate": false,
    ///     "createdAtUtc": "2026-05-04T10:00:00Z",
    ///     "displayName": "Tên nhóm",
    ///     "displayAvatarUrl": null,
    ///     "members": [
    ///       {
    ///         "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "familyName": "Nguyễn",
    ///         "givenName": "Alice",
    ///         "avatarUrl": null,
    ///         "role": 1,
    ///         "lastSeenMessageId": null
    ///       }
    ///     ]
    ///   },
    ///   "errors": null
    /// }
    /// </code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 201).
    /// - <c>VALIDATION_ERROR</c>: <c>name</c> rỗng hoặc vượt quá 100 ký tự (HTTP 400).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest req, CancellationToken ct)
        => CreatedResult("api/v1/message-groups",
            await mediator.Send(new CreateGroupCommand(req.Name, req.AvatarMediaObjectId), ct));

    #region
    /// <summary>Thêm thành viên vào nhóm.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ Owner mới được thêm thành viên. Không thể thêm vào chat 1-1.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "targetUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>VALIDATION_ERROR</c>: Cố thêm vào chat 1-1 (HTTP 400).
    /// - <c>USER_NOT_FOUND</c>: <c>targetUserId</c> không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải Owner (HTTP 403).
    /// - <c>GROUP_MEMBER_ALREADY_EXISTS</c>: Người dùng đã là thành viên (HTTP 409).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPost("{groupId:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid groupId, [FromBody] AddGroupMemberRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(new AddGroupMemberCommand(groupId, req.TargetUserId), ct));

    #region
    /// <summary>Xóa thành viên khỏi nhóm (Owner only).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ Owner mới được xóa thành viên. Owner không thể tự xóa mình (dùng DELETE /members/me để rời nhóm).
    ///
    /// **Path params:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    /// - <c>userId</c> (guid, bắt buộc): Id của thành viên cần xóa.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>VALIDATION_ERROR</c>: Owner cố tự remove (HTTP 400).
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải Owner (HTTP 403).
    /// - <c>GROUP_MEMBER_NOT_FOUND</c>: <c>userId</c> không phải thành viên nhóm (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpDelete("{groupId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid groupId, Guid userId, CancellationToken ct)
        => HandleResult(await mediator.Send(new RemoveGroupMemberCommand(groupId, userId), ct));

    #region
    /// <summary>Rời khỏi nhóm.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Owner phải transfer ownership trước khi rời nếu còn thành viên khác (PUT /{groupId}/owner).
    /// Nếu là thành viên cuối cùng, nhóm sẽ bị xóa hoàn toàn.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm muốn rời.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>VALIDATION_ERROR</c>: Owner còn thành viên khác, chưa transfer ownership (HTTP 400).
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpDelete("{groupId:guid}/members/me")]
    public async Task<IActionResult> Leave(Guid groupId, CancellationToken ct)
        => HandleResult(await mediator.Send(new LeaveGroupCommand(groupId), ct));

    #region
    /// <summary>Transfer ownership cho thành viên khác.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ Owner hiện tại mới được transfer. Sau khi transfer, Owner cũ trở thành Member.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "newOwnerUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải Owner (HTTP 403).
    /// - <c>GROUP_MEMBER_NOT_FOUND</c>: <c>newOwnerUserId</c> không phải thành viên nhóm (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPut("{groupId:guid}/owner")]
    public async Task<IActionResult> TransferOwnership(
        Guid groupId, [FromBody] TransferOwnershipRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(new TransferOwnershipCommand(groupId, req.NewOwnerUserId), ct));

    #region
    /// <summary>Cập nhật tên/avatar nhóm (Owner only).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Chỉ Owner mới được cập nhật. Không áp dụng cho chat 1-1.
    /// Chỉ truyền field muốn cập nhật; field <c>null</c> sẽ bị bỏ qua (không xóa giá trị cũ).
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "name": "Tên nhóm mới (tuỳ chọn, tối đa 100 ký tự)",
    ///   "avatarMediaObjectId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>VALIDATION_ERROR</c>: Cố cập nhật chat 1-1, hoặc <c>name</c> rỗng/vượt 100 ký tự (HTTP 400).
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải Owner (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPatch("{groupId:guid}")]
    public async Task<IActionResult> Update(
        Guid groupId, [FromBody] UpdateGroupRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(new UpdateGroupCommand(groupId, req.Name, req.AvatarMediaObjectId), ct));

    #region
    /// <summary>Cập nhật trạng thái đang gõ trong nhóm chat.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Fire-and-forget — không lưu DB. Broadcast SignalR event <c>ReceiveTypingStatus(groupId, typingUserId, isTyping)</c>
    /// đến room <c>message_group:{groupId}</c>.
    /// Gọi khi user bắt đầu gõ (<c>isTyping: true</c>) và khi dừng gõ (<c>isTyping: false</c>).
    /// Frontend cũng có thể gửi typing trực tiếp qua SignalR bằng hub method
    /// <c>SendTypingStatus({ messageGroupId, isTyping })</c>; hub sẽ broadcast event này đến các connection khác
    /// trong cùng room <c>message_group:{groupId}</c>.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "isTyping": true
    /// }
    /// </code>
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên nhóm (HTTP 403).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPatch("{groupId:guid}/typing")]
    public async Task<IActionResult> UpdateTypingStatus(
        Guid groupId, [FromBody] UpdateTypingStatusRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new UpdateTypingStatusCommand(groupId, currentUser.UserId, req.IsTyping), ct));

    #region
    /// <summary>Đánh dấu đã đọc tin nhắn đến một vị trí nhất định trong nhóm.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Cập nhật <c>lastSeenMessageId</c> của thành viên, sau đó broadcast SignalR event
    /// <c>ReceiveMessageSeen(groupId, seenByUserId, lastSeenMessageId)</c> đến room <c>message_group:{groupId}</c>.
    /// Đồng thời emit <c>ReceiveMessageGroupSeen(groupId, lastSeenMessageId)</c> vào room cá nhân
    /// <c>user:{userId}</c> của user vừa mark seen để FE cập nhật trạng thái seen của group.
    /// Server cũng push <c>ReceiveUnreadMessageCount(groupId, unreadCount)</c> vào room <c>user:{userId}</c>
    /// của user vừa mark seen.
    /// Gọi khi user nhìn thấy tin nhắn mới nhất — truyền <c>id</c> của tin nhắn cuối cùng trong viewport.
    ///
    /// **Path param:**
    /// - <c>groupId</c> (guid, bắt buộc): Id của nhóm.
    ///
    /// **Request body:**
    /// <code>
    /// {
    ///   "lastSeenMessageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    /// }
    /// </code>
    ///
    /// **Lưu ý:** <c>lastSeenMessageId</c> là <c>id</c> (guid) của tin nhắn, lấy từ response của GET /{groupId}/messages hoặc POST /{groupId}/messages.
    ///
    /// **Response khi thành công (HTTP 200):**
    /// <code>{ "success": true, "message": "...", "code": null, "data": null, "errors": null }</code>
    ///
    /// **Các giá trị <c>code</c>:**
    /// - <c>null</c>: Thành công (HTTP 200), <c>data</c> là <c>null</c>.
    /// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
    /// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên nhóm (HTTP 403).
    /// - <c>MESSAGE_NOT_FOUND</c>: Tin nhắn không tồn tại trong nhóm (HTTP 404).
    /// - <c>401</c>: Token không hợp lệ hoặc hết hạn.
    /// </remarks>
    #endregion
    [HttpPatch("{groupId:guid}/seen")]
    public async Task<IActionResult> MarkMessagesSeen(
        Guid groupId, [FromBody] MarkGroupMessagesSeenRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new MarkGroupMessagesSeenCommand(groupId, currentUser.UserId, req.LastSeenMessageId), ct));
}
