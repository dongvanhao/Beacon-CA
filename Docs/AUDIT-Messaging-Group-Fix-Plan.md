# Messaging & Group Chat — Fix Plan

> Kết quả từ production-level review. Ưu tiên từ cao xuống thấp.
> Mỗi mục có: **Vấn đề**, **Hậu quả nếu không fix**, **Cách fix cụ thể**.

---

## MUST FIX NOW 🚨

---

### FIX-01 — Thêm `MessageGroupMemberSettings` table

**Vấn đề:**
`MessageGroup` chỉ có 1 `Name` và 1 `AvatarUrl` dùng chung cho tất cả members. Business requirement yêu cầu mỗi user có thể tự đặt nickname và avatar riêng cho đoạn chat (đặc biệt DM 1-1). Không có cơ chế track unread count.

**Hậu quả nếu không fix:**
- Per-user custom name/avatar không thể deliver — business requirement miss hoàn toàn.
- Unread count không thể implement.
- Read receipt không có chỗ lưu.

**Cách fix:**

**Bước 1 — Thêm Domain Entity**

Tạo file `src/Beacon.Domain/Entities/Messaging/MessageGroupMemberSetting.cs`:

```csharp
public class MessageGroupMemberSetting
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public string? CustomName { get; private set; }
    public string? CustomAvatarUrl { get; private set; }
    public bool IsMuted { get; private set; }
    public Guid? LastReadMessageId { get; private set; }
    public DateTime? LastReadAtUtc { get; private set; }

    private MessageGroupMemberSetting() { }

    public static MessageGroupMemberSetting Create(Guid groupId, Guid userId)
        => new() { GroupId = groupId, UserId = userId };

    public void UpdateCustomName(string? name) => CustomName = name;
    public void UpdateCustomAvatarUrl(string? url) => CustomAvatarUrl = url;
    public void SetMuted(bool muted) => IsMuted = muted;

    public void MarkRead(Guid messageId, DateTime readAtUtc)
    {
        LastReadMessageId = messageId;
        LastReadAtUtc = readAtUtc;
    }
}
```

**Bước 2 — EF Configuration**

Tạo `src/Beacon.Infrashtructure/Presistence/Configuration/Messaging/MessageGroupMemberSettingConfiguration.cs`:

```csharp
public class MessageGroupMemberSettingConfiguration : IEntityTypeConfiguration<MessageGroupMemberSetting>
{
    public void Configure(EntityTypeBuilder<MessageGroupMemberSetting> b)
    {
        b.ToTable("MessageGroupMemberSettings");
        b.HasKey(x => new { x.GroupId, x.UserId });

        b.Property(x => x.CustomName).HasMaxLength(100);
        b.Property(x => x.CustomAvatarUrl).HasMaxLength(500);

        b.HasOne<MessageGroup>()
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Message>()
            .WithMany()
            .HasForeignKey(x => x.LastReadMessageId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
```

**Bước 3 — Thêm DbSet vào AppDbContext**

```csharp
public DbSet<MessageGroupMemberSetting> MessageGroupMemberSettings => Set<MessageGroupMemberSetting>();
```

**Bước 4 — Tạo migration**

```bash
dotnet ef migrations add Add_MessageGroupMemberSettings \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

**Bước 5 — Cập nhật `AcceptFriendRequestCommandHandler`**

Khi tạo DM group, tạo luôn `MessageGroupMemberSetting` cho cả 2 user:

```csharp
var settingA = MessageGroupMemberSetting.Create(group.Id, request.SenderId);
var settingB = MessageGroupMemberSetting.Create(group.Id, currentUserId);
await _settingRepo.AddAsync(settingA, ct);
await _settingRepo.AddAsync(settingB, ct);
```

**Bước 6 — Cập nhật `GetMessageGroupDetailQueryHandler`**

Khi resolve display name/avatar, đọc từ `MessageGroupMemberSetting.CustomName` của caller trước, fallback về peer name nếu null.

---

### FIX-02 — Message soft delete + idempotency + sequence

**Vấn đề:**
- `Message` không có `IsDeleted` → "Thu hồi tin nhắn" không implement được.
- Không có `ClientMessageId` → mobile retry gây tin nhắn trùng.
- Không có `SequenceNumber` → với clock skew trên nhiều server, thứ tự tin nhắn không deterministic.

**Hậu quả nếu không fix:**
- User xóa tin nhắn = hard delete, tất cả member trong group không thấy gì, không có "Tin nhắn đã bị thu hồi".
- Mobile mất mạng retry → user nhận 2-3 tin nhắn giống nhau.
- Khi scale nhiều server SignalR, tin nhắn hiển thị sai thứ tự.

**Cách fix:**

**Bước 1 — Cập nhật `Message` entity**

```csharp
public class Message : BaseEntity
{
    public Guid GroupId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    // Thêm các field sau
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public DateTime? EditedAtUtc { get; private set; }
    public string? ClientMessageId { get; private set; }
    public Guid? ReplyToMessageId { get; private set; }

