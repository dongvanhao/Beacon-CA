# TECHNICAL DESIGN DOCUMENT — Beacon Realtime & FCM

> Revision: 2026-05-09 | Author: Technical Lead Audit | Status: DRAFT

---

## 1. SYSTEM OVERVIEW

### 1.1 Architecture Hiện Tại

```
Client (Mobile/Web)
       │
       │ HTTP REST + WebSocket (SignalR)
       ▼
Beacon.Api
  ├── Controllers/          REST endpoints
  ├── Hubs/BeaconHub.cs     SignalR hub (hiện TRỐNG)
  ├── Services/SignalRRealtimeNotifier.cs
  ├── Extensions/AuthExtensions.cs   JWT + CORS + RBAC
  └── Extensions/SignalRExtensions.cs
       │
       ▼ MediatR (CQRS)
Beacon.Application
  ├── Features/{Module}/Commands|Queries
  ├── Common/Interfaces/IService/IRealtimeNotifier.cs
  └── Common/Interfaces/IHubs/IBeaconHub.cs
       │
       ▼ Repository Interfaces
Beacon.Domain
  └── Entities/, IRepository/
       │
       ▼ EF Core
Beacon.Infrashtructure
  ├── Repository/
  └── Presistence/AppDbContext
```

### 1.2 Module Boundaries

| Module | Controller | Application Layer | Domain Entities |
|--------|-----------|-------------------|-----------------|
| Messaging | MessageGroupsController | Features/Messaging/ | MessageGroup, Message, MessageGroupMember |
| Notification | NotificationsController | Features/Group/Queries/ListNotifications, Commands/MarkNotificationRead | Notification |
| Device | DevicesController | Features/Identity/Commands/RegisterDevice | UserDevice |
| Friend/Group | FriendsController, FriendRequestsController | Features/Group/ | Friend, FriendRequest, FriendPair |

### 1.3 Realtime Architecture Hiện Tại

**SignalR Hub** (`BeaconHub.cs`):
```csharp
[Authorize]
public class BeaconHub : Hub<IBeaconHub>
{
    // TRỐNG HOÀN TOÀN — không có OnConnectedAsync/OnDisconnectedAsync
}
```

**Client Interface** (`IBeaconHub.cs`) — đã thiết kế đúng:
```csharp
Task ReceiveNotification(NotificationPayload payload);
Task ReceiveNewMessage(object messageDto);
Task ReceiveTypingStatus(Guid groupId, Guid typingUserId, bool isTyping);
Task ReceiveMessageSeen(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId);
```

**Notifier Implementation** (`SignalRRealtimeNotifier.cs`):
- Dùng `hubContext.Clients.User(userId.ToString())` — targeting theo User ID, KHÔNG dùng Groups/Rooms
- `NotifyNewMessageAsync` iterate từng member ID → N requests đến SignalR backplane (anti-pattern at scale)

**JWT-for-WebSocket** (`AuthExtensions.cs:41-54`) — **ĐÃ CÓ**, đúng pattern:
```csharp
OnMessageReceived = context => {
    var accessToken = context.Request.Query["access_token"];
    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        context.Token = accessToken;
}
```

### 1.4 Current Strengths

1. **JWT WebSocket extraction**: đã implement đúng `OnMessageReceived` trong JwtBearerEvents
2. **Clean Architecture**: dependency direction đúng, handler không inject DbContext trực tiếp
3. **IRealtimeNotifier abstraction**: Application layer không biết SignalR — dễ swap implementation
4. **Cursor pagination**: toàn bộ list API dùng cursor, không offset
5. **Result pattern**: nhất quán, không throw exception cho business error
6. **Notification entity**: đủ fields (ReceiverUserId, Type, Data, IsRead, ReadAtUtc)
7. **AcceptFriendRequest**: đã có pattern đúng — tạo notification + emit realtime

### 1.5 Current Weaknesses & Technical Debt

| # | Issue | Severity | Location |
|---|-------|----------|----------|
| W1 | BeaconHub trống — không OnConnectedAsync, không Groups | CRITICAL | `Hubs/BeaconHub.cs` |
| W2 | SignalRRealtimeNotifier dùng Clients.User() thay Group rooms | HIGH | `Services/SignalRRealtimeNotifier.cs:16` |
| W3 | MessageGroup dùng `IsPrivate` bool thay type enum | HIGH | `Domain/Entities/Messaging/MessageGroup.cs:8` |
| W4 | Không có `DirectKey` — không ngăn duplicate DIRECT conversation | HIGH | `Domain/Entities/Messaging/MessageGroup.cs` |
| W5 | UserDevice thiếu `DeviceId`, `AppVersion`, `RevokedAt` fields | MEDIUM | `Domain/Entities/Identity/UserDevice.cs` |
| W6 | DevicesController route sai (`/devices/register` vs `/device-tokens`) | MEDIUM | `Controllers/DevicesController.cs:32` |
| W7 | Không có FCM/Firebase integration | HIGH | — |
| W8 | Không có `INotificationService` — logic notification rải rác trong handler | MEDIUM | Multiple handlers |
| W9 | `NotificationDelivery` entity đã thiết kế nhưng chưa sử dụng (commented out) | MEDIUM | `Domain/Entities/Notification/NotificationDelivery.cs` |
| W10 | SignalR chưa có Redis backplane — không scale horizontal | HIGH | `Extensions/SignalRExtensions.cs` |
| W11 | `CreateDirectMessageGroupHandler` không set `DirectKey` | HIGH | `EventHandlers/CreateDirectMessageGroupHandler.cs` |

### 1.6 Scalability Issues

- **No Redis backplane**: SignalR chỉ work với 1 instance. Deploy nhiều pod → user A trên pod 1, user B trên pod 2 → B không nhận message từ A
- **Clients.User() iteration**: `NotifyNewMessageAsync` gọi N lần cho N members. Group 100 người = 100 SignalR calls. Với rooms/groups → 1 broadcast
- **Startup SearchIndex seeder**: `Program.cs:53-70` load toàn bộ Users table vào memory mỗi restart — sẽ fail ở production với 100k+ users

### 1.7 Security Issues

- **No OnDisconnectedAsync**: connection leak — khi client disconnect, không cleanup group memberships
- **BeaconHub không validate**: client có thể call Hub methods mà không có ownership check (khi thêm socket events sau)
- **CORS AllowAll ở dev**: không vấn đề nhưng cần đảm bảo không deploy config dev lên prod

