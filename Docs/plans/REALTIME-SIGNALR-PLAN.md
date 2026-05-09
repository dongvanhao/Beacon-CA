# Plan: Realtime Notification & Chat với SignalR

**Spec:** `docs/specs/REALTIME-NOTIFICATION-SPEC.md`  
**Module:** Group + Messaging (cross-cutting)  
**Tổng số slices:** 15  
**Thứ tự:** Foundation → Notification Domain → REST APIs → SignalR Infrastructure → Push Hooks → Typing/Seen

---

## Trạng thái hiện tại (Baseline)

| Kiểm tra | Kết quả |
|---|---|
| `Notification` entity | ❌ Chưa có |
| `INotificationRepository` | ❌ Chưa có |
| `IRealtimeNotifier` interface | ❌ Chưa có |
| `BeaconHub` | ❌ Chưa có |
| `SignalR` NuGet package | ❌ Chưa có trong `Beacon.Api.csproj` |
| Error codes Notification | ❌ Chưa có |
| `AppDbContext` DbSet Notification | ❌ Chưa có (có `using` nhưng không có `DbSet`) |
| `SendMessageCommandHandler` push | ❌ Chưa call notifier |
| `SendFriendRequestCommandHandler` push | ❌ Chưa call notifier |

---

## Phase 0 — SignalR Wiring (Foundation)

> Không có business logic. Chỉ setup cơ sở hạ tầng SignalR.

---

### Slice 0.1 — Thêm SignalR NuGet + BeaconHub + JWT Auth

**Type:** Infrastructure setup  
**Dependencies:** Không có

#### Việc cần làm

**1. Thêm NuGet** vào `src/Beacon.Api/Beacon.Api.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR" Version="8.0.*" />
```
> SignalR đã bundled trong ASP.NET Core 8, thực ra chỉ cần `AddSignalR()` — không cần thêm package riêng. Kiểm tra lại trước khi add.

**2. Tạo** `src/Beacon.Api/Hubs/BeaconHub.cs`  
**3. Tạo** `src/Beacon.Api/Extensions/SignalRExtensions.cs` — `AddApiSignalR()` + `MapSignalRHubs()`  
**4. Sửa** `src/Beacon.Api/Extensions/AuthExtensions.cs` — thêm `OnMessageReceived` đọc `?access_token=` cho path `/hubs`  
**5. Sửa** `src/Beacon.Api/Program.cs` — gọi `builder.Services.AddApiSignalR()` và `app.MapSignalRHubs()`

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error
- [ ] `GET /hubs/beacon` trả `101 Switching Protocols` khi có token hợp lệ
- [ ] `GET /hubs/beacon` trả `401` khi không có token

---

### Slice 0.2 — `IRealtimeNotifier` + `IBeaconHub` marker + `NotificationPayload`

**Type:** Application layer abstraction  
**Dependencies:** Slice 0.1

#### Việc cần làm

**1. Tạo** `src/Beacon.Application/Common/Interfaces/IHubs/IBeaconHub.cs` — marker interface (rỗng)  
**2. Tạo** `src/Beacon.Application/Common/Interfaces/IService/NotificationPayload.cs` — record  
**3. Tạo** `src/Beacon.Application/Common/Interfaces/IService/IRealtimeNotifier.cs` — 4 method signatures

> Application layer chỉ thấy `IRealtimeNotifier` — không biết SignalR tồn tại.

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error
- [ ] Các file nằm đúng namespace `Beacon.Application.Common.Interfaces.*`

---

## ✅ Checkpoint 0: Foundation

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `BeaconHub` route hoạt động (test thủ công với Postman WebSocket)
- [ ] `IRealtimeNotifier` interface tồn tại đúng layer

---

## Phase 1 — Notification Domain + Infrastructure

---

### Slice 1.1 — `NotificationType` Enum + `Notification` Entity + `INotificationRepository`

**Type:** Domain  
**Dependencies:** Không có (pure Domain, zero framework dep)

#### Việc cần làm

**1. Tạo** `src/Beacon.Domain/Enums/Group/NotificationType.cs`

```csharp
public enum NotificationType
{
    FriendRequest  = 1,
    FriendAccepted = 2,
    GroupInvite    = 3,
    GroupMessage   = 4,
    System         = 99
}
```

