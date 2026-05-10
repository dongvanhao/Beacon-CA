# Plan: Realtime & FCM — Beacon

**Module**: Messaging, Group, Notification, Identity  
**Phạm vi**: 6 Phase, 18 Slices  
**Source Spec**: `docs/specs/realtime-fcm-spec.md`

> Thứ tự: Foundation → Non-breaking slices trước → Breaking migration sau → FCM cuối.
> Mỗi slice phải pass `dotnet build` + test trước khi qua slice tiếp theo.

---

## Phase 1 — SignalR Foundation (Non-breaking)

> Không thay đổi schema, không break FE. Implement lifecycle Hub và extract NotificationService.

---

### Slice 1.1 — BeaconHub Lifecycle (OnConnected / OnDisconnected)

**Module**: API (Hub)  
**Type**: Infrastructure  
**Dependencies**: Không có  

**Mục tiêu**: Hub hiện trống 10 dòng. Thêm connection lifecycle để user tự join personal room `user:{userId}` khi connect.

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Hub/BeaconHubConnectionTests.cs`

```csharp
// Test OnConnectedAsync: user join đúng room user:{userId}
// Test OnDisconnectedAsync: log không throw
// Dùng mock IHubCallerClients + HubCallerContext
```

Cases:
- `OnConnectedAsync_ShouldAddToPersonalRoom_WhenTokenValid`
- `OnConnectedAsync_ShouldSetUserIdentifier_FromNameIdentifierClaim`

#### Bước 2 — Verify JwtService Claim

File: `src/Beacon.Infrashtructure/Services/JwtService.cs`

Verify JwtService đang set `ClaimTypes.NameIdentifier = userId` — đây là claim SignalR dùng để map `Context.UserIdentifier`.  
**Nếu sai claim** → fix tại JwtService, không sửa Hub.

#### Bước 3 — Update BeaconHub

File: `src/Beacon.Api/Hubs/BeaconHub.cs`

```csharp
[Authorize]
public class BeaconHub : Hub<IBeaconHub>
{
    private readonly ILogger<BeaconHub> _logger;
    
    public BeaconHub(ILogger<BeaconHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is null)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        _logger.LogInformation("Hub connected: userId={UserId}, connId={ConnId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation("Hub disconnected: userId={UserId}, connId={ConnId}, error={Error}",
            userId, Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }
}
```

#### Bước 4 — Update IBeaconHub (thêm error event)

File: `src/Beacon.Application/Common/Interfaces/IHubs/IBeaconHub.cs`

```csharp
Task ReceiveNotification(NotificationPayload payload);
Task ReceiveNewMessage(object messageDto);
Task ReceiveTypingStatus(Guid groupId, Guid typingUserId, bool isTyping);
Task ReceiveMessageSeen(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId);
Task ReceiveError(string code, string message);   // NEW — dùng cho Hub method errors
```

#### Files Update
- `src/Beacon.Api/Hubs/BeaconHub.cs`
- `src/Beacon.Application/Common/Interfaces/IHubs/IBeaconHub.cs`

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] SignalR connect với valid JWT → log "Hub connected: userId=..."
- [ ] `Context.UserIdentifier` không null sau khi connect

---

### Slice 1.2 — Extract INotificationService

**Module**: Application → Infrastructure  
**Type**: Refactor (non-breaking)  
**Dependencies**: Slice 1.1  

**Mục tiêu**: Pattern `tạo Notification → SaveChanges → NotifyUserAsync` đang bị copy trong `AcceptFriendRequestCommandHandler`. Extract thành `INotificationService` để tái dụng và chuẩn bị hook FCM sau.

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Notification/NotificationServiceTests.cs`

Cases:
- `CreateAndDeliver_ShouldSaveNotificationToDb_Always`
- `CreateAndDeliver_ShouldEmitSignalR_AfterDbCommit`
- `CreateAndDeliver_ShouldNotThrow_WhenSignalRFails`  
- `CreateAndDeliver_ShouldNotRollbackNotification_WhenSignalRFails`

#### Bước 2 — Interface

File: `src/Beacon.Application/Common/Interfaces/IService/INotificationService.cs`

```csharp
public interface INotificationService
{
    Task CreateAndDeliverAsync(
        Guid receiverUserId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        CancellationToken ct = default);
}
```

#### Bước 3 — Implementation

File: `src/Beacon.Infrashtructure/Services/NotificationService.cs`

```csharp
public class NotificationService(
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAndDeliverAsync(
        Guid receiverUserId, NotificationType type,
        string title, string body, string? data = null, CancellationToken ct = default)
    {
        // SYNCHRONOUS — phải commit trước
        var notification = Notification.Create(receiverUserId, type, title, body, data);
        await notifRepo.AddAsync(notification, ct);
        await notifRepo.SaveChangesAsync(ct);

        // POST-COMMIT — không được throw, không được rollback
        var payload = new NotificationPayload(notification.Id, type.ToString(), title, body, data);
        try
        {
            await notifier.NotifyUserAsync(receiverUserId, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR emit failed for user {UserId}", receiverUserId);
        }
        // FCM sẽ được inject ở Phase 5
    }
}
```

#### Bước 4 — Đăng ký DI

File: `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

```csharp
services.AddScoped<INotificationService, NotificationService>();
```

#### Bước 5 — Refactor AcceptFriendRequestCommandHandler

File: `src/Beacon.Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommandHandler.cs`

Replace manual pattern:
```csharp
// BEFORE (xóa)
var notification = Notification.Create(...);
await notifRepo.AddAsync(notification, ct);
await notifRepo.SaveChangesAsync(ct);
await notifier.NotifyUserAsync(...);

// AFTER
await _notificationService.CreateAndDeliverAsync(
    request.InitiatorId,
    NotificationType.FriendAccepted,
    "Lời mời kết bạn đã được chấp nhận",
    $"{accepterName} đã chấp nhận lời mời kết bạn của bạn",
    ct: ct);