---

## 2. GAP ANALYSIS THEO TỪNG TASK

---

### Task 1 — JWT Socket Authentication

**STATUS: PARTIAL (70%)**

#### Current State
`AuthExtensions.cs:39-54` đã extract `access_token` từ query param cho `/hubs/*` route. JWT middleware validate token bình thường. `[Authorize]` trên `BeaconHub` đã force authentication.

#### What Works
- Token extraction từ `?access_token=` query param: ✅
- JWT validation (issuer, audience, lifetime, signing key): ✅
- Hub từ chối connection nếu token invalid (middleware reject trước khi OnConnected): ✅

#### What's Missing
```
OnConnectedAsync() → extract userId từ Context.UserIdentifier → log
OnDisconnectedAsync() → cleanup groups
```

`Context.UserIdentifier` trong SignalR được map từ `NameIdentifier` claim (ASP.NET Core Identity default). Cần verify JwtService có set `NameIdentifier` claim khi tạo token không.

#### Files Need Verification
- `src/Beacon.Infrashtructure/Services/JwtService.cs` — kiểm tra claim `NameIdentifier` hay `sub`
- `src/Beacon.Api/Hubs/BeaconHub.cs` — cần thêm `OnConnectedAsync`

#### Risk of Fix
LOW. Thêm override methods vào Hub không break existing behavior.

---

### Task 2 — Auto Join Personal Room

**STATUS: FAIL (0%)**

#### Current State
`BeaconHub.cs` không có `OnConnectedAsync`. Không có `Groups.AddToGroupAsync` ở bất kỳ đâu trong codebase.

#### Problems
1. `SignalRRealtimeNotifier.NotifyUserAsync` dùng `hubContext.Clients.User(userId.ToString())` — đây là SignalR's built-in user targeting, KHÔNG phải group room
2. `Clients.User()` hoạt động nhờ UserIdProvider, sẽ gửi đến tất cả connections của user đó — về mặt chức năng đúng
3. Tuy nhiên, requirement yêu cầu room format `user:{userId}` — đây là explicit Groups, khác với Clients.User()

#### Design Decision Required
**Option A**: Giữ `Clients.User()` — simpler, SignalR manages mapping automatically. Không cần OnConnectedAsync chỉ để join room.

**Option B**: Implement `Groups.AddToGroupAsync("user:{userId}")` trong OnConnectedAsync, sau đó dùng `Clients.Group("user:{userId}")`. Đúng spec hơn, nhưng phức tạp hơn.

**Recommendation**: Option A đủ cho functional requirement. Option B chỉ cần nếu có use case cần send to group room từ external service (Redis backplane scenario). Implement OnConnectedAsync nhưng dùng `Clients.User()` pattern.

#### Files Need Update
- `src/Beacon.Api/Hubs/BeaconHub.cs`

#### Risk
LOW. Không có breaking change.

---

### Task 3 — Notification Database Design

**STATUS: PASS (95%)**

#### Current State
`src/Beacon.Domain/Entities/Group/Notification.cs`:
```csharp
public Guid ReceiverUserId   // ✅
public NotificationType Type  // ✅
public string Title           // ✅
public string Body            // ✅
public string? Data           // ✅ JSON metadata
public bool IsRead            // ✅
public DateTime? ReadAtUtc    // ✅
```

#### Minor Gap
`Notification` kế thừa `AuditableEntity` → có `CreatedAtUtc` dùng làm cursor. Đủ.

Không có `ReadAtUtc` alias từ spec (`readAt`) — chỉ là naming, DTO mapping đã handle.

#### Risk
NONE. Không cần migration thêm.

---

### Task 4 — NotificationService

**STATUS: PARTIAL (40%)**

#### Current State
- `IRealtimeNotifier.NotifyUserAsync()` tồn tại và hoạt động
- `AcceptFriendRequestCommandHandler` implement pattern manually: tạo Notification → SaveChanges → NotifyUserAsync
- KHÔNG có `INotificationService` interface
- KHÔNG có FCM push
- Logic tạo notification bị duplicate trong từng handler (SendFriendRequest, AcceptFriendRequest...)

#### Code Smell
```csharp
// AcceptFriendRequestCommandHandler.cs:49-60
var notification = Notification.Create(...);
await notifRepo.AddAsync(notification, ct);
await notifRepo.SaveChangesAsync(ct);
await notifier.NotifyUserAsync(request.InitiatorId, new NotificationPayload(...), ct);
```
Pattern này sẽ bị copy-paste sang mọi handler cần tạo notification → vi phạm DRY, dễ quên step nào đó.

#### Required Design
```csharp
// Application/Common/Interfaces/IService/INotificationService.cs
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

Implementation trong Infrastructure inject `INotificationRepository + IRealtimeNotifier + IFcmService`.

#### Impact
Cần refactor AcceptFriendRequestCommandHandler, SendFriendRequestCommandHandler (nếu có notification), và các handler tương lai.

---

### Task 5 — API Get Notification List

**STATUS: PASS (100%)**

#### Current State
- `GET /api/v1/notifications` — `NotificationsController` + `ListNotificationsQueryHandler`
- Cursor pagination (DateTime)
- `unreadCount` trả kèm
- Owner check: query theo `q.CurrentUserId`

#### Potential Performance Issue
`CountUnreadAsync` là query riêng sau `ListByReceiverAsync` → 2 round trips. Có thể optimize bằng 1 query với GROUP BY, nhưng đây là premature optimization ở scale hiện tại.

---

### Task 6 — Mark Notification as Read

**STATUS: PASS (95%)**

#### Current State
`MarkNotificationReadCommandHandler` — đúng:
- Owner check: `notification.ReceiverUserId != cmd.CurrentUserId` → Forbidden
- Idempotent: `Notification.MarkRead()` check `if (IsRead) return`
- Trả `unreadCount` mới

`MarkAllNotificationsReadCommandHandler` — cần verify trả `updatedCount`.

#### Minor Gap
Response spec yêu cầu `notificationId, isRead, readAt, unreadCount`. Cần kiểm tra `MarkReadResponse` DTO có đủ field không.

---

### Task 7 — MessageGroup Type DIRECT/GROUP

**STATUS: FAIL (0%)**

#### Current State
```csharp
// MessageGroup.cs:8
public bool IsPrivate { get; set; }  // ❌ boolean, không phải enum
```

```csharp
// CreateDirectMessageGroupHandler.cs:17
var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
// ❌ không có DirectKey
```

#### Problems
1. **`IsPrivate` bool không extensible** — tương lai nếu có type thứ 3 (CHANNEL, BROADCAST) thì phải refactor toàn bộ
2. **Không có `DirectKey`** — không có cơ chế ngăn tạo duplicate DIRECT conversation giữa 2 user. Hiện tại `MessageGroupRepository.GetPrivateGroupBetweenAsync()` workaround bằng cách tìm group có đúng 2 member cụ thể, nhưng không có unique constraint ở DB level → race condition có thể tạo duplicate
3. **Không có `Name` và `AvatarUrl` riêng cho GROUP** — đã có `Name` và `AvatarMediaObjectId` nhưng không phân biệt DIRECT/GROUP context

#### Required Migration
```sql
-- Non-breaking: thêm column mới, sau đó migrate data, sau đó drop IsPrivate
ALTER TABLE MessageGroups ADD Type NVARCHAR(20) NOT NULL DEFAULT 'DIRECT';
ALTER TABLE MessageGroups ADD DirectKey NVARCHAR(100) NULL;
CREATE UNIQUE INDEX UX_MessageGroups_DirectKey ON MessageGroups (DirectKey) WHERE DirectKey IS NOT NULL;