**2. Tạo** `src/Beacon.Domain/Entities/Group/Notification.cs`
- Kế thừa `AuditableEntity` (có `Id`, `CreatedAtUtc`, `UpdatedAtUtc`)
- Fields: `ReceiverUserId`, `Type`, `Title`, `Body`, `Data` (JSON string), `IsRead`, `ReadAtUtc`
- Static factory: `Notification.Create(...)`
- Domain method: `MarkRead()` — guard nếu đã đọc rồi

**3. Tạo** `src/Beacon.Domain/IRepository/Group/INotificationRepository.cs`
- `GetByIdAsync(Guid id, ct)`
- `ListByReceiverAsync(Guid receiverUserId, DateTime? cursor, int limit, ct)`
- `CountUnreadAsync(Guid receiverUserId, ct)`
- `AddAsync(Notification notification, ct)`
- `MarkAllReadAsync(Guid receiverUserId, ct)` — bulk update
- `SaveChangesAsync(ct)`

#### Unit Test (không cần mock — pure domain)

File: `tests/Beacon.UnitTests/Group/NotificationEntityTests.cs`

- `MarkRead_ShouldSetIsReadTrue_AndSetReadAtUtc`
- `MarkRead_WhenAlreadyRead_ShouldNotUpdateReadAtUtc`
- `Create_ShouldInitializeWithIsReadFalse`

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error
- [ ] `NotificationEntityTests` — 3 tests GREEN

---

### Slice 1.2 — EF Config + Repository Implementation + AppDbContext

**Type:** Infrastructure  
**Dependencies:** Slice 1.1

#### Việc cần làm

**1. Tạo** `src/Beacon.Infrashtructure/Presistence/Configuration/Group/NotificationConfiguration.cs`
- Table: `Notifications`
- Index 1: `(ReceiverUserId, CreatedAtUtc)` — `IX_Notifications_ReceiverUserId_CreatedAtUtc`
- Index 2: `(ReceiverUserId, IsRead)` — `IX_Notifications_ReceiverUserId_IsRead`
- FK: `ReceiverUserId → Users.Id` với `DeleteBehavior.Cascade`
- MaxLength: `Title(256)`, `Body(1024)`, `Data(4000)`

**2. Sửa** `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`
- Thêm `DbSet<Notification> Notifications { get; set; }`

**3. Tạo** `src/Beacon.Infrashtructure/Repository/Group/NotificationRepository.cs`
- `ListByReceiverAsync` dùng cursor trên `CreatedAtUtc DESC`
- `MarkAllReadAsync` dùng `ExecuteUpdateAsync` (bulk, không load toàn bộ entity)
- `SaveChangesAsync` gọi `db.SaveChangesAsync(ct)`

**4. Đăng ký DI** trong `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`:
```csharp
services.AddScoped<INotificationRepository, NotificationRepository>();
```

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error
- [ ] `AppDbContext` có `DbSet<Notification>`
- [ ] EF config được auto-discover qua `ApplyConfigurationsFromAssembly`

---

### Slice 1.3 — EF Migration: `AddNotificationEntity`

**Type:** Migration (schema change)  
**Dependencies:** Slice 1.2

#### Việc cần làm