```

Remove `INotificationRepository` và `IRealtimeNotifier` khỏi handler — inject `INotificationService` thay.

#### Files Update
- `src/Beacon.Application/Common/Interfaces/IService/INotificationService.cs` (mới)
- `src/Beacon.Infrashtructure/Services/NotificationService.cs` (mới)
- `src/Beacon.Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommandHandler.cs`
- `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] Unit tests GREEN — bao gồm case SignalR fail không throw
- [ ] AcceptFriendRequest flow vẫn hoạt động (integration test pass)

---

## ✅ Checkpoint Phase 1

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN
- [ ] SignalR connect → log đúng userId

---

## Phase 2 — MessageGroup Schema Migration (Breaking)

> Đây là breaking change — cần sync FE. Thực hiện theo thứ tự nghiêm ngặt để backward compatible tối đa.

---

### Slice 2.1 — Domain: MessageGroupType Enum + DirectKey

**Module**: Domain, Application  
**Type**: Entity Update  
**Dependencies**: Phase 1 complete  

**Mục tiêu**: Thêm `Type` enum và `DirectKey` vào `MessageGroup`. Đây là step domain trước khi có migration.

#### Bước 1 — Enum

File: `src/Beacon.Domain/Enums/Messaging/MessageGroupType.cs`

```csharp
public enum MessageGroupType
{
    Direct = 0,  // Chat 1-1 giữa 2 user
    Group  = 1   // Chat nhóm nhiều người
}
```

#### Bước 2 — Update MessageGroup Entity

File: `src/Beacon.Domain/Entities/Messaging/MessageGroup.cs`

```csharp
public class MessageGroup : BaseEntity
{
    public MessageGroupType Type { get; set; }   // THAY THẾ IsPrivate
    public string? DirectKey { get; set; }        // unique index — chỉ cho Direct
    public string? Name { get; set; }
    public Guid? AvatarMediaObjectId { get; set; }
    public MediaObject? AvatarMedia { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public ICollection<MessageGroupMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];

    // Helper factory methods
    public static string BuildDirectKey(Guid userA, Guid userB)
    {
        var ids = new[] { userA, userB }.OrderBy(id => id).ToArray();
        return $"{ids[0]}_{ids[1]}";
    }

    public void Delete() { IsDeleted = true; DeletedAtUtc = DateTime.UtcNow; }
}
```

**Ghi chú**: Giữ lại `IsPrivate` trong DB một thời gian (data migration), nhưng remove khỏi entity — EF Migration sẽ drop column sau khi data đã migrate.

#### Bước 3 — Update DTOs

Files:
- `src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDto.cs`
- `src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDetailDto.cs`
- `src/Beacon.Domain/IRepository/Messaging/IMessageGroupRepository.cs` — `MessageGroupSummary` record

Replace `bool IsPrivate` → `MessageGroupType Type` trong tất cả DTOs và records.

#### Bước 4 — Update IMessageGroupRepository

File: `src/Beacon.Domain/IRepository/Messaging/IMessageGroupRepository.cs`

```csharp
public record MessageGroupSummary(
    Guid GroupId,
    MessageGroupType Type,    // THAY IsPrivate
    string? DirectKey,
    DateTime CreatedAtUtc,
    string? LastMessageContent,
    DateTime? LastMessageAtUtc,
    string? LastMessageSenderFamilyName,
    string? LastMessageSenderGivenName,
    string? DisplayName,
    string? AvatarObjectKey);

// Thêm method mới:
Task<MessageGroup?> GetByDirectKeyAsync(string directKey, CancellationToken ct);
```

#### Files Update
- `src/Beacon.Domain/Enums/Messaging/MessageGroupType.cs` (mới)
- `src/Beacon.Domain/Entities/Messaging/MessageGroup.cs`
- `src/Beacon.Domain/IRepository/Messaging/IMessageGroupRepository.cs`
- `src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDto.cs`
- `src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDetailDto.cs`

---

### Slice 2.2 — EF Migration: Add Type + DirectKey

**Module**: Infrastructure  
**Type**: Migration  
**Dependencies**: Slice 2.1  