    public static Message Create(
        Guid groupId, Guid senderId, string content, string? clientMessageId = null)
        => new()
        {
            GroupId = groupId,
            SenderId = senderId,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            ClientMessageId = clientMessageId
        };

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        Content = string.Empty; // clear content khi thu hồi
    }

    public void Edit(string newContent)
    {
        Content = newContent;
        EditedAtUtc = DateTime.UtcNow;
    }
}
```

**Bước 2 — Cập nhật `MessageConfiguration`**

```csharp
// Thêm vào Configure()
b.Property(x => x.IsDeleted).HasDefaultValue(false);
b.Property(x => x.ClientMessageId).HasMaxLength(100);

b.HasIndex(x => new { x.GroupId, x.ClientMessageId })
    .IsUnique()
    .HasFilter("[ClientMessageId] IS NOT NULL");

b.HasOne<Message>()
    .WithMany()
    .HasForeignKey(x => x.ReplyToMessageId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);
```

**Bước 3 — Cập nhật `SendMessageCommand`**

```csharp
public record SendMessageCommand(
    Guid GroupId,
    string Content,
    string? ClientMessageId   // thêm field này
) : IRequest<Result<MessageDto>>;
```

**Bước 4 — Idempotency check trong handler**

```csharp
// SendMessageCommandHandler
if (request.ClientMessageId is not null)
{
    var existing = await _messageRepo.GetByClientMessageIdAsync(
        request.GroupId, request.ClientMessageId, ct);
    if (existing is not null)
        return Result.Success(_mapper.ToDto(existing)); // trả về message cũ, không tạo mới
}
```

**Bước 5 — Tạo migration**

```bash
dotnet ef migrations add Add_Message_SoftDelete_Idempotency \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

---

### FIX-03 — `RemoveFriend` phải soft-delete MessageGroup

**Vấn đề:**
`RemoveFriendCommandHandler` remove cả 2 members khỏi MessageGroup nhưng không soft-delete group. MessageGroup vẫn tồn tại trong DB với 0 members — empty ghost record.

**Hậu quả:**
- Data inconsistency: group không có member nhưng vẫn tồn tại.
- `ListByUserAsync` có thể return empty group nếu query filter không bắt được.
- Sau này nếu user kết bạn lại, hệ thống có thể tạo group mới trong khi group cũ vẫn còn.

**Cách fix:**

Trong `RemoveFriendCommandHandler`, sau khi remove members, soft-delete group:

```csharp
// Thêm vào handler sau RemoveMemberAsync calls
var group = await _messageGroupRepo.GetByIdAsync(friend.MessageGroupId, ct);
if (group is not null)
    group.Delete(); // gọi method soft-delete có sẵn

await _friendRepo.DeleteAsync(friend);
await _friendRepo.SaveChangesAsync(ct); // 1 SaveChanges bao tất cả
```

Đảm bảo `MessageGroup.Delete()` set `IsDeleted = true` và `DeletedAtUtc = DateTime.UtcNow`.

> **Lưu ý:** Nếu sau này 2 người kết bạn lại, `AcceptFriendRequestCommandHandler` phải tạo MessageGroup mới thay vì reuse group cũ đã bị soft-delete.

---

### FIX-04 — Tách cross-domain coupling trong `AcceptFriendRequest`

**Vấn đề:**
`AcceptFriendRequestCommandHandler` inject cả `IFriendRepository`, `IFriendRequestRepository`, và `IMessageGroupRepository` — 1 handler thực hiện mutation trên 2 domain (Group + Messaging) cùng lúc. Vi phạm Single Responsibility và tạo tight coupling.