```bash
dotnet ef migrations add AddNotificationEntity \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

**Review migration file trước khi apply:**
- [ ] Table `Notifications` được tạo
- [ ] 2 index xuất hiện đúng tên
- [ ] FK đến `Users` đúng
- [ ] Không có `DROP` nào ngoài ý muốn

#### Acceptance Criteria
- [ ] `dotnet ef database update` — thành công, không lỗi
- [ ] DB có table `Notifications` với đúng schema

---

### Slice 1.4 — Error Codes cho Notification

**Type:** Shared constants  
**Dependencies:** Không có (có thể làm song song với Slice 1.1)

#### Việc cần làm

Sửa `src/Beacon.Shared/Constants/ErrorCodes.cs` — thêm section Notification:

```csharp
// Notification
public const string NOTIFICATION_NOT_FOUND = "NOTIFICATION_NOT_FOUND";
public const string NOTIFICATION_FORBIDDEN  = "NOTIFICATION_FORBIDDEN";
```

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error
- [ ] Các constant được dùng trong handler ở Phase 2

---

## ✅ Checkpoint 1: Notification Domain Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — `NotificationEntityTests` GREEN
- [ ] DB migration apply thành công
- [ ] `INotificationRepository` đăng ký DI đúng

---

## Phase 2 — Notification REST APIs (TDD)

---

### Slice 2.1 — `ListNotifications` Query

**Type:** Query  
**Dependencies:** Slice 1.1, 1.2, 1.4

#### Bước 1 — Viết Test (RED) trước

File: `tests/Beacon.UnitTests/Group/ListNotificationsQueryHandlerTests.cs`

```csharp
// Scenarios:
// Handle_ShouldReturnEmptyList_WhenUserHasNoNotifications
// Handle_ShouldReturnCursorPagedResult_WithCorrectUnreadCount
// Handle_ShouldFilterByCursor_WhenCursorProvided
// Handle_ShouldCapLimitAt50
```

Mock `INotificationRepository` — test FAIL vì handler chưa tồn tại.

#### Bước 2 — DTO

File: `src/Beacon.Application/Features/Group/Dtos/NotificationDto.cs`
- `NotificationDto` (record)
- `NotificationListResponse` (record: Items, NextCursor, HasNextPage, UnreadCount)

#### Bước 3 — Query + Handler

**File:** `src/Beacon.Application/Features/Group/Queries/ListNotifications/ListNotificationsQuery.cs`
```csharp
public record ListNotificationsQuery(Guid CurrentUserId, DateTime? Cursor, int Limit)
    : IRequest<Result<NotificationListResponse>>;
```

**File:** `src/Beacon.Application/Features/Group/Queries/ListNotifications/ListNotificationsQueryHandler.cs`
- Cap limit: `Math.Min(q.Limit, 50)`
- Lấy `limit + 1` items để detect `HasNextPage`
- Tính `nextCursor` từ `page.Last().CreatedAtUtc.ToString("O")` nếu có trang tiếp

#### Bước 4 — Validator

File: `src/Beacon.Application/Features/Group/Validators/Group/ListNotificationsQueryValidator.cs`
```csharp
RuleFor(x => x.Limit).InclusiveBetween(1, 50)
    .WithMessage("Limit phải từ 1 đến 50.");
```

#### Bước 5 — Hoàn thiện Test (GREEN)

Chạy lại test từ Bước 1 — phải pass.

#### Acceptance Criteria
- [ ] 4 unit test GREEN
- [ ] Handler return `Result<NotificationListResponse>` đúng shape
- [ ] `UnreadCount` chính xác (riêng biệt với pagination)

---

### Slice 2.2 — `MarkNotificationRead` Command

**Type:** Command  
**Dependencies:** Slice 1.1, 1.2, 1.4

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Group/MarkNotificationReadCommandHandlerTests.cs`

```csharp
// Handle_ShouldReturnSuccess_AndReturnNewUnreadCount
// Handle_ShouldReturnNotFound_WhenNotificationDoesNotExist
// Handle_ShouldReturnForbidden_WhenNotificationBelongsToAnotherUser
// Handle_ShouldBeIdempotent_WhenAlreadyRead (gọi 2 lần không lỗi)
```

#### Bước 2 — Command + Handler

**File:** `src/Beacon.Application/Features/Group/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs`

**File:** `src/Beacon.Application/Features/Group/Commands/MarkNotificationRead/MarkNotificationReadCommandHandler.cs`
- GetByIdAsync → null → `Error.NotFound(NOTIFICATION_NOT_FOUND)`
- Owner check: `notification.ReceiverUserId != cmd.CurrentUserId` → `Error.Forbidden(NOTIFICATION_FORBIDDEN)`
- `notification.MarkRead()` → `SaveChangesAsync` → `CountUnreadAsync` → return

#### Bước 3 — Response DTO

File: `src/Beacon.Application/Features/Group/Dtos/NotificationDto.cs` — thêm:
```csharp
public record MarkReadResponse(int UnreadCount);
```

#### Acceptance Criteria
- [ ] 4 unit test GREEN
- [ ] Owner check hoạt động đúng

---

### Slice 2.3 — `MarkAllNotificationsRead` Command