#### Bước 1 — EF Config Update

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Messaging/MessageGroupConfiguration.cs`

```csharp
public class MessageGroupConfiguration : IEntityTypeConfiguration<MessageGroup>
{
    public void Configure(EntityTypeBuilder<MessageGroup> b)
    {
        b.ToTable("MessageGroups");
        b.Property(g => g.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(MessageGroupType.Direct);

        b.Property(g => g.DirectKey)
            .HasMaxLength(100)
            .IsRequired(false);

        // Unique partial index: DirectKey chỉ unique khi không null
        b.HasIndex(g => g.DirectKey)
            .IsUnique()
            .HasFilter("[DirectKey] IS NOT NULL")
            .HasDatabaseName("UX_MessageGroups_DirectKey");
    }
}
```

#### Bước 2 — Tạo Migration

```bash
dotnet ef migrations add Add_MessageGroup_Type_DirectKey \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

#### Bước 3 — Review và Edit Migration

Migration Up() phải:
1. Thêm column `Type` với default `'Direct'`
2. Thêm column `DirectKey` nullable
3. Data migration: `UPDATE MessageGroups SET Type = CASE WHEN IsPrivate = 1 THEN 'Direct' ELSE 'Group' END`
4. Tạo unique partial index trên `DirectKey`

```csharp
// Trong migration Up():
migrationBuilder.AddColumn<string>("Type", "MessageGroups", maxLength: 20, defaultValue: "Direct");
migrationBuilder.AddColumn<string?>("DirectKey", "MessageGroups", maxLength: 100, nullable: true);

// Data migration
migrationBuilder.Sql(@"
    UPDATE MessageGroups
    SET Type = CASE WHEN IsPrivate = 1 THEN 'Direct' ELSE 'Group' END
");

migrationBuilder.CreateIndex(
    "UX_MessageGroups_DirectKey", "MessageGroups", "DirectKey",
    unique: true, filter: "[DirectKey] IS NOT NULL");
```

> **KHÔNG drop IsPrivate trong migration này** — backward compatible. Drop ở migration riêng sau 1 sprint.

---

### Slice 2.3 — Update Repository + Handlers cho Direct/Group Logic

**Module**: Infrastructure, Application  
**Type**: Business Logic Update  
**Dependencies**: Slice 2.2  

**Mục tiêu**: Update tất cả handlers dùng `IsPrivate` sang `Type`. Implement `DirectKey` logic trong `CreateDirectMessageGroupHandler`.

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Messaging/CreateDirectMessageGroupHandlerTests.cs`

Cases:
- `Handle_ShouldCreateDirectGroup_WithCorrectDirectKey`
- `Handle_ShouldReturnExistingGroup_WhenDirectKeyAlreadyExists`

File: `tests/Beacon.UnitTests/Messaging/CreateGroupCommandHandlerTests.cs`

Cases:
- `Handle_ShouldSetTypeGroup_WhenCreatingMultiPersonGroup`

#### Bước 2 — Update CreateDirectMessageGroupHandler

File: `src/Beacon.Application/Features/Messaging/EventHandlers/CreateDirectMessageGroupHandler.cs`

```csharp
public async Task Handle(FriendRequestAcceptedEvent ev, CancellationToken ct)
{
    var directKey = MessageGroup.BuildDirectKey(ev.SenderId, ev.ReceiverId);

    // Idempotency: check DirectKey trước khi tạo
    var existing = await groupRepo.GetByDirectKeyAsync(directKey, ct);
    if (existing is not null) return;  // Đã tồn tại — skip

    var group = new MessageGroup
    {
        Type = MessageGroupType.Direct,
        DirectKey = directKey,
        CreatedAtUtc = DateTime.UtcNow
    };
    // ... thêm members, settings như cũ
}
```

#### Bước 3 — Update CreateGroupCommandHandler

File: `src/Beacon.Application/Features/Messaging/Commands/CreateGroup/CreateGroupCommandHandler.cs`

```csharp
var group = new MessageGroup
{
    Type = MessageGroupType.Group,   // THAY IsPrivate = false
    Name = command.Name,
    // ...
};
```

#### Bước 4 — Update Repository Implementation

File: `src/Beacon.Infrashtructure/Repository/Messaging/MessageGroupRepository.cs`

Thêm `GetByDirectKeyAsync()`:
```csharp
public Task<MessageGroup?> GetByDirectKeyAsync(string directKey, CancellationToken ct)
    => db.MessageGroups.FirstOrDefaultAsync(g => g.DirectKey == directKey, ct);
```

Update `GetPrivateGroupBetweenAsync()` → dùng `DirectKey` thay scan members:
```csharp
public Task<MessageGroup?> GetPrivateGroupBetweenAsync(Guid userId1, Guid userId2, CancellationToken ct)
{
    var directKey = MessageGroup.BuildDirectKey(userId1, userId2);
    return db.MessageGroups.FirstOrDefaultAsync(g => g.DirectKey == directKey, ct);
}
```

#### Bước 5 — Update displayName Logic

Files: `MessageGroupMapper`, `ListMyMessageGroupsQueryHandler`, `GetMessageGroupDetailQueryHandler`

```csharp
// Thay điều kiện IsPrivate bằng Type
if (summary.Type == MessageGroupType.Direct)
{
    displayName = peerUser?.FullName ?? "Người dùng";
    displayAvatarUrl = peerUser?.AvatarUrl;
}
else  // Group
{
    displayName = summary.Name ?? BuildGroupFallbackName(members);
    displayAvatarUrl = summary.AvatarUrl;
}
```

#### Files Update
- `src/Beacon.Application/Features/Messaging/EventHandlers/CreateDirectMessageGroupHandler.cs`
- `src/Beacon.Application/Features/Messaging/Commands/CreateGroup/CreateGroupCommandHandler.cs`
- `src/Beacon.Infrashtructure/Repository/Messaging/MessageGroupRepository.cs`
- Mapper + Handler files dùng `IsPrivate`

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] Unit test `CreateDirectMessageGroupHandler` — DirectKey đúng format
- [ ] Integration test: tạo 2 friend request giữa cùng 2 user → chỉ 1 group được tạo (DirectKey unique)
- [ ] `GET /api/v1/message-groups` trả `type: "Direct"` thay `isPrivate: true`

---

## ✅ Checkpoint Phase 2

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test` — GREEN
- [ ] Migration apply sạch: `dotnet ef database update`
- [ ] DB có column `Type` + `DirectKey` + unique index `UX_MessageGroups_DirectKey`
- [ ] Existing data đã migrate đúng (kiểm tra bằng SQL SELECT)
- [ ] FE đã cập nhật handle `type` field thay `isPrivate`

---

## Phase 3 — Message Group Socket Events

> Implement `JoinMessageGroup` hub method và switch broadcast sang Group rooms.

---

### Slice 3.1 — JoinMessageGroup Hub Method

**Module**: API (Hub), Application  
**Type**: New Feature  
**Dependencies**: Slice 1.1, Phase 2 complete  

**Mục tiêu**: Client có thể emit `JoinMessageGroup` → BE verify membership → add vào room `message_group:{id}`.

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Hub/BeaconHubJoinGroupTests.cs`

Cases:
- `JoinMessageGroup_ShouldAddToRoom_WhenUserIsMember`
- `JoinMessageGroup_ShouldReturnError_WhenUserNotMember`
- `JoinMessageGroup_ShouldReturnError_WhenGroupNotFound`

#### Bước 2 — CheckMembership Query (không dùng Hub inject Repository trực tiếp)

File: `src/Beacon.Application/Features/Messaging/Queries/CheckGroupMembership/CheckGroupMembershipQuery.cs`

```csharp
public record CheckGroupMembershipQuery(Guid UserId, Guid GroupId) : IRequest<Result<bool>>;
```

File: `src/Beacon.Application/Features/Messaging/Queries/CheckGroupMembership/CheckGroupMembershipQueryHandler.cs`

```csharp
public async Task<Result<bool>> Handle(CheckGroupMembershipQuery q, CancellationToken ct)
{
    var isMember = await _groupRepo.IsMemberAsync(q.GroupId, q.UserId, ct);
    if (!isMember)
        return Result<bool>.Failure(
            Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không thuộc nhóm chat này."));
    return Result<bool>.Success(true);
}
```

#### Bước 3 — JoinMessageGroup Request Model

File: `src/Beacon.Application/Features/Messaging/Dtos/JoinMessageGroupRequest.cs`

```csharp
public record JoinMessageGroupRequest(Guid MessageGroupId);
```

#### Bước 4 — Update BeaconHub

File: `src/Beacon.Api/Hubs/BeaconHub.cs`

```csharp
public async Task<JoinGroupResult> JoinMessageGroup(JoinMessageGroupRequest request)
{
    var userId = Context.UserIdentifier;
    if (userId is null)
        return new JoinGroupResult(false, request.MessageGroupId, null, "Không xác định được người dùng.");

    var parsedUserId = Guid.Parse(userId);
    var result = await _mediator.Send(
        new CheckGroupMembershipQuery(parsedUserId, request.MessageGroupId));

    if (!result.IsSuccess)
        return new JoinGroupResult(false, request.MessageGroupId, null, result.Error.Message);

    var roomName = $"message_group:{request.MessageGroupId}";
    await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

    _logger.LogInformation(
        "User {UserId} joined room {Room}", userId, roomName);

    return new JoinGroupResult(true, request.MessageGroupId, roomName, null);
}
```

#### Bước 5 — JoinGroupResult DTO

File: `src/Beacon.Application/Features/Messaging/Dtos/JoinGroupResult.cs`

```csharp
public record JoinGroupResult(
    bool Success,
    Guid MessageGroupId,
    string? Room,
    string? ErrorMessage);
```

#### Bước 6 — Inject IMediator vào Hub

Update `BeaconHub` constructor: inject `IMediator` và `ILogger<BeaconHub>`.  
Update `SignalRExtensions.cs` nếu cần (Hub được tạo bởi SignalR DI tự động).

#### Files Update/Create
- `src/Beacon.Api/Hubs/BeaconHub.cs`
- `src/Beacon.Application/Features/Messaging/Queries/CheckGroupMembership/CheckGroupMembershipQuery.cs` (mới)
- `src/Beacon.Application/Features/Messaging/Queries/CheckGroupMembership/CheckGroupMembershipQueryHandler.cs` (mới)
- `src/Beacon.Application/Features/Messaging/Dtos/JoinGroupResult.cs` (mới)
- `src/Beacon.Application/Features/Messaging/Dtos/JoinMessageGroupRequest.cs` (mới)

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] Unit test GREEN — member join thành công, non-member bị reject
- [ ] FE có thể gọi `connection.invoke("JoinMessageGroup", { messageGroupId: "..." })` → nhận JoinGroupResult

---

### Slice 3.2 — Switch NotifyNewMessage sang Group Room Broadcast

**Module**: Infrastructure (SignalR Notifier)  
**Type**: Refactor (tăng performance)  
**Dependencies**: Slice 3.1 (user phải join room trước)  

**Mục tiêu**: Thay N Clients.User() calls bằng 1 Clients.Group() call cho message broadcast.

#### Bước 1 — Update IRealtimeNotifier

File: `src/Beacon.Application/Common/Interfaces/IService/IRealtimeNotifier.cs`

```csharp
// Signature thay đổi: bỏ memberIds param vì không còn iterate
Task NotifyNewMessageAsync(Guid groupId, object messageDto, CancellationToken ct = default);
// Giữ các method khác
```

#### Bước 2 — Update SignalRRealtimeNotifier

File: `src/Beacon.Api/Services/SignalRRealtimeNotifier.cs`

```csharp
// TRƯỚC (N calls — anti-pattern):
public Task NotifyNewMessageAsync(Guid groupId, IEnumerable<Guid> memberIds, ...)
    => Task.WhenAll(memberIds.Select(id => hubContext.Clients.User(id.ToString()).ReceiveNewMessage(dto)));

// SAU (1 call — đúng cách):
public Task NotifyNewMessageAsync(Guid groupId, object messageDto, CancellationToken ct = default)
    => hubContext.Clients.Group($"message_group:{groupId}").ReceiveNewMessage(messageDto);
```

Tương tự update `NotifyTypingAsync` và `NotifyMessageSeenAsync` → dùng `Clients.GroupExcept()`.

#### Bước 3 — Update SendMessageCommandHandler

File: `src/Beacon.Application/Features/Messaging/Commands/SendMessage/SendMessageCommandHandler.cs`

```csharp
// TRƯỚC:
var memberIds = group.Members.Where(m => m.UserId != currentUser.UserId).Select(m => m.UserId);
await notifier.NotifyNewMessageAsync(command.GroupId, memberIds, dto, ct);

// SAU:
await notifier.NotifyNewMessageAsync(command.GroupId, dto, ct);
// Không cần memberIds nữa — broadcast to room
```

Update tương tự `UpdateTypingStatusCommandHandler`, `MarkGroupMessagesSeenCommandHandler`.

#### Files Update
- `src/Beacon.Application/Common/Interfaces/IService/IRealtimeNotifier.cs`
- `src/Beacon.Api/Services/SignalRRealtimeNotifier.cs`
- `src/Beacon.Application/Features/Messaging/Commands/SendMessage/SendMessageCommandHandler.cs`
- `src/Beacon.Application/Features/Messaging/Commands/UpdateTypingStatus/UpdateTypingStatusCommandHandler.cs`
- `src/Beacon.Application/Features/Messaging/Commands/MarkGroupMessagesSeen/MarkGroupMessagesSeenCommandHandler.cs`

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] Integration test: gửi message → chỉ member đã join room nhận được event
- [ ] Không còn `IEnumerable<Guid> memberIds` trong NotifyNewMessage call sites

---

## ✅ Checkpoint Phase 3

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test` — GREEN
- [ ] Socket flow: connect → join room → gửi message → nhận event trong room
- [ ] Typing + seen status emit đúng room

---

## Phase 4 — UserDeviceToken Entity & API

> Tạo entity mới `UserDeviceToken` (không modify `UserDevice` để không break auth flow).

---

### Slice 4.1 — UserDeviceToken Domain Entity + Migration

**Module**: Domain, Infrastructure  
**Type**: New Entity  
**Dependencies**: Phase 1 complete  

#### Bước 1 — Entity

File: `src/Beacon.Domain/Entities/Identity/UserDeviceToken.cs`

```csharp
public class UserDeviceToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DevicePlatform Platform { get; private set; }
    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public string? AppVersion { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime LastUsedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public User User { get; private set; } = default!;

    protected UserDeviceToken() { }

    public static UserDeviceToken Create(
        Guid userId, string token, DevicePlatform platform,
        string? deviceId = null, string? deviceName = null, string? appVersion = null)
        => new()
        {
            UserId = userId,
            Token = token,
            Platform = platform,
            DeviceId = deviceId,
            DeviceName = deviceName,
            AppVersion = appVersion,
            IsActive = true,
            LastUsedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    public void UpdateOwner(Guid newUserId)
    {
        UserId = newUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordUsage()
    {
        LastUsedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        IsActive = true;
    }

    public void Revoke()
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkInvalid()  // Khi Firebase báo token expired/unregistered
    {
        IsActive = false;
        RevokedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
```

#### Bước 2 — Update DevicePlatform Enum

File: `src/Beacon.Domain/Enums/Identity/DevicePlatform.cs`

```csharp
public enum DevicePlatform
{
    Unknown = 0,   // THÊM MỚI
    Android = 1,
    iOS     = 2,
    Web     = 3
}
```

#### Bước 3 — Repository Interface

File: `src/Beacon.Domain/IRepository/Identity/IUserDeviceTokenRepository.cs`

```csharp
public interface IUserDeviceTokenRepository
{
    Task<UserDeviceToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<UserDeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserDeviceToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

#### Bước 4 — EF Config

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Identity/UserDeviceTokenConfiguration.cs`

```csharp
public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> b)
    {
        b.ToTable("UserDeviceTokens");
        b.HasKey(t => t.Id);
        b.Property(t => t.Token).IsRequired().HasMaxLength(1000);
        b.HasIndex(t => t.Token).IsUnique().HasDatabaseName("UX_UserDeviceTokens_Token");
        b.HasIndex(t => new { t.UserId, t.IsActive }).HasDatabaseName("IX_UserDeviceTokens_UserId_IsActive");
        b.Property(t => t.Platform).HasConversion<string>().HasMaxLength(20);
        b.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### Bước 5 — DbContext + Migration

`AppDbContext`: thêm `DbSet<UserDeviceToken> UserDeviceTokens`

```bash
dotnet ef migrations add Add_UserDeviceTokens_Table \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

#### Files Create/Update
- `src/Beacon.Domain/Entities/Identity/UserDeviceToken.cs` (mới)
- `src/Beacon.Domain/Enums/Identity/DevicePlatform.cs`
- `src/Beacon.Domain/IRepository/Identity/IUserDeviceTokenRepository.cs` (mới)
- `src/Beacon.Infrashtructure/Presistence/Configuration/Identity/UserDeviceTokenConfiguration.cs` (mới)
- `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`
- Migration file (auto-generated)

---

### Slice 4.2 — Repository Implementation + Register/Revoke Commands

**Module**: Application, Infrastructure  
**Type**: CQRS Commands  
**Dependencies**: Slice 4.1  

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Identity/RegisterDeviceTokenCommandHandlerTests.cs`

Cases:
- `Handle_ShouldCreateNewToken_WhenTokenNotExists`
- `Handle_ShouldUpdateToken_WhenTokenAlreadyExistsForSameUser`
- `Handle_ShouldTransferToken_WhenTokenBelongsToAnotherUser`

File: `tests/Beacon.UnitTests/Identity/RevokeDeviceTokenCommandHandlerTests.cs`

Cases:
- `Handle_ShouldDeactivateToken_WhenTokenExists`
- `Handle_ShouldReturnSuccess_WhenTokenNotExists` (idempotent)

#### Bước 2 — Repository Implementation

File: `src/Beacon.Infrashtructure/Repository/Identity/UserDeviceTokenRepository.cs`

```csharp
public class UserDeviceTokenRepository(AppDbContext db) : IUserDeviceTokenRepository
{
    public Task<UserDeviceToken?> GetByTokenAsync(string token, CancellationToken ct)
        => db.UserDeviceTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public Task<List<UserDeviceToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct)
        => db.UserDeviceTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(UserDeviceToken token, CancellationToken ct)
        => await db.UserDeviceTokens.AddAsync(token, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);
}
```

#### Bước 3 — Register Command

File: `src/Beacon.Application/Features/Identity/Commands/RegisterDeviceToken/RegisterDeviceTokenCommand.cs`

```csharp
public record RegisterDeviceTokenCommand(
    Guid UserId,
    string Token,
    DevicePlatform Platform,
    string? DeviceId,
    string? DeviceName,
    string? AppVersion) : IRequest<Result>;
```

Handler: `RegisterDeviceTokenCommandHandler.cs`
- GetByToken → null: Create mới
- GetByToken → exists, same user: RecordUsage()
- GetByToken → exists, different user: UpdateOwner() + RecordUsage()
- SaveChanges

#### Bước 4 — Revoke Command

File: `src/Beacon.Application/Features/Identity/Commands/RevokeDeviceToken/RevokeDeviceTokenCommand.cs`

```csharp
public record RevokeDeviceTokenCommand(Guid UserId, string Token) : IRequest<Result>;
```

Handler: GetByToken → null: `Result.Success()` (idempotent); exists: `token.Revoke()` → SaveChanges.

#### Bước 5 — Validators

`RegisterDeviceTokenCommandValidator`:
- `Token`: NotEmpty, MaxLength(1000)
- `Platform`: Must be valid enum value

`RevokeDeviceTokenCommandValidator`:
- `Token`: NotEmpty

#### Bước 6 — Controller

File: `src/Beacon.Api/Controllers/Identity/DeviceTokensController.cs`

```csharp
[Route("api/v1/device-tokens")]
[Authorize]
public class DeviceTokensController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var command = new RegisterDeviceTokenCommand(
            currentUser.UserId, request.Token, request.Platform,
            request.DeviceId, request.DeviceName, request.AppVersion);
        return HandleResult(await mediator.Send(command, ct));
    }

    [HttpDelete]
    public async Task<IActionResult> Revoke([FromBody] RevokeDeviceTokenRequest request, CancellationToken ct)
    {
        var command = new RevokeDeviceTokenCommand(currentUser.UserId, request.Token);
        return HandleResult(await mediator.Send(command, ct));
    }
}
```

**Note**: `DevicesController.cs` cũ với route `/devices/register` giữ nguyên để backward compatible. Deprecate sau 1 sprint.

#### Bước 7 — Đăng ký DI

`InfrastructureServiceExtensions.cs`: `services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>()`

#### Files Create/Update
- `src/Beacon.Application/Features/Identity/Commands/RegisterDeviceToken/RegisterDeviceTokenCommand.cs` (mới)
- `src/Beacon.Application/Features/Identity/Commands/RegisterDeviceToken/RegisterDeviceTokenCommandHandler.cs` (mới)
- `src/Beacon.Application/Features/Identity/Commands/RevokeDeviceToken/RevokeDeviceTokenCommand.cs` (mới)
- `src/Beacon.Application/Features/Identity/Commands/RevokeDeviceToken/RevokeDeviceTokenCommandHandler.cs` (mới)
- `src/Beacon.Application/Features/Identity/Validators/RegisterDeviceTokenCommandValidator.cs` (mới)
- `src/Beacon.Application/Features/Identity/Validators/RevokeDeviceTokenCommandValidator.cs` (mới)
- `src/Beacon.Application/Features/Identity/Dtos/RegisterDeviceTokenRequest.cs` (mới)
- `src/Beacon.Application/Features/Identity/Dtos/RevokeDeviceTokenRequest.cs` (mới)
- `src/Beacon.Infrashtructure/Repository/Identity/UserDeviceTokenRepository.cs` (mới)
- `src/Beacon.Api/Controllers/Identity/DeviceTokensController.cs` (mới)

**Acceptance Criteria**:
- [ ] `POST /api/v1/device-tokens` — 200 khi register
- [ ] `DELETE /api/v1/device-tokens` — 200 kể cả khi token không tồn tại
- [ ] Token transfer: gửi token của user A từ user B → token chuyển sang user B
- [ ] Unit test GREEN — all cases

---

## ✅ Checkpoint Phase 4

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test` — GREEN
- [ ] Migration apply: `UserDeviceTokens` table tồn tại với `UX_UserDeviceTokens_Token` unique index
- [ ] Integration test: `POST /api/v1/device-tokens` + `DELETE /api/v1/device-tokens`