-- Data migration: IsPrivate=true → DIRECT, IsPrivate=false → GROUP
UPDATE MessageGroups SET Type = CASE WHEN IsPrivate = 1 THEN 'DIRECT' ELSE 'GROUP' END;
-- DirectKey: gen từ Member pairs cho existing DIRECT groups
```

#### Risk of Fix
MEDIUM-HIGH. Breaking change cho `MessageGroupDto` (có `IsPrivate` field), `MessageGroupDetailDto` (có `IsPrivate` field). FE cần update.

---

### Task 8 — displayName/displayAvatarUrl

**STATUS: PARTIAL (60%)**

#### Current State
DTOs đã có:
```csharp
// MessageGroupDto.cs
public record MessageGroupDto(
    Guid GroupId,
    bool IsPrivate,     // ❌ cần thay bằng Type
    ...
    string? DisplayName,       // ✅ field tồn tại
    string? DisplayAvatarUrl); // ✅ field tồn tại
```

Logic trong `MessageGroupMapper` hoặc `ListMyMessageGroupsQueryHandler` — cần check xem DisplayName được compute thế nào. Với `IsPrivate=true`, cần tìm peer user và lấy tên/avatar.

Hiện tại `MessageGroupRepository.ListByUserAsync` là cursor query phức tạp — cần verify có join Users table để lấy peer info cho DIRECT groups không.

#### Missing
- Sau khi migrate sang `Type` enum, logic phải đổi điều kiện từ `IsPrivate` → `Type == DIRECT`
- Fallback: "Người dùng" và "Nhóm chat" chưa được implement
- GROUP fallback name từ member names chưa implement

---

### Task 9 — message_group:join Socket Event

**STATUS: FAIL (0%)**

#### Current State
`BeaconHub.cs` trống. Không có socket event handler nào.

#### Required Design
```csharp
public async Task JoinMessageGroup(JoinMessageGroupRequest request)
{
    // 1. Extract currentUserId từ Context.UserIdentifier
    // 2. Verify membership via IMessageGroupRepository
    // 3. Groups.AddToGroupAsync("message_group:{groupId}")
    // 4. Ack response
}
```

#### Architecture Note
Hub methods inject services via HubContext DI, không qua MediatR. Hub không nên contain business logic → cần inject `IMessageGroupRepository` hoặc wrapper service.

**Concern**: Hub inject repository trực tiếp? Hay Hub dispatch sang MediatR? 

**Recommendation**: Hub gọi `IMediator.Send()` — giữ Clean Architecture, Hub chỉ là thin layer. Tạo `JoinMessageGroupCommand` handler.

Tuy nhiên Hub cần gọi `Groups.AddToGroupAsync()` — đây là SignalR-specific, không thể đặt trong Application layer (vi phạm layer). 

**Pattern đúng**: Handler return `Result<bool>`, Hub nhận result rồi tự call `Groups.AddToGroupAsync`.

---

### Task 10 — Emit message:new to Room

**STATUS: PARTIAL (50%)**

#### Current State
```csharp
// SignalRRealtimeNotifier.cs:13-16
public Task NotifyNewMessageAsync(Guid groupId, IEnumerable<Guid> memberIds, object messageDto, ...)
    => Task.WhenAll(memberIds.Select(id =>
        hubContext.Clients.User(id.ToString()).ReceiveNewMessage(messageDto)));
```

**Chức năng đúng** (gửi tới đúng user), nhưng **scale sai**:
- Group 50 người = 50 parallel SignalR calls
- Với Redis backplane, mỗi call là 1 Redis pub/sub message → 50x overhead
- Đúng design: `hubContext.Clients.Group("message_group:{groupId}").ReceiveNewMessage(dto)` = 1 call

#### Required Change
Sau khi có Task 9 (join room), `NotifyNewMessageAsync` phải chuyển sang `Clients.Group()`:
```csharp
public Task NotifyNewMessageAsync(Guid groupId, ...) 
    => hubContext.Clients.Group($"message_group:{groupId}").ReceiveNewMessage(messageDto);