**Type:** Command  
**Dependencies:** Slice 1.1, 1.2

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Group/MarkAllNotificationsReadCommandHandlerTests.cs`

```csharp
// Handle_ShouldReturnUnreadCountZero_AfterMarkAll
// Handle_ShouldReturnSuccess_WhenNoUnreadExists (idempotent)
```

#### Bước 2 — Command + Handler

**File:** `src/Beacon.Application/Features/Group/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommand.cs`

**File:** `src/Beacon.Application/Features/Group/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommandHandler.cs`
- Gọi `_repo.MarkAllReadAsync(cmd.CurrentUserId, ct)` — bulk EF `ExecuteUpdateAsync`
- Return `Result.Success(new MarkReadResponse(0))`

> Không gọi `CountUnreadAsync` sau bulk update — biết chắc là 0.

#### Acceptance Criteria
- [ ] 2 unit test GREEN
- [ ] Bulk update không load toàn bộ entity (verify qua EF query log trong integration test)

---

### Slice 2.4 — `NotificationsController` + Integration Tests

**Type:** API layer  
**Dependencies:** Slice 2.1, 2.2, 2.3

#### Việc cần làm

**1. Tạo** `src/Beacon.Api/Controllers/Group/NotificationsController.cs`
- Route: `api/v1/notifications`
- `[Authorize]` trên class
- `GET /` → `ListNotificationsQuery` (query params: `cursor`, `limit`)
- `PATCH /{id:guid}/read` → `MarkNotificationReadCommand`
- `PATCH /read-all` → `MarkAllNotificationsReadCommand`
- XML doc đầy đủ cho cả 3 endpoint (theo quy tắc CLAUDE.md)

**2. Tạo** `tests/Beacon.IntergrationTests/Group/NotificationsControllerTests.cs`

| Test | Endpoint | Expected |
|---|---|---|
| `List_ShouldReturn200_WithEmptyList` | GET / | 200, `data.items = []` |
| `List_ShouldReturn401_WhenNotAuthenticated` | GET / | 401 |
| `List_ShouldReturn400_WhenLimitOutOfRange` | GET /?limit=100 | 400, `VALIDATION_ERROR` |
| `MarkRead_ShouldReturn200_AndDecreaseUnreadCount` | PATCH /{id}/read | 200 |
| `MarkRead_ShouldReturn404_WhenNotFound` | PATCH /{id}/read | 404, `NOTIFICATION_NOT_FOUND` |
| `MarkRead_ShouldReturn403_WhenWrongOwner` | PATCH /{id}/read | 403, `NOTIFICATION_FORBIDDEN` |
| `MarkAllRead_ShouldReturn200_WithZeroUnreadCount` | PATCH /read-all | 200, `unreadCount = 0` |

#### Acceptance Criteria
- [ ] 7 integration tests GREEN
- [ ] `HandleResult` / `CreatedResult` đúng (GET → 200, không dùng 204)
- [ ] Response shape: `{ success, message, code, data, errors }`

---

## ✅ Checkpoint 2: Notification REST APIs Complete

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả Notification tests GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — `NotificationsControllerTests` GREEN
- [ ] Swagger hiển thị 3 endpoint đúng route và auth requirement

---

## Phase 3 — SignalR Infrastructure

---

### Slice 3.1 — `SignalRRealtimeNotifier` Implementation

**Type:** Infrastructure service  
**Dependencies:** Slice 0.1, 0.2

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Group/SignalRRealtimeNotifierTests.cs`

```csharp
// NotifyUserAsync_ShouldCallClientsUserWithCorrectPayload
// NotifyNewMessageAsync_ShouldPushToAllMembersExceptSender
// NotifyTypingAsync_ShouldExcludeTypingUser
// NotifyMessageSeenAsync_ShouldExcludeSeenByUser
```

Mock `IHubContext<BeaconHub, IBeaconHub>` để verify `Clients.User(id).SendAsync(eventName, payload)` được gọi đúng số lần.

#### Bước 2 — Implementation

**File:** `src/Beacon.Infrashtructure/Services/SignalRRealtimeNotifier.cs`
- Inject `IHubContext<BeaconHub, IBeaconHub>`
- 4 methods theo spec
- `NotifyNewMessageAsync`: `Task.WhenAll` cho tất cả memberIds
- `NotifyTypingAsync`: filter bỏ `typingUserId` trước khi push
- `NotifyMessageSeenAsync`: filter bỏ `seenByUserId` trước khi push