---

## Phase 5 — Firebase Admin SDK & FCM Service

---

### Slice 5.1 — Firebase Admin SDK Setup + IFcmService

**Module**: Application (interface), Infrastructure (impl)  
**Type**: External Service Integration  
**Dependencies**: Slice 4.1  

#### Bước 1 — Interface

File: `src/Beacon.Application/Common/Interfaces/IService/IFcmService.cs`

```csharp
public interface IFcmService
{
    Task SendToTokenAsync(
        string token, string title, string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> SendToUserAndGetInvalidTokensAsync(
        Guid userId, string title, string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
}
```

#### Bước 2 — Install Package

```bash
cd src/Beacon.Infrashtructure
dotnet add package FirebaseAdmin
```

#### Bước 3 — Configuration

`appsettings.json` (non-secret):
```json
{
  "Firebase": {
    "ProjectId": "beacon-prod"
  }
}
```

`appsettings.Development.json` (local dev — KHÔNG commit service account):
```json
{
  "Firebase": {
    "CredentialPath": "/secrets/firebase-service-account.json"
  }
}
```

Prod: environment variable `GOOGLE_APPLICATION_CREDENTIALS` hoặc `Firebase:CredentialJson` (full JSON).

#### Bước 4 — FcmService Implementation

File: `src/Beacon.Infrashtructure/Services/FcmService.cs`