```

**Dependency**: Task 9 phải hoàn thành trước — user phải đã join room trước khi có thể nhận broadcast.

---

### Task 11 — FCM Token Table

**STATUS: PARTIAL (45%)**

#### Current State
`UserDevice` entity:
```csharp
public Guid UserId
public DevicePlatform Platform     // ✅ enum: Android, iOS, Web...
public string DeviceName           // ✅
public string DeviceToken          // ✅ (FCM/APNs token)
public bool IsActive               // ✅
public DateTime? LastSeenAtUtc     // ✅ (alias LastUsedAt)
// SoftDeletableEntity → IsDeleted, DeletedAtUtc
```

**Missing fields:**
- `DeviceId` (device fingerprint từ client) — KHÔNG CÓ. UserDevice được track qua RefreshToken DeviceId, đây là design khác
- `AppVersion` — KHÔNG CÓ
- `RevokedAt` — dùng `DeletedAtUtc` từ SoftDeletableEntity thay thế được, nhưng không có explicit `RevokedAt`
- `UpdatedAt` — AuditableEntity có `UpdatedAtUtc` không? Cần check base class

#### Design Issue
`UserDevice` hiện đang dùng như thiết bị vật lý (physical device), còn FCM token spec yêu cầu nó là token-centric (1 token = 1 record, có thể transfer giữa user). Đây là conflict về intent.

**Recommendation**: Tạo entity mới `UserDeviceToken` thay vì modify `UserDevice`, để không break existing auth flow (RefreshToken FK vào UserDevice).

---

### Task 12 — API Device Token

**STATUS: PARTIAL (30%)**

#### Current State
`DevicesController`:
- `POST /api/v1/devices/register` — ✅ exists
- Route sai: spec yêu cầu `POST /api/v1/device-tokens`
- Thiếu `DELETE /api/v1/device-tokens`

`RegisterDeviceCommand` cần review implementation — lấy DeviceId từ JWT claim, không từ request body.

#### Files Need Update
- `src/Beacon.Api/Controllers/DevicesController.cs` — add DELETE, fix route
- `src/Beacon.Application/Features/Identity/Commands/RegisterDevice*` — verify logic
- Cần `RevokeDeviceTokenCommand` + handler

---

### Task 13 — Firebase Admin SDK

**STATUS: FAIL (0%)**

Không có bất kỳ file nào liên quan đến Firebase trong toàn bộ codebase.

Required:
- NuGet: `FirebaseAdmin`
- `appsettings.json` section `Firebase:CredentialsPath` (dev) hoặc env var
- `IFcmService` interface tại `Application/Common/Interfaces/IService/`
- `FcmService` implementation tại `Infrastructure/Services/`

---

### Task 14 — Send FCM on Notification Created

**STATUS: FAIL (0%)**

Depends on Task 13 (FCM SDK) + Task 11/12 (token storage) + Task 4 (INotificationService).

Cannot implement without prerequisites.

---

## 3. REALTIME ARCHITECTURE SPEC

### 3.1 Hub Flow (Production-Grade)

```
Client Connect Flow:
─────────────────────────────────────────────────────────────
1. FE: new HubConnection("wss://.../hubs/beacon?access_token=<jwt>")
2. SignalR: extract token từ query param (AuthExtensions.OnMessageReceived) ✅ DONE
3. JWT Middleware: validate token → set ClaimsPrincipal
4. BeaconHub.OnConnectedAsync() [MISSING]:
   a. userId = Context.UserIdentifier  // mapped from NameIdentifier claim
   b. Log: "User {userId} connected, ConnectionId={id}"
   c. Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}")
   d. (Optional) presence tracking: IPresenceService.SetOnlineAsync(userId)
5. Connection established ← FE receives connected event
─────────────────────────────────────────────────────────────

Client Disconnect Flow:
─────────────────────────────────────────────────────────────
1. Client disconnect (network drop, logout, tab close)
2. BeaconHub.OnDisconnectedAsync(exception) [MISSING]:
   a. Log: "User {userId} disconnected, reason={exception?.Message}"
   b. Groups.RemoveFromGroupAsync(ConnectionId, $"user:{userId}")
      → SignalR auto-removes from all groups on disconnect, but explicit is clearer
   c. (Optional) presence: IPresenceService.SetOfflineAsync(userId)
3. All message group rooms auto-cleaned by SignalR
─────────────────────────────────────────────────────────────

Message Group Join Flow (new):
─────────────────────────────────────────────────────────────
1. FE opens chat screen → emit "JoinMessageGroup" với payload {messageGroupId}
2. BeaconHub.JoinMessageGroup(request):
   a. userId = Context.UserIdentifier
   b. var result = await mediator.Send(new CheckMessageGroupMembershipQuery(userId, groupId))
   c. if (!result.IsSuccess) → Clients.Caller.ReceiveError("NOT_MEMBER")
   d. await Groups.AddToGroupAsync(ConnectionId, $"message_group:{groupId}")
   e. Ack: Clients.Caller.ReceiveJoinAck({ success: true, groupId, room })
─────────────────────────────────────────────────────────────

Message Send + Broadcast Flow (updated):
─────────────────────────────────────────────────────────────
1. FE: POST /api/v1/message-groups/{id}/messages
2. SendMessageCommandHandler:
   a. Verify group exists + user is member
   b. Idempotency check (clientMessageId)
   c. Message.Create() → messageRepo.SaveChanges()
   d. notifier.NotifyNewMessageAsync(groupId, dto)
3. SignalRRealtimeNotifier.NotifyNewMessageAsync [UPDATED]:
   → hubContext.Clients.Group("message_group:{groupId}").ReceiveNewMessage(dto)
   // Single broadcast — NOT iteration
─────────────────────────────────────────────────────────────
```

### 3.2 Connection Lifecycle States

```
DISCONNECTED → CONNECTING → CONNECTED → ACTIVE
                               │
                          OnConnectedAsync
                          - join user:{id} room
                          - log
                               │
                          [message_group:join events]
                          - join message_group:{id} rooms
                               │
                          DISCONNECTED
                          OnDisconnectedAsync
                          - SignalR auto-removes all groups
                          - log
```

### 3.3 Reconnect Strategy (FE Responsibility)

```javascript
// FE implementation guide
const connection = new HubConnectionBuilder()
  .withUrl("/hubs/beacon", { accessTokenFactory: () => getAccessToken() })
  .withAutomaticReconnect([0, 2000, 10000, 30000])  // retry intervals ms
  .build();

connection.onreconnected(() => {
  // Re-join message group rooms after reconnect
  openChatGroups.forEach(id => connection.invoke("JoinMessageGroup", { messageGroupId: id }));
});
```

BE không cần persist room membership — client re-joins on reconnect.

### 3.4 Room Naming Convention

| Room | Format | Usage |
|------|--------|-------|
| Personal notification | `user:{userId}` | Notifications, friend requests |
| Message group | `message_group:{groupId}` | Chat messages, typing, seen |

### 3.5 Online/Offline Detection (Optional — Phase 2)

Implement `IPresenceService` backed by Redis SET:
- `SADD online_users {userId}` on connect
- `SREM online_users {userId}` on disconnect  
- TTL 60s, refreshed by heartbeat ping

Used by NotificationService: nếu user online → skip FCM (optional optimization).

---

## 4. DATABASE DESIGN SPEC

### 4.1 MessageGroup — Migration Strategy

#### Phase 1: Add new columns (non-breaking)
```sql
ALTER TABLE MessageGroups ADD Type NVARCHAR(20) NOT NULL DEFAULT 'DIRECT';
ALTER TABLE MessageGroups ADD DirectKey NVARCHAR(100) NULL;
```

#### Phase 2: Populate data
```sql
-- Migrate IsPrivate → Type
UPDATE MessageGroups 
SET Type = CASE WHEN IsPrivate = 1 THEN 'DIRECT' ELSE 'GROUP' END;