**Sửa** `BeaconHub.cs` để implement `IBeaconHub`:
```csharp
public class BeaconHub : Hub<IBeaconHub> { }
```

#### Bước 3 — Đăng ký DI

Sửa `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`:
```csharp
services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
```

#### Acceptance Criteria
- [ ] 4 unit test GREEN
- [ ] `IRealtimeNotifier` resolve được qua DI container
- [ ] `dotnet build` — 0 error

---

## ✅ Checkpoint 3: SignalR Infrastructure Complete

- [ ] `dotnet build` — 0 error
- [ ] `SignalRRealtimeNotifierTests` — 4 tests GREEN
- [ ] `IRealtimeNotifier` DI registration hoạt động

---

## Phase 4 — Push Hooks vào Handler hiện có

> Tích hợp `IRealtimeNotifier` vào các handler đã tồn tại. Mỗi slice là một handler.

---

### Slice 4.1 — `SendFriendRequestCommandHandler` → push `notification:new`

**Type:** Handler modification  
**Dependencies:** Slice 1.1, 1.2, 3.1

#### Việc cần làm

Sửa `src/Beacon.Application/Features/Group/Commands/SendFriendRequest/SendFriendRequestCommandHandler.cs`:

1. Inject `INotificationRepository` và `IRealtimeNotifier`
2. Sau `SaveChangesAsync` thành công:
   - Tạo `Notification.Create(targetUserId, NotificationType.FriendRequest, ...)`
   - `_notifRepo.AddAsync(notification, ct)` + `_notifRepo.SaveChangesAsync(ct)`
   - `await _notifier.NotifyUserAsync(targetUserId, payload, ct)`

#### Bước 1 — Thêm Test Case vào Test hiện có

Sửa `tests/Beacon.UnitTests/Group/SendFriendRequestCommandHandlerTests.cs` — thêm:
```csharp
// Handle_ShouldCallNotifyUserAsync_WhenFriendRequestCreated
// Handle_ShouldSaveNotification_WhenFriendRequestCreated
```

#### Acceptance Criteria
- [ ] Test mới GREEN
- [ ] Test cũ không bị break
- [ ] `IRealtimeNotifier.NotifyUserAsync` được gọi đúng 1 lần với đúng `receiverUserId`

---

### Slice 4.2 — `AcceptFriendRequestCommandHandler` → push `notification:new` (FriendAccepted)

**Type:** Handler modification  
**Dependencies:** Slice 4.1

#### Việc cần làm

Sửa `src/Beacon.Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommandHandler.cs`:

1. Inject `INotificationRepository` và `IRealtimeNotifier`
2. Sau accept thành công:
   - Push `notification:new` với `Type = NotificationType.FriendAccepted` đến **người gửi friend request ban đầu**
   - Tạo và lưu Notification entity tương ứng

#### Bước 1 — Thêm Test Case

```csharp
// Handle_ShouldNotifyOriginalSender_WhenFriendRequestAccepted
```

#### Acceptance Criteria
- [ ] Test mới GREEN
- [ ] Notification lưu đúng `ReceiverUserId` = người gửi request ban đầu (không phải người accept)

---

### Slice 4.3 — `SendMessageCommandHandler` → push `message:new`

**Type:** Handler modification  
**Dependencies:** Slice 3.1

#### Việc cần làm

Sửa `src/Beacon.Application/Features/Messaging/Commands/SendMessage/SendMessageCommandHandler.cs`:

1. Inject `IRealtimeNotifier`
2. Cần biết danh sách `memberIds` của group — nếu handler hiện tại chưa load members, cần thêm query vào repository
3. Sau `SaveChangesAsync`:
   ```csharp
   var memberIds = group.Members
       .Where(m => m.UserId != cmd.SenderId)
       .Select(m => m.UserId);
   await _notifier.NotifyNewMessageAsync(group.Id, memberIds, messageDto, ct);
   ```

> **Lưu ý:** Nếu `group.Members` chưa được load, cần review `IMessageGroupRepository` để thêm `GetWithMembersAsync`. Đánh giá khi implement để tránh N+1.

#### Bước 1 — Thêm Test Case