```csharp
public class FcmService(
    IUserDeviceTokenRepository tokenRepo,
    ILogger<FcmService> logger) : IFcmService
{
    public async Task SendToTokenAsync(
        string token, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var message = new Message
        {
            Token = token,
            Notification = new Notification { Title = title, Body = body },
            Data = data ?? new Dictionary<string, string>()
        };
        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex)
        {
            logger.LogWarning("FCM send failed: token={Token}, error={Error}", 
                MaskToken(token), ex.MessagingErrorCode);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> SendToUserAndGetInvalidTokensAsync(
        Guid userId, string title, string body,
        Dictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var tokens = await tokenRepo.GetActiveByUserIdAsync(userId, ct);
        if (tokens.Count == 0) return Array.Empty<string>();

        var messages = tokens.Select(t => new Message
        {
            Token = t.Token,
            Notification = new Notification { Title = title, Body = body },
            Data = data ?? new Dictionary<string, string>()
        }).ToList();

        var response = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages, ct);

        var invalidTokens = new List<string>();
        for (var i = 0; i < response.Responses.Count; i++)
        {
            var r = response.Responses[i];
            if (!r.IsSuccess && IsTokenInvalid(r.Exception))
            {
                invalidTokens.Add(tokens[i].Token);
                logger.LogWarning("FCM token invalid: userId={UserId}", userId);
            }
        }
        return invalidTokens;
    }

    private static bool IsTokenInvalid(FirebaseMessagingException? ex)
        => ex?.MessagingErrorCode is MessagingErrorCode.Unregistered
            or MessagingErrorCode.InvalidArgument;

    private static string MaskToken(string token)
        => token.Length > 10 ? token[..6] + "..." + token[^4..] : "***";
}
```