-- Gen DirectKey cho DIRECT groups (EF migration C#)
-- Dùng: smaller_guid + "_" + larger_guid (sorted order)
```

#### Phase 3: Add constraints
```sql
CREATE UNIQUE INDEX UX_MessageGroups_DirectKey 
ON MessageGroups (DirectKey) 
WHERE DirectKey IS NOT NULL;
```

#### Phase 4: Deprecate IsPrivate (không xóa ngay, FE có thể còn dùng)
Sau 1 release cycle → `ALTER TABLE MessageGroups DROP COLUMN IsPrivate`.

#### Tại sao cần DirectKey?
- `GetPrivateGroupBetweenAsync()` hiện dùng query: tìm group có đúng 2 member là userA và userB — đây là `O(n)` scan với subquery, không dùng index hiệu quả
- Race condition: 2 user cùng accept friend request → `GetPrivateGroupBetweenAsync()` trả null cho cả 2 → tạo 2 duplicate DIRECT groups
- `DirectKey = min(userId, userId2) + "_" + max(userId, userId2)` + UNIQUE constraint → DB enforce atomicity

#### Entity Design (updated)
```csharp
public class MessageGroup : BaseEntity
{
    public MessageGroupType Type { get; set; }  // enum: Direct, Group
    public string? DirectKey { get; set; }       // nullable, unique index
    public string? Name { get; set; }            // GROUP only
    public Guid? AvatarMediaObjectId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    // ... navigation props
}

public enum MessageGroupType { Direct = 0, Group = 1 }
```

### 4.2 UserDeviceToken — New Entity Design

**Decision**: Tạo entity mới `UserDeviceToken` thay vì modify `UserDevice`. Lý do: `UserDevice` hiện đang có FK từ `RefreshToken` → modify sẽ phá vỡ auth flow.

```csharp
// Domain/Entities/Identity/UserDeviceToken.cs
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
}
```

#### Indexes
```sql
CREATE UNIQUE INDEX UX_UserDeviceTokens_Token ON UserDeviceTokens (Token);
CREATE INDEX IX_UserDeviceTokens_UserId_IsActive ON UserDeviceTokens (UserId, IsActive);
```

#### Platform Enum (add UNKNOWN)
```csharp
public enum DevicePlatform { Unknown = 0, Android = 1, iOS = 2, Web = 3 }
```
`DevicePlatform.Unknown` đã cần thêm vào existing enum.

### 4.3 Notifications — Current State (PASS, no changes needed)

```sql
-- Hiện có:
CREATE INDEX IX_Notifications_ReceiverUserId ON Notifications (ReceiverUserId);
-- Cần verify:
CREATE INDEX IX_Notifications_ReceiverUserId_IsRead ON Notifications (ReceiverUserId, IsRead);
-- Cho CountUnreadAsync query
```

### 4.4 Tại sao không dùng IsPrivate nữa

1. **Semantic ambiguity**: "private" không rõ private với ai — với system, với user ngoài group, hay private = 1-1?
2. **Non-extensible**: nếu thêm type mới (CHANNEL, BROADCAST, SAVED_MESSAGES) thì bool không đủ
3. **Explicit is better**: `Type.Direct` vs `IsPrivate=true` — code self-documenting hơn
4. **DB query**: `WHERE Type = 'DIRECT'` có thể dùng partial index, `WHERE IsPrivate = 1` cũng được nhưng semantic rõ hơn

---

## 5. SOCKET EVENT SPEC

### 5.1 Client → Server Events (Client Emit)

#### `JoinMessageGroup`
```typescript
// Client emit
socket.invoke("JoinMessageGroup", { messageGroupId: "guid-here" });

// Server ack (return value)
{
  "success": true,
  "messageGroupId": "guid-here",
  "room": "message_group:guid-here"
}

// Error ack
{
  "success": false,
  "error": "NOT_MEMBER",
  "message": "Bạn không thuộc nhóm chat này"
}
```

**Authorization**: Hub extract userId từ `Context.UserIdentifier`, verify membership via repo. Client không được pass userId.

### 5.2 Server → Client Events (Server Emit)

#### `ReceiveNotification` (existing)
```typescript
// Payload: NotificationPayload
{
  "notificationId": "guid",
  "type": "FriendAccepted",     // NotificationType enum name
  "title": "...",
  "body": "...",
  "data": "{\"friendId\":\"...\"}"  // JSON string, nullable
}
```
Room target: `user:{receiverUserId}`

#### `ReceiveNewMessage` (existing — update target)
```typescript
// Payload: MessageDto (current implementation passes object)
{
  "messageId": "guid",
  "groupId": "guid",
  "senderId": "guid",
  "senderFamilyName": "...",
  "senderGivenName": "...",
  "content": "...",
  "sentAtUtc": "2026-05-09T..."
}
```
Room target: `message_group:{groupId}` (phải đổi từ Clients.User iteration)

#### `ReceiveTypingStatus` (existing)
```typescript
{ groupId: "guid", typingUserId: "guid", isTyping: true }
```
Room target: Clients.GroupExcept("message_group:{groupId}", [Context.ConnectionId])

#### `ReceiveMessageSeen` (existing)
```typescript
{ groupId: "guid", seenByUserId: "guid", lastSeenMessageId: "guid" }
```

### 5.3 Room Naming Strategy

```
user:{userId}              // Personal room, joined on OnConnectedAsync
message_group:{groupId}    // Chat room, joined on JoinMessageGroup event
```

### 5.4 Ack Pattern

SignalR supports return values from Hub methods (server-side ack):
```csharp
public async Task<JoinGroupResult> JoinMessageGroup(JoinMessageGroupRequest request)
{
    // ...
    return new JoinGroupResult { Success = true, Room = $"message_group:{groupId}" };
}
```

Client: `const result = await connection.invoke("JoinMessageGroup", payload);`

### 5.5 Security Validation Rules

| Event | Validation |
|-------|-----------|
| JoinMessageGroup | Verify userId ∈ group.Members |
| ReceiveNewMessage | SendMessage API handles auth, Hub only broadcasts |
| ReceiveTypingStatus | UpdateTypingStatus API handles auth |
| ReceiveNotification | Only sent to `user:{receiverId}` — no client can request this |

---

## 6. NOTIFICATION SERVICE SPEC

### 6.1 INotificationService Design

```csharp
// Application/Common/Interfaces/IService/INotificationService.cs
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