Sửa `tests/Beacon.UnitTests/Messaging/SendMessageCommandHandlerTests.cs`:
```csharp
// Handle_ShouldCallNotifyNewMessageAsync_WhenMessageSent
// Handle_ShouldNotPushToSender_OnlyToOtherMembers
```

#### Acceptance Criteria
- [ ] Test mới GREEN
- [ ] Test cũ không bị break
- [ ] Sender không nhận `message:new` về chính mình

---

## ✅ Checkpoint 4: Push Hooks Complete

- [ ] `dotnet build` — 0 error
- [ ] Tất cả test cũ trong `Group/` và `Messaging/` vẫn GREEN
- [ ] Test mới cho notification push GREEN
- [ ] End-to-end manual test: gửi friend request → nhận event trên WebSocket client

---

## Phase 5 — Typing & Seen REST Endpoints

---

### Slice 5.1 — `UpdateTypingStatus` Command → push `message:typing`

**Type:** Command (fire-and-forget, KHÔNG lưu DB)  
**Dependencies:** Slice 3.1

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Messaging/UpdateTypingStatusCommandHandlerTests.cs`

```csharp
// Handle_ShouldCallNotifyTypingAsync_WhenUserIsTyping
// Handle_ShouldCallNotifyTypingAsync_WhenUserStopsTyping
// Handle_ShouldReturnNotFound_WhenGroupDoesNotExist
// Handle_ShouldReturnForbidden_WhenUserIsNotGroupMember
```

#### Bước 2 — Command + Handler

**File:** `src/Beacon.Application/Features/Messaging/Commands/UpdateTypingStatus/UpdateTypingStatusCommand.cs`
```csharp
public record UpdateTypingStatusCommand(Guid GroupId, Guid UserId, bool IsTyping)
    : IRequest<Result>;
```

**File:** `src/Beacon.Application/Features/Messaging/Commands/UpdateTypingStatus/UpdateTypingStatusCommandHandler.cs`
- Load group để verify membership (handler không lưu bất kỳ gì vào DB)
- `await _notifier.NotifyTypingAsync(groupId, otherMemberIds, userId, isTyping, ct)`
- Return `Result.Success()`

#### Bước 3 — Validator

```csharp
RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId không được để trống.");
RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId không được để trống.");
```

#### Bước 4 — Endpoint

Thêm vào `MessageGroupsController`:
```
PATCH /api/v1/message-groups/{id:guid}/typing
Body: { "isTyping": true }
```

#### Acceptance Criteria
- [ ] 4 unit test GREEN
- [ ] Handler không gọi `SaveChangesAsync` — xác nhận bằng mock verify `Times.Never`
- [ ] Chỉ member của group mới gọi được endpoint (403 nếu không phải member)

---

### Slice 5.2 — `MarkGroupMessagesSeen` Command → push `message:seen`

**Type:** Command (có lưu DB — LastSeenMessageId của member)  
**Dependencies:** Slice 3.1

#### Bước 1 — Viết Test (RED)

File: `tests/Beacon.UnitTests/Messaging/MarkGroupMessagesSeenCommandHandlerTests.cs`

```csharp
// Handle_ShouldUpdateLastSeenMessageId_ForCurrentUser
// Handle_ShouldCallNotifyMessageSeenAsync_ToOtherMembers
// Handle_ShouldReturnNotFound_WhenGroupDoesNotExist
// Handle_ShouldReturnNotFound_WhenMessageDoesNotExist
```

#### Bước 2 — Domain / Entity Change

Kiểm tra `MessageGroupMember` entity có `LastSeenMessageId` chưa:
- Nếu chưa có → thêm property + migration riêng `AddLastSeenMessageId`
- Nếu đã có → dùng trực tiếp

#### Bước 3 — Command + Handler

**File:** `src/Beacon.Application/Features/Messaging/Commands/MarkGroupMessagesSeen/MarkGroupMessagesSeenCommand.cs`
```csharp
public record MarkGroupMessagesSeenCommand(Guid GroupId, Guid UserId, Guid LastSeenMessageId)
    : IRequest<Result>;