**Hậu quả:**
- Không thể test riêng từng domain.
- Khi thêm logic vào một trong 2 domain (vd: notification khi tạo DM group), phải sửa handler này.
- Cản trở SignalR event flow về sau.

**Cách fix — Domain Events:**

**Bước 1 — Tạo Domain Event**

```csharp
// src/Beacon.Domain/Events/FriendRequestAcceptedEvent.cs
public record FriendRequestAcceptedEvent(
    Guid FriendRequestId,
    Guid SenderId,
    Guid ReceiverId,
    Guid FriendId
) : INotification; // MediatR INotification
```

**Bước 2 — Đơn giản hóa `AcceptFriendRequestCommandHandler`**

```csharp
// Handler chỉ xử lý Group domain
var friend = Friend.Create(request.SenderId, currentUserId);
var success = await _friendRepo.TryAddAsync(friend, ct);
if (!success)
    return Result.Failure<Unit>(Error.Conflict(...));

friendRequest.Accept();
await _friendRequestRepo.SaveChangesAsync(ct);

// Publish event — Messaging domain tự xử lý
await _mediator.Publish(new FriendRequestAcceptedEvent(
    request.Id, request.SenderId, currentUserId, friend.Id), ct);

return Result.Success(Unit.Value);
```

**Bước 3 — Tạo Event Handler trong Messaging domain**

```csharp
// src/Beacon.Application/Features/Messaging/EventHandlers/CreateDirectMessageGroupHandler.cs
public class CreateDirectMessageGroupHandler(IMessageGroupRepository repo)
    : INotificationHandler<FriendRequestAcceptedEvent>
{
    public async Task Handle(FriendRequestAcceptedEvent ev, CancellationToken ct)
    {
        var group = MessageGroup.CreatePrivate();
        group.AddMember(ev.SenderId, GroupMemberRole.Member);
        group.AddMember(ev.ReceiverId, GroupMemberRole.Member);
        await repo.AddAsync(group, ct);
        await repo.SaveChangesAsync(ct);
    }
}
```

> **Lưu ý:** Sau khi tách, `AcceptFriendRequestCommand` không còn trả về `MessageGroupDetailDto`. Cần review controller response.

---

### FIX-05 — Fix race condition trong `AcceptFriendRequest`

**Vấn đề:**
Thứ tự hiện tại trong handler:
1. Đọc FriendRequest (no lock)
2. Validate
3. Create Friend (TryAdd — có unique constraint)

Giữa step 1 và step 3 có window cho 2 request đồng thời vượt qua validation. `TryAddAsync` chặn được duplicate Friend, nhưng MessageGroup có thể được tạo 2 lần.

**Cách fix:**

Dùng `UPDLOCK` hint khi read FriendRequest để lock row:

```csharp
// FriendRequestRepository
public async Task<FriendRequest?> GetByIdForUpdateAsync(Guid id, CancellationToken ct)
    => await db.FriendRequests
        .FromSqlRaw("SELECT * FROM FriendRequests WITH (UPDLOCK, ROWLOCK) WHERE Id = {0}", id)
        .FirstOrDefaultAsync(ct);
```

Hoặc đơn giản hơn, dùng optimistic concurrency với `RowVersion`:

```csharp
// FriendRequest entity
[Timestamp]
public byte[] RowVersion { get; private set; } = null!;
// EF tự throw DbUpdateConcurrencyException nếu 2 request update cùng lúc
```

```csharp
// EF Configuration
b.Property(x => x.RowVersion).IsRowVersion();
```

Handler catch `DbUpdateConcurrencyException` và trả về conflict error.

---

## SHOULD FIX ⚡

---

### FIX-06 — Bỏ `Friend.MessageGroupId` FK — giải quyết cross-domain coupling

**Vấn đề:**
`Friend` entity (Group domain) chứa FK đến `MessageGroup` (Messaging domain) — bidirectional coupling ở tầng Domain.

**Cách fix:**

**Bước 1 — Xóa `MessageGroupId` khỏi `Friend` entity**

```csharp
// Xóa
public Guid MessageGroupId { get; private set; }
public MessageGroup MessageGroup { get; private set; } = null!;
```

**Bước 2 — Thêm method lookup vào `IMessageGroupRepository`**