### 6.2 Transaction Boundary & Timing

```
CreateAndDeliverAsync flow:
─────────────────────────────────────────────────────────────────────
[SYNCHRONOUS — same thread, same DB transaction as caller]
1. Notification.Create(receiverUserId, type, title, body, data)
2. notifRepo.AddAsync(notification, ct)
3. notifRepo.SaveChangesAsync(ct)  ← DB commit here

[POST-COMMIT — KHÔNG rollback nếu fail]
4. realtimeNotifier.NotifyUserAsync(receiverUserId, payload, ct)
   → fire-and-forget hoặc try-catch không throw
5. fcmService.SendToUserAsync(receiverUserId, title, body, fcmData, ct)
   → fire-and-forget hoặc try-catch không throw

[LOG]
6. Nếu bước 4 hoặc 5 fail → log warning, KHÔNG throw, KHÔNG rollback
─────────────────────────────────────────────────────────────────────
```

**Key Rules:**
- Bước 1-3: SYNCHRONOUS, must succeed. Nếu DB fail → exception propagate → caller handle
- Bước 4-5: POST-COMMIT, KHÔNG được block response, KHÔNG được rollback notification đã lưu
- FCM fail ≠ notification fail — notification đã lưu vào DB là source of truth

### 6.3 Async Pattern cho FCM

Option A (Simple — recommended for current scale):
```csharp
// Fire and forget với error boundary
_ = Task.Run(async () =>
{
    try { await fcmService.SendToUserAsync(...); }
    catch (Exception ex) { logger.LogWarning(ex, "FCM delivery failed for user {UserId}", userId); }
});
```

Option B (Channels — recommended for high scale):
```csharp
// Publish to System.Threading.Channels, background worker consumes
await channel.Writer.WriteAsync(new FcmJob(receiverUserId, title, body, data));
```

**Recommendation**: Option A cho MVP. Option B khi user base > 10k active.

### 6.4 Failure Handling

| Step | Failure | Action |
|------|---------|--------|
| DB SaveChanges | Exception | Propagate — caller handles (400/500) |
| SignalR emit | Exception | Log warning, don't throw |
| FCM send | Exception | Log warning, mark invalid tokens |
| FCM invalid token | FirebaseException | Mark token inactive in DB |

### 6.5 Logic Classification

| Logic | Synchronous | Asynchronous | No-rollback |
|-------|-------------|--------------|-------------|
| Create Notification in DB | ✅ | | |
| SignalR emit | | ✅ (await OK, but no throw) | ✅ |
| FCM push | | ✅ (fire-forget) | ✅ |
| Mark FCM token inactive | | ✅ | ✅ |

---

## 7. FCM ARCHITECTURE SPEC

### 7.1 Firebase Admin SDK Integration

```
Infrastructure/Services/FcmService.cs
    → FirebaseApp.Create(AppOptions)  [singleton]
    → FirebaseMessaging.DefaultInstance
    
Configuration:
- Dev: appsettings.Development.json: { "Firebase": { "CredentialPath": "/path/to/sa.json" } }
- Prod: Environment variable FIREBASE_CREDENTIAL_JSON (full JSON content)
- KHÔNG commit service account JSON vào git
```

### 7.2 IFcmService Interface

```csharp
// Application/Common/Interfaces/IService/IFcmService.cs
public interface IFcmService
{
    Task SendToTokenAsync(
        string token, string title, string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task SendToUserAsync(
        Guid userId, string title, string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> SendToUserAndGetInvalidTokensAsync(
        Guid userId, string title, string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default);
}
```

### 7.3 Multi-Device Strategy

```csharp
// FcmService.SendToUserAsync implementation flow:
1. Fetch all active tokens: SELECT * FROM UserDeviceTokens WHERE UserId = @userId AND IsActive = true
2. Build BatchMessage với tất cả tokens
3. FirebaseMessaging.SendEachAsync(messages)  // parallel, 500 per batch limit
4. Collect failed tokens (Registration-Token-Not-Registered, INVALID_ARGUMENT)
5. Mark invalid tokens inactive: UPDATE SET IsActive=false, RevokedAtUtc=now WHERE Token IN (...)
```

### 7.4 Invalid Token Cleanup

Firebase trả `MessagingErrorCode.Unregistered` hoặc `MessagingErrorCode.InvalidArgument` khi token expired/revoked:
```csharp
foreach (var (response, token) in results.Zip(tokens))
{
    if (!response.IsSuccess && IsTokenInvalid(response.Exception))
        invalidTokens.Add(token);
}
// Batch update: mark inactive
```

### 7.5 Online User vs Offline User

**Current implementation**: không detect presence → gửi FCM cho tất cả.

**Recommended behavior** (product decision cần confirm):
- Option A: Luôn gửi FCM (simpler, nhất quán)
- Option B: Chỉ gửi FCM nếu user không có active SignalR connection

Option A recommended cho MVP. Duplicate notification (user nhận cả socket lẫn FCM khi online) là acceptable UX với nhiều apps (Slack, WhatsApp).

### 7.6 FCM Data Payload Convention

```json
// Notification loại thông thường
{
  "type": "NOTIFICATION",
  "notificationId": "guid"
}

// Notification liên quan đến message
{
  "type": "MESSAGE_NEW",
  "notificationId": "guid",
  "messageGroupId": "guid",
  "messageId": "guid"
}

// Friend request
{
  "type": "FRIEND_REQUEST",
  "notificationId": "guid",
  "requesterId": "guid"
}
```

FE dùng `type` để navigate đến đúng screen.

---

## 8. IMPLEMENTATION ROADMAP

### Phase 1 — Foundation (Không break anything)

**Duration: 1-2 days**

1. **BeaconHub.OnConnectedAsync** — thêm log + join `user:{userId}` room
   - File: `src/Beacon.Api/Hubs/BeaconHub.cs`
   - Dependency: None
   
2. **Verify JwtService NameIdentifier claim** — ensure `Context.UserIdentifier` works
   - File: `src/Beacon.Infrashtructure/Services/JwtService.cs`
   - Dependency: None