#### Bước 5 — Firebase Initialization + DI

File: `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

```csharp
// Firebase initialization (Singleton — chỉ init 1 lần)
var credentialPath = configuration["Firebase:CredentialPath"];
var credentialJson = configuration["Firebase:CredentialJson"];

if (!string.IsNullOrEmpty(credentialPath))
    FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(credentialPath) });
else if (!string.IsNullOrEmpty(credentialJson))
    FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromJson(credentialJson) });
// else: dùng Application Default Credentials (GOOGLE_APPLICATION_CREDENTIALS env var)

services.AddScoped<IFcmService, FcmService>();
```

#### Files Create/Update
- `src/Beacon.Application/Common/Interfaces/IService/IFcmService.cs` (mới)
- `src/Beacon.Infrashtructure/Services/FcmService.cs` (mới)
- `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`
- `appsettings.json`, `appsettings.Development.json`
- `.gitignore`: thêm `**/firebase-service-account.json`

**Acceptance Criteria**:
- [ ] `dotnet build` 0 error
- [ ] Không có Firebase credential trong git
- [ ] FcmService có thể gửi test message (cần Firebase project thật)

---

### Slice 5.2 — Wire FCM vào NotificationService

**Module**: Infrastructure  
**Type**: Integration  
**Dependencies**: Slice 5.1, Slice 1.2  

**Mục tiêu**: `NotificationService.CreateAndDeliverAsync()` vừa emit SignalR vừa gửi FCM (fire-and-forget).

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Notification/NotificationServiceFcmTests.cs`