```csharp
// Tìm DM group giữa 2 user
Task<MessageGroup?> GetPrivateGroupBetweenAsync(
    Guid userId1, Guid userId2, CancellationToken ct = default);
```

**Bước 3 — Implement trong `MessageGroupRepository`**

```csharp
public Task<MessageGroup?> GetPrivateGroupBetweenAsync(Guid userId1, Guid userId2, CancellationToken ct)
    => db.MessageGroups
        .Where(g => g.IsPrivate
            && g.Members.Any(m => m.UserId == userId1)
            && g.Members.Any(m => m.UserId == userId2))
        .FirstOrDefaultAsync(ct);
```

**Bước 4 — Cập nhật các handler đang dùng `friend.MessageGroupId`**

Tìm và replace bằng `GetPrivateGroupBetweenAsync(userId1, userId2)`.

**Bước 5 — Tạo migration xóa column**

```bash
dotnet ef migrations add Remove_Friend_MessageGroupId \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

> **Review migration file trước khi apply** — đảm bảo không có DROP COLUMN ngoài ý muốn.

---

### FIX-07 — Thêm `SequenceNumber` vào `Message` để đảm bảo ordering

**Vấn đề:**
`CreatedAtUtc` làm ordering key sẽ bị clock skew khi nhiều server — 2 messages gửi trong cùng 1ms sẽ có thứ tự không ổn định.

**Cách fix:**

Thêm `SequenceNumber` dạng `BIGINT IDENTITY` tại DB level:

```csharp
// Message entity
public long SequenceNumber { get; private set; } // DB-generated, không set trong code
```

```csharp
// MessageConfiguration
b.Property(x => x.SequenceNumber)
    .ValueGeneratedOnAdd()
    .UseIdentityColumn(); // SQL Server IDENTITY

b.HasIndex(x => new { x.GroupId, x.SequenceNumber });
```

Cập nhật `ListByGroupAsync` dùng `SequenceNumber` thay `CreatedAtUtc` để sort:

```csharp
.OrderByDescending(m => m.SequenceNumber)
.Where(m => cursor == null || m.SequenceNumber < cursor)
```

---

### FIX-08 — Rename `FindUserByPhoneQuery` → `SearchUsersQuery`

**Vấn đề:**
Query tên là `FindUserByPhone` nhưng thực tế search cả name và phone. Misleading cho developer mới.

**Cách fix:**

Rename toàn bộ:
- `FindUserByPhoneQuery` → `SearchUsersQuery`
- `FindUserByPhoneQueryHandler` → `SearchUsersQueryHandler`
- `FindUserByPhoneQueryHandler.cs` → `SearchUsersQueryHandler.cs`
- Cập nhật controller injection

---

### FIX-09 — Thêm `GetGroupIdsByUserAsync` vào `IMessageGroupRepository`

**Vấn đề:**
Khi SignalR connect, cần add user vào tất cả Hub groups của user đó. Hiện tại không có method lấy danh sách groupId mà user thuộc về.

**Cách fix:**

```csharp
// IMessageGroupRepository
Task<List<Guid>> GetGroupIdsByUserAsync(Guid userId, CancellationToken ct = default);
```

```csharp
// MessageGroupRepository
public Task<List<Guid>> GetGroupIdsByUserAsync(Guid userId, CancellationToken ct)
    => db.MessageGroupMembers
        .Where(m => m.UserId == userId)
        .Select(m => m.GroupId)
        .ToListAsync(ct);
```

---

### FIX-10 — Validate message gửi vào group đã soft-delete

**Vấn đề:**
`SendMessageCommandHandler` chỉ check `IsMemberAsync` — nếu `IsMemberAsync` query trực tiếp `MessageGroupMembers` mà không join với `MessageGroups`, user có thể gửi message vào group đã bị xóa.

**Cách fix:**

Thêm check group còn tồn tại không (query filter tự lọc soft-deleted):

```csharp
// SendMessageCommandHandler
var group = await _messageGroupRepo.GetByIdAsync(request.GroupId, ct);
if (group is null)
    return Result.Failure<MessageDto>(Error.NotFound(ErrorCodes.GROUP_NOT_FOUND, "Group not found"));