3. **INotificationService** — extract pattern từ AcceptFriendRequestCommandHandler
   - Files: `Application/Common/Interfaces/IService/INotificationService.cs`, `Infrastructure/Services/NotificationService.cs`
   - Dependency: None (FCM phần sau)

---

### Phase 2 — MessageGroup Schema Migration (Breaking)

**Duration: 2-3 days**

4. **Add MessageGroupType enum** — `Domain/Enums/Messaging/MessageGroupType.cs`
5. **Update MessageGroup entity** — add `Type`, `DirectKey`
6. **EF Migration** — add columns, unique index, populate Type from IsPrivate
7. **Update CreateDirectMessageGroupHandler** — set `Type=Direct`, compute `DirectKey`
8. **Update CreateGroupCommandHandler** — set `Type=Group`
9. **Update all DTOs** — replace `IsPrivate` with `Type`
10. **Update displayName logic** — use `Type==Direct` condition

**Migration script** (outline):
```csharp
// EF Migration Up():
migrationBuilder.AddColumn<string>("Type", "MessageGroups", ...);
migrationBuilder.AddColumn<string?>("DirectKey", "MessageGroups", ...);
migrationBuilder.Sql("UPDATE MessageGroups SET Type = CASE WHEN IsPrivate = 1 THEN 'DIRECT' ELSE 'GROUP' END");
// DirectKey for existing DIRECT groups: C# migration data seeder
migrationBuilder.CreateIndex("UX_MessageGroups_DirectKey", "MessageGroups", "DirectKey", unique: true, filter: "[DirectKey] IS NOT NULL");
```

---

### Phase 3 — Message Group Socket Events

**Duration: 1-2 days**

11. **JoinMessageGroup hub method** — BeaconHub.JoinMessageGroup()
    - Dependency: Phase 1 (Hub has OnConnectedAsync)
    
12. **Update SignalRRealtimeNotifier.NotifyNewMessageAsync** — switch to Group room
    - Dependency: Task 11 (clients must be in rooms first)

---

### Phase 4 — FCM Infrastructure

**Duration: 2-3 days**

13. **UserDeviceToken entity + migration** — new entity, not modify UserDevice
14. **IUserDeviceTokenRepository** + implementation
15. **Register DeviceToken API** — `POST /api/v1/device-tokens`, `DELETE /api/v1/device-tokens`
16. **Firebase Admin SDK setup** — `FirebaseApp.Create`, `appsettings` config
17. **IFcmService + FcmService** — implementation

---

### Phase 5 — Wire FCM into Notification Flow

**Duration: 1-2 days**

18. **Update INotificationService** — inject IFcmService, fire-and-forget FCM after DB commit
19. **Update AcceptFriendRequest, other handlers** — use INotificationService instead of manual pattern
20. **Revoke token on logout** — logout command → mark tokens inactive

---

### Phase 6 — Redis Backplane (Production Readiness)

**Duration: 1 day**

21. **AddSignalR().AddStackExchangeRedis(connectionString)** — `SignalRExtensions.cs`
22. Deploy Redis instance
23. Remove sticky session requirement

**Dependency**: All phases above must complete first.

---

## 9. PRODUCTION CONCERNS

### 9.1 Redis Backplane — CRITICAL for Horizontal Scale

**Current state**: SignalR in-memory — 1 server instance only.

```csharp
// SignalRExtensions.cs — MUST add before multi-pod deploy
services.AddSignalR()
    .AddStackExchangeRedis(configuration.GetConnectionString("Redis")!);
```

**Without Redis**: Pod A users cannot receive messages from Pod B users. Load balancer sticky session is a workaround but fragile (session drain, pod restart).

**With Redis**: Groups, User mappings replicated via Redis pub/sub. Any pod can broadcast to any user.

### 9.2 Connection Explosion Risk

**Scenario**: 10k concurrent users × 1 connection each = 10k SignalR connections.

**Memory**: SignalR connection ~2-5KB each = ~50MB RAM for 10k users. Manageable.

**Risk**: Reconnect storm (server restart → all clients reconnect simultaneously). Mitigate with `withAutomaticReconnect` jitter on FE.

### 9.3 Group Cleanup

SignalR auto-removes connection from all groups on disconnect. No manual cleanup needed.
BUT: If server crashes (not graceful shutdown), in-memory groups are lost. With Redis backplane, this is handled automatically.

### 9.4 Memory Leak Risks

1. `Task.WhenAll` in `NotifyNewMessageAsync` — if any task throws, exception is silently swallowed (WhenAll rethrows on await but result not awaited in current impl). Must await or handle exception.

2. Firebase connection pooling — `FirebaseApp.Create()` must be called once (singleton). Multiple calls throw `FirebaseAppException`.

### 9.5 Security Hardening

| Concern | Current State | Recommendation |
|---------|--------------|----------------|
| Rate limiting on Hub | None | Add per-connection rate limiter |
| Group membership validation | Missing in Hub | Hub verify membership before join |
| Token replay | JWT doesn't rotate on WS | Accept — WS connection is long-lived |
| CORS for WebSocket | `AllowAll` in dev | Ensure prod config has `AllowCredentials` + specific origins |

### 9.6 Startup SearchIndex Seeder — DANGER

```csharp
// Program.cs:53-70
var users = db.Users.ToList();  // ❌ LOADS ALL USERS INTO MEMORY
```

With 100k users → OOM risk on pod with limited memory. Should be:
```csharp
// Batch process:
await db.Users.ExecuteUpdateAsync(u => u.SetProperty(x => x.SearchIndex, x => /* computed */));
// OR: run as one-time migration script, not on every startup
```

### 9.7 Logging

Minimum required log points:
```
Hub.OnConnectedAsync: Info — "Hub connected: userId={}, connId={}"
Hub.OnDisconnectedAsync: Info — "Hub disconnected: userId={}, connId={}, error={}"
JoinMessageGroup: Info — "User {} joined group room {}"
NotificationService: Warning — "SignalR emit failed for user {}"
FcmService: Warning — "FCM delivery failed: token={masked}, error={}"
FcmService: Info — "FCM token invalidated: userId={}, count={}"
```

### 9.8 Distributed Tracing

Add `Activity`/OpenTelemetry correlation:
- TraceId propagate từ HTTP request → MediatR handler → SignalR emit → FCM send
- Tất cả log phải include `traceId` và `userId` (không phải PII khác)

---

## 10. FINAL RECOMMENDATION

### 10.1 Kiến Trúc Tối Ưu