```

**File:** Handler:
- Verify group membership
- Update `member.LastSeenMessageId = cmd.LastSeenMessageId`
- `SaveChangesAsync`
- `_notifier.NotifyMessageSeenAsync(groupId, otherMemberIds, userId, lastSeenMessageId, ct)`

#### Bước 4 — Endpoint

Thêm vào `MessageGroupsController`:
```
PATCH /api/v1/message-groups/{id:guid}/seen
Body: { "lastSeenMessageId": "guid" }
```

#### Acceptance Criteria
- [ ] 4 unit test GREEN
- [ ] `LastSeenMessageId` được persist vào DB
- [ ] Event `message:seen` chỉ push đến các member khác (không phải người gọi endpoint)

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] Manual E2E test với Postman:
  - [ ] Connect WebSocket `wss://localhost/hubs/beacon?access_token=<jwt>`
  - [ ] UserA gửi friend request → UserB nhận `notification:new`
  - [ ] UserA gửi message vào group → các member nhận `message:new`
  - [ ] UserA typing → các member nhận `message:typing`
  - [ ] UserA đọc message → các member nhận `message:seen`
- [ ] `/review` — 5-axis code review
- [ ] `/security-review` — audit auth coverage và sensitive data

---

## Thứ tự thực hiện tối ưu

```
Slice 0.1 → 0.2
                ↓
Slice 1.4 → 1.1 → 1.2 → 1.3      (có thể song song 1.4 + 1.1)
                ↓
Slice 2.1 → 2.2 → 2.3 → 2.4      (2.1/2.2/2.3 độc lập, làm tuần tự để dễ test)
                ↓
Slice 3.1                          (sau khi có IRealtimeNotifier từ 0.2)
                ↓
Slice 4.1 → 4.2 → 4.3             (4.1 và 4.2 độc lập nhau)
                ↓
Slice 5.1 → 5.2                   (5.1 và 5.2 độc lập nhau)
```

---

## File Structure sau khi hoàn thành

```
src/
├── Beacon.Domain/
│   ├── Entities/Group/Notification.cs                          ← Slice 1.1
│   ├── Enums/Group/NotificationType.cs                         ← Slice 1.1
│   └── IRepository/Group/INotificationRepository.cs           ← Slice 1.1
│
├── Beacon.Application/
│   ├── Common/Interfaces/IHubs/IBeaconHub.cs                   ← Slice 0.2
│   ├── Common/Interfaces/IService/IRealtimeNotifier.cs         ← Slice 0.2
│   ├── Common/Interfaces/IService/NotificationPayload.cs       ← Slice 0.2
│   └── Features/Group/
│       ├── Commands/MarkNotificationRead/                      ← Slice 2.2
│       ├── Commands/MarkAllNotificationsRead/                  ← Slice 2.3
│       ├── Queries/ListNotifications/                          ← Slice 2.1
│       ├── Validators/Group/ListNotificationsQueryValidator.cs ← Slice 2.1
│       └── Dtos/NotificationDto.cs                            ← Slice 2.1
│
├── Beacon.Infrashtructure/
│   ├── Presistence/Configuration/Group/NotificationConfiguration.cs ← Slice 1.2
│   ├── Repository/Group/NotificationRepository.cs             ← Slice 1.2
│   └── Services/SignalRRealtimeNotifier.cs                    ← Slice 3.1
│
├── Beacon.Api/
│   ├── Controllers/Group/NotificationsController.cs           ← Slice 2.4
│   ├── Hubs/BeaconHub.cs                                      ← Slice 0.1
│   └── Extensions/SignalRExtensions.cs                        ← Slice 0.1
│
└── Beacon.Shared/Constants/ErrorCodes.cs                      ← Slice 1.4 (sửa)

tests/
├── Beacon.UnitTests/Group/
│   ├── NotificationEntityTests.cs                             ← Slice 1.1
│   ├── ListNotificationsQueryHandlerTests.cs                  ← Slice 2.1
│   ├── MarkNotificationReadCommandHandlerTests.cs             ← Slice 2.2
│   ├── MarkAllNotificationsReadCommandHandlerTests.cs         ← Slice 2.3
│   └── SignalRRealtimeNotifierTests.cs                        ← Slice 3.1
├── Beacon.UnitTests/Messaging/
│   ├── UpdateTypingStatusCommandHandlerTests.cs               ← Slice 5.1
│   └── MarkGroupMessagesSeenCommandHandlerTests.cs            ← Slice 5.2
└── Beacon.IntergrationTests/Group/
    └── NotificationsControllerTests.cs                        ← Slice 2.4
```