Cases:
- `CreateAndDeliver_ShouldCallFcmSendToUser_AfterDbCommit`
- `CreateAndDeliver_ShouldMarkInvalidTokens_WhenFcmReportsInvalid`
- `CreateAndDeliver_ShouldNotThrow_WhenFcmFails`
- `CreateAndDeliver_ShouldNotRollbackNotification_WhenFcmFails`

#### Bước 2 — Update NotificationService

File: `src/Beacon.Infrashtructure/Services/NotificationService.cs`

```csharp
public class NotificationService(
    INotificationRepository notifRepo,
    IRealtimeNotifier notifier,
    IFcmService fcmService,
    IUserDeviceTokenRepository tokenRepo,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateAndDeliverAsync(
        Guid receiverUserId, NotificationType type, string title, string body,
        string? data = null, CancellationToken ct = default)
    {
        // Step 1: DB commit — PHẢI thành công
        var notification = Notification.Create(receiverUserId, type, title, body, data);
        await notifRepo.AddAsync(notification, ct);
        await notifRepo.SaveChangesAsync(ct);

        var payload = new NotificationPayload(notification.Id, type.ToString(), title, body, data);

        // Step 2: SignalR — không throw
        try { await notifier.NotifyUserAsync(receiverUserId, payload, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "SignalR failed for user {UserId}", receiverUserId); }

        // Step 3: FCM — fire-and-forget với error boundary
        _ = Task.Run(async () =>
        {
            try
            {
                var fcmData = BuildFcmData(type, notification.Id, data);
                var invalidTokens = await fcmService.SendToUserAndGetInvalidTokensAsync(
                    receiverUserId, title, body, fcmData);

                // Cleanup invalid tokens
                if (invalidTokens.Count > 0)
                    await MarkTokensInvalidAsync(invalidTokens);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "FCM delivery failed for user {UserId}", receiverUserId);
            }
        }, CancellationToken.None);  // CancellationToken.None để không bị cancel khi request end
    }

    private static Dictionary<string, string> BuildFcmData(
        NotificationType type, Guid notificationId, string? extraData)
    {
        var d = new Dictionary<string, string>
        {
            ["type"] = "NOTIFICATION",
            ["notificationId"] = notificationId.ToString()
        };
        // Nếu extraData chứa messageGroupId → thêm vào payload
        if (!string.IsNullOrEmpty(extraData))
            d["data"] = extraData;
        return d;
    }

    private async Task MarkTokensInvalidAsync(IReadOnlyList<string> tokens)
    {
        foreach (var token in tokens)
        {
            var t = await tokenRepo.GetByTokenAsync(token);
            t?.MarkInvalid();
        }
        await tokenRepo.SaveChangesAsync();
    }
}
```

#### Files Update
- `src/Beacon.Infrashtructure/Services/NotificationService.cs`

**Acceptance Criteria**:
- [ ] Unit test GREEN — FCM được gọi sau DB commit, không throw khi FCM fail
- [ ] Notification trong DB tồn tại kể cả khi FCM fail
- [ ] Invalid tokens bị mark inactive trong DB

---