```
Kiến trúc mục tiêu:

Client ─── REST API ──────────────────► Handler → DB
  │                                         │
  │                                         ├──► INotificationService
  │                                         │       ├──► DB (Notification)
  │                                         │       ├──► SignalR (realtime)
  │                                         │       └──► FCM (push, fire-forget)
  │
  └── WebSocket (SignalR) ─► BeaconHub
          │                    ├── OnConnected → join user:{id}
          │                    ├── JoinMessageGroup → join message_group:{id}
          │                    └── Receive events (passive)
          │
          └── Redis Backplane (multi-pod)
```

### 10.2 Thứ Tự Sửa Code

1. **Ngay lập tức (không breaking)**: BeaconHub.OnConnectedAsync, INotificationService extract
2. **Sprint 1**: MessageGroup schema migration (Type + DirectKey) — cần coordination với FE
3. **Sprint 1**: JoinMessageGroup hub event + update NotifyNewMessage sang Group rooms
4. **Sprint 2**: UserDeviceToken entity + FCM API endpoints + Firebase SDK
5. **Sprint 2**: Wire FCM vào INotificationService
6. **Pre-production**: Redis backplane

### 10.3 Những Phần Nguy Hiểm Nhất

| Risk | Impact | Mitigation |
|------|--------|-----------|
| `IsPrivate` → `Type` migration | Breaking — FE/BE sync required | Blue-green deploy, feature flag |
| Startup SearchIndex seeder load-all | OOM in production | Fix before go-live |
| `Task.WhenAll` without await in notifier | Silent FCM/SignalR failures | Add proper error handling |
| No Redis backplane | Cannot scale horizontally | Block multi-pod deploy until added |
| DirectKey race condition if not fixed | Duplicate DIRECT conversations | Add UNIQUE constraint before go-live |

### 10.4 Anti-Patterns Hiện Tại

| Anti-pattern | Location | Fix |
|-------------|----------|-----|
| Hub hoàn toàn trống | `BeaconHub.cs` | Add lifecycle methods |
| N+1 SignalR calls (iterate members) | `SignalRRealtimeNotifier.cs:16` | Use `Clients.Group()` |
| Boolean flag thay enum | `MessageGroup.IsPrivate` | Migrate to Type enum |
| Notification pattern bị duplicate | `AcceptFriendRequest, các handler khác` | Extract INotificationService |
| Startup full-table load | `Program.cs:53` | Batch update or one-time migration |
| Device token trên endpoint sai | `DevicesController` | Fix route + add DELETE |

### 10.5 Refactor Bắt Buộc Trước Go-Live

1. **Fix startup seeder** (`Program.cs:53-70`) — OOM risk
2. **Add Redis backplane** — horizontal scale prerequisite
3. **Add DirectKey unique constraint** — data integrity
4. **Add proper error handling** cho `NotifyNewMessageAsync` (Task.WhenAll exception handling)
5. **BeaconHub.OnDisconnectedAsync** — connection cleanup logging

---

## APPENDIX — File Checklist

### Files to Create
```
src/Beacon.Domain/Entities/Identity/UserDeviceToken.cs
src/Beacon.Domain/Enums/Messaging/MessageGroupType.cs
src/Beacon.Domain/IRepository/Identity/IUserDeviceTokenRepository.cs
src/Beacon.Application/Common/Interfaces/IService/INotificationService.cs
src/Beacon.Application/Common/Interfaces/IService/IFcmService.cs
src/Beacon.Application/Features/Identity/Commands/RegisterDeviceToken/RegisterDeviceTokenCommand.cs
src/Beacon.Application/Features/Identity/Commands/RegisterDeviceToken/RegisterDeviceTokenCommandHandler.cs
src/Beacon.Application/Features/Identity/Commands/RevokeDeviceToken/RevokeDeviceTokenCommand.cs
src/Beacon.Application/Features/Identity/Commands/RevokeDeviceToken/RevokeDeviceTokenCommandHandler.cs
src/Beacon.Application/Features/Messaging/Queries/CheckMessageGroupMembership/CheckMessageGroupMembershipQuery.cs
src/Beacon.Application/Features/Messaging/Queries/CheckMessageGroupMembership/CheckMessageGroupMembershipQueryHandler.cs
src/Beacon.Infrashtructure/Repository/Identity/UserDeviceTokenRepository.cs
src/Beacon.Infrashtructure/Presistence/Configuration/Identity/UserDeviceTokenConfiguration.cs
src/Beacon.Infrashtructure/Services/NotificationService.cs
src/Beacon.Infrashtructure/Services/FcmService.cs
```

### Files to Modify
```
src/Beacon.Api/Hubs/BeaconHub.cs                                    — add OnConnectedAsync, OnDisconnectedAsync, JoinMessageGroup
src/Beacon.Api/Services/SignalRRealtimeNotifier.cs                  — update NotifyNewMessageAsync to use Groups
src/Beacon.Api/Controllers/DevicesController.cs                     — fix route, add DELETE
src/Beacon.Api/Extensions/SignalRExtensions.cs                      — add Redis backplane
src/Beacon.Api/Extensions/AuthExtensions.cs                         — verify (already correct)
src/Beacon.Api/Program.cs                                           — fix startup seeder
src/Beacon.Application/Common/Interfaces/IHubs/IBeaconHub.cs       — add ReceiveError, ReceiveJoinAck
src/Beacon.Application/Common/Interfaces/IService/IRealtimeNotifier.cs — add group-based overload
src/Beacon.Application/Features/Group/Commands/AcceptFriendRequest/ — use INotificationService
src/Beacon.Application/Features/Messaging/Commands/CreateGroup/     — set Type=Group
src/Beacon.Application/Features/Messaging/EventHandlers/CreateDirectMessageGroupHandler.cs — set Type+DirectKey
src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDto.cs  — replace IsPrivate with Type
src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDetailDto.cs — replace IsPrivate with Type
src/Beacon.Domain/Entities/Messaging/MessageGroup.cs               — add Type, DirectKey
src/Beacon.Domain/Entities/Identity/UserDevice.cs                  — keep as-is, new entity instead
src/Beacon.Domain/Enums/Identity/DevicePlatform.cs                 — add Unknown value
src/Beacon.Infrashtructure/Presistence/AppDbContext.cs             — add UserDeviceTokens DbSet
src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs — register new repos + services
```

### Migrations Required
```
Add_MessageGroup_Type_DirectKey
Add_UserDeviceTokens_Table
```