var isMember = group.Members.Any(m => m.UserId == currentUserId);
// hoặc dùng IsMemberAsync nếu không eager load members
```

---

## NICE TO HAVE 📝

---

### FIX-11 — Thêm `InvitedByUserId` vào `MessageGroupMember`

**Mục đích:** Audit trail — biết ai đã add member này vào group.

```csharp
// MessageGroupMember
public Guid? InvitedByUserId { get; private set; }
```

```csharp
// MessageGroupMemberConfiguration
b.Property(x => x.InvitedByUserId).IsRequired(false);
b.HasOne<User>()
    .WithMany()
    .HasForeignKey(x => x.InvitedByUserId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);
```

---

### FIX-12 — `MessageGroup.AvatarUrl` → FK đến `MediaObject`

**Vấn đề:**
`User` entity có FK proper đến `MediaObject`, nhưng `MessageGroup.AvatarUrl` là raw string. Inconsistency trong design — MediaObject lifecycle không được manage.

**Cách fix:**

```csharp
// MessageGroup
public Guid? AvatarMediaObjectId { get; private set; }
public MediaObject? AvatarMedia { get; private set; }
// Xóa: public string? AvatarUrl { get; private set; }
```

```csharp
// MessageGroupConfiguration
b.HasOne(x => x.AvatarMedia)
    .WithMany()
    .HasForeignKey(x => x.AvatarMediaObjectId)
    .OnDelete(DeleteBehavior.SetNull)
    .IsRequired(false);
```

---

### FIX-13 — Normalize `FriendRequest` pair để thêm unique DB constraint

**Mục đích:** Thêm unique constraint tại DB level để chặn duplicate requests ngay cả khi có race condition ở application level.

**Cách fix:**

```csharp
// FriendRequest entity — store normalized pair + initiator
public Guid UserId1 { get; private set; }  // always < UserId2
public Guid UserId2 { get; private set; }  // always > UserId1
public Guid InitiatorId { get; private set; } // người gửi thật sự

public static FriendRequest Create(Guid senderId, Guid receiverId)
{
    var (id1, id2) = senderId < receiverId ? (senderId, receiverId) : (receiverId, senderId);
    return new FriendRequest
    {
        UserId1 = id1,
        UserId2 = id2,
        InitiatorId = senderId,
        Status = FriendRequestStatus.Pending
    };
}
```

```csharp
// FriendRequestConfiguration
b.HasIndex(x => new { x.UserId1, x.UserId2 })
    .HasFilter("[Status] = 0") // 0 = Pending
    .IsUnique();
```

---

## POSTPONE ⏳

Các feature sau **không nên implement ngay** — chờ core messaging ổn định:

| Feature | Lý do postpone |
|---|---|
| Reply message (`ReplyToMessageId`) | Column đã thêm ở FIX-02, logic implement sau |
| Pinned message | V2 feature, cần thêm table riêng |
| Message edit history | Advanced, cần audit log table |
| Attachment table | Phụ thuộc vào file upload flow cho messages |
| Block user | Cần separate `BlockList` table + logic filter toàn system |
| Typing indicator | Ephemeral — dùng Redis TTL, không cần DB |
| Online presence / last seen | Dùng Redis, không cần DB |

---

## Checklist theo thứ tự thực hiện

```
SPRINT 1 — Core Data Model
[ ] FIX-02: Message soft delete + idempotency + sequence (migration)
[ ] FIX-01: MessageGroupMemberSettings table (migration)
[ ] FIX-03: RemoveFriend soft-delete MessageGroup
[ ] FIX-10: Validate message gửi vào group đã xóa

SPRINT 2 — Architecture Cleanup
[ ] FIX-04: Tách AcceptFriendRequest thành Domain Events
[ ] FIX-06: Bỏ Friend.MessageGroupId FK
[ ] FIX-05: Fix race condition với RowVersion

SPRINT 3 — SignalR Readiness
[ ] FIX-07: SequenceNumber cho Message ordering
[ ] FIX-09: GetGroupIdsByUserAsync cho SignalR Hub OnConnected
[ ] FIX-08: Rename FindUserByPhoneQuery

SPRINT 4 — Nice to have
[ ] FIX-11: InvitedByUserId trong MessageGroupMember
[ ] FIX-12: MessageGroup.AvatarUrl → FK MediaObject
[ ] FIX-13: Normalize FriendRequest pair
```

---

> Tạo ngày 2026-05-09. Review bởi Senior Architect audit session.