## ✅ Checkpoint Phase 5

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test` — GREEN
- [ ] Flow end-to-end: AcceptFriendRequest → Notification trong DB → SignalR emit → FCM push
- [ ] FCM fail không break flow

---

## Phase 6 — Production Hardening

---

### Slice 6.1 — Fix Startup SearchIndex Seeder (OOM Risk)

**Module**: API  
**Type**: Bug Fix / Production Risk  
**Dependencies**: Không có  
**Priority**: THỰC HIỆN NGAY — OOM risk ở production

#### Bước 1 — Fix Program.cs

File: `src/Beacon.Api/Program.cs:53-70`

```csharp
// TRƯỚC (❌ load toàn bộ Users vào memory):
var users = db.Users.ToList();

// SAU (✅ batch update bằng EF ExecuteUpdate hoặc raw SQL):
var updatedCount = 0;
var batchSize = 500;
var skip = 0;
List<User> batch;
do
{
    batch = db.Users.OrderBy(u => u.Id).Skip(skip).Take(batchSize).ToList();
    foreach (var user in batch)
    {
        var before = user.SearchIndex;
        user.UpdateSearchIndex();
        if (user.SearchIndex != before) updatedCount++;
    }
    if (batch.Count > 0) db.SaveChanges();
    skip += batchSize;
} while (batch.Count == batchSize);
```

---

### Slice 6.2 — Redis Backplane cho SignalR

**Module**: API  
**Type**: Infrastructure  
**Dependencies**: Redis instance available  

#### Bước 1 — Install Package

```bash
cd src/Beacon.Api
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

#### Bước 2 — Update SignalRExtensions

File: `src/Beacon.Api/Extensions/SignalRExtensions.cs`

```csharp
public static IServiceCollection AddApiSignalR(this IServiceCollection services, IConfiguration configuration)
{
    var redisConnection = configuration.GetConnectionString("Redis");

    var signalR = services.AddSignalR();

    if (!string.IsNullOrEmpty(redisConnection))
        signalR.AddStackExchangeRedis(redisConnection, opts =>
            opts.Configuration.ChannelPrefix = RedisChannel.Literal("beacon"));

    services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
    return services;
}
```

#### Bước 3 — Configuration

`appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Redis": ""  // empty = no backplane (dev mode)
  }
}
```

**Acceptance Criteria**:
- [ ] Với Redis connection string: backplane active
- [ ] Không có Redis connection string: fallback in-memory (dev mode)
- [ ] 2 instance chạy song song → user trên instance A nhận message từ instance B

---

### Slice 6.3 — NotifyUserAsync dùng Group Room thay Clients.User()

**Module**: Infrastructure  
**Type**: Refactor  
**Dependencies**: Slice 1.1 (OnConnectedAsync join `user:{userId}`)  

Sau khi OnConnectedAsync join room `user:{userId}`, có thể switch `NotifyUserAsync` sang group room:

```csharp
// TRƯỚC:
public Task NotifyUserAsync(Guid userId, NotificationPayload payload, ...)
    => hubContext.Clients.User(userId.ToString()).ReceiveNotification(payload);

// SAU (consistent với room pattern):
public Task NotifyUserAsync(Guid userId, NotificationPayload payload, ...)
    => hubContext.Clients.Group($"user:{userId}").ReceiveNotification(payload);
```

Benefit: `Clients.Group()` works correctly với Redis backplane; `Clients.User()` cũng work nhưng routing khác.

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN
- [ ] SignalR flow end-to-end: connect → join room → send message → receive in room
- [ ] FCM flow: notification created → push sent → invalid tokens cleaned
- [ ] Migration history clean: 2 new migrations apply cleanly
- [ ] No `IsPrivate` references trong Application/API layer
- [ ] Redis backplane test: 2 instances communicate

---

## Dependency Graph

```
Slice 1.1 (Hub Lifecycle)
    ├── Slice 1.2 (INotificationService)
    │       └── Slice 5.2 (FCM → NotificationService)
    └── Slice 3.1 (JoinMessageGroup)
            └── Slice 3.2 (Group Room Broadcast)

Slice 2.1 (Domain Entity)
    └── Slice 2.2 (Migration)
            └── Slice 2.3 (Handlers Update)

Slice 4.1 (UserDeviceToken Entity)
    └── Slice 4.2 (Register/Revoke Commands)

Slice 5.1 (Firebase SDK)
    └── Slice 5.2 (FCM → NotificationService) [cần Slice 1.2 và Slice 4.1]

Slice 6.1 (Fix OOM) — bất cứ lúc nào, no dependency
Slice 6.2 (Redis Backplane) — sau Slice 3.1
Slice 6.3 (Notify via Group Room) — sau Slice 1.1
```

## Thứ Tự Thực Hiện Khuyến Nghị

| Order | Slice | Lý do ưu tiên |
|-------|-------|--------------|
| 1 | **6.1** — Fix OOM Seeder | Production risk, 5 phút fix |
| 2 | **1.1** — Hub Lifecycle | Foundation cho tất cả socket features |
| 3 | **1.2** — INotificationService | Foundation cho FCM integration |
| 4 | **2.1** — Domain Entity update | Breaking — cần làm sớm để unblock FE |
| 5 | **2.2** — Migration | Phụ thuộc 2.1 |
| 6 | **2.3** — Handlers update | Phụ thuộc 2.2 |
| 7 | **3.1** — JoinMessageGroup | Socket events |
| 8 | **3.2** — Group Broadcast | Phụ thuộc 3.1 |
| 9 | **4.1** — UserDeviceToken Entity | FCM prerequisite |
| 10 | **4.2** — Device Token API | Phụ thuộc 4.1 |
| 11 | **5.1** — Firebase SDK | Phụ thuộc 4.1 |
| 12 | **5.2** — FCM wire-up | Phụ thuộc 5.1 + 1.2 + 4.1 |
| 13 | **6.2** — Redis Backplane | Production deploy |
| 14 | **6.3** — Notify via Group Room | Optimization |
