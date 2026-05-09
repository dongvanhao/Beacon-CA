# REALTIME-NOTIFICATION-SPEC.md
# Feature: Realtime Notification & Chat với SignalR

**Phiên bản:** 1.0  
**Ngày:** 2026-05-09  
**Tác giả:** Senior .NET Backend Architect  
**Trạng thái:** Draft — chờ review

---

## 1. Mục tiêu

Tích hợp SignalR vào Beacon-CA để cung cấp:

1. **Realtime Notification** — push thông báo (friend request, group invite, system alert) tức thời đến client ngay sau khi sự kiện xảy ra, không cần polling.
2. **Realtime Chat** — push tin nhắn mới, typing indicator và seen status vào các group chat hiện có (module Messaging).

> Hệ thống REST API hiện tại **không thay đổi** — SignalR chỉ là kênh _push_ bổ sung, không thay thế REST.

---

## 2. Phạm vi

### Trong scope (MVP)

| # | Tính năng |
|---|---|
| 1 | SignalR Hub (`/hubs/beacon`) với JWT auth qua QueryString |
| 2 | Entity `Notification` + EF config + Repository + Migration |
| 3 | `IRealtimeNotifier` interface tại Application layer |
| 4 | `SignalRRealtimeNotifier` implement tại Infrastructure layer |
| 5 | REST APIs: List / MarkRead / MarkAllRead Notifications |
| 6 | Push event `notification:new` khi tạo FriendRequest, AcceptFriendRequest, GroupInvite |
| 7 | Push event `message:new` khi SendMessage thành công |
| 8 | Push event `message:typing` (client → server qua REST, server → client qua SignalR) |
| 9 | Push event `message:seen` khi user đọc tin nhắn |

### Ngoài scope

- Push Notification ngoài app (FCM/APNs) — sẽ dùng `NotificationDelivery` entity có sẵn trong sprint sau
- Presence system (online/offline tracking)
- Read receipts per-member (chỉ seen của người nhận cuối cùng trong MVP)
- Horizontal scale với Redis backplane (đánh dấu Tech Debt)

---

## 3. Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────┐
│  Client (Mobile / Web)                                  │
│  WebSocket (SignalR)          REST API (HTTP)           │
└────────────┬──────────────────────┬────────────────────┘
             │  receive events      │  send actions
             ▼                      ▼
┌─────────────────────────────────────────────────────────┐
│  Beacon.Api                                             │
│  ┌─────────────────┐    ┌──────────────────────────┐   │
│  │  BeaconHub.cs   │    │  NotificationsController  │   │
│  │  (SignalR Hub)  │    │  MessageGroupsController  │   │
│  └────────┬────────┘    └────────────┬─────────────┘   │
│           │ IHubContext               │ IMediator       │
└───────────┼───────────────────────────┼─────────────────┘
            │                           │
┌───────────┼───────────────────────────┼─────────────────┐
│  Beacon.Infrastructure                │                  │
│  ┌────────▼──────────────────────┐   │                  │
│  │  SignalRRealtimeNotifier      │   │                  │
│  │  implements IRealtimeNotifier │   │                  │
│  └───────────────────────────────┘   │                  │
└──────────────────────────────────────┼─────────────────┘
                                       │
┌──────────────────────────────────────▼─────────────────┐
│  Beacon.Application                                     │
│  ┌─────────────────────────────────────────────────┐   │
│  │  Handlers (MediatR)                             │   │
│  │  → after SaveChangesAsync                       │   │
│  │  → call IRealtimeNotifier.NotifyAsync(...)      │   │
│  └─────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

**Luồng dữ liệu (Golden Path — Send Message):**

```
Client                REST POST /api/v1/message-groups/{id}/messages
  → SendMessageCommandHandler → repo.Add(message) → SaveChangesAsync
  → _notifier.NotifyNewMessageAsync(groupId, messageDto)   ← Application gọi interface
    → SignalRRealtimeNotifier.NotifyNewMessageAsync(...)    ← Infrastructure implement
      → _hubContext.Clients.User(memberId)                 ← push đến từng member
        .SendAsync("message:new", payload)
Client WebSocket ← nhận event "message:new"
```

---

## 4. Domain Layer

### 4.1 Enum `NotificationType`

**File:** `src/Beacon.Domain/Enums/Group/NotificationType.cs`

```csharp
namespace Beacon.Domain.Enums.Group;

public enum NotificationType
{
    FriendRequest   = 1,
    FriendAccepted  = 2,
    GroupInvite     = 3,
    GroupMessage    = 4,
    System          = 99
}
```

### 4.2 Entity `Notification`

**File:** `src/Beacon.Domain/Entities/Group/Notification.cs`

```csharp
using Beacon.Domain.Common;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group;

public class Notification : AuditableEntity
{
    public Guid ReceiverUserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    // JSON string — flexible metadata (e.g. {"friendRequestId": "...", "senderId": "..."})
    public string? Data { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    // EF Core constructor
    private Notification() { }

    public static Notification Create(
        Guid receiverUserId,
        NotificationType type,
        string title,
        string body,
        string? data = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            ReceiverUserId = receiverUserId,
            Type = type,
            Title = title,
            Body = body,
            Data = data,
            IsRead = false
        };
    }

    public void MarkRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAtUtc = DateTime.UtcNow;
    }
}
```

> `AuditableEntity` cung cấp `Id`, `CreatedAtUtc`, `UpdatedAtUtc`.

### 4.3 Repository Interface

**File:** `src/Beacon.Domain/IRepository/Group/INotificationRepository.cs`

```csharp
using Beacon.Domain.Entities.Group;
using Beacon.Shared.Common.Pagination;

namespace Beacon.Domain.IRepository.Group;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cursor-paged list cho một receiver.</summary>
    Task<(IReadOnlyList<Notification> Items, bool HasNextPage)> ListByReceiverAsync(
        Guid receiverUserId,
        DateTime? cursor,
        int limit,
        CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid receiverUserId, CancellationToken ct = default);

    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Bulk mark-read — trả về số bản ghi bị ảnh hưởng.</summary>
    Task<int> MarkAllReadAsync(Guid receiverUserId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
```

---

## 5. Application Layer

### 5.1 Interface `IRealtimeNotifier`

**File:** `src/Beacon.Application/Common/Interfaces/IService/IRealtimeNotifier.cs`

```csharp
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Group.Dtos;

namespace Beacon.Application.Common.Interfaces.IService;

/// <summary>
/// Abstraction để Application layer phát lệnh push realtime mà không phụ thuộc SignalR.
/// Infrastructure inject IHubContext vào implement.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Push thông báo mới đến một user cụ thể.</summary>
    Task NotifyUserAsync(Guid receiverUserId, NotificationPayload payload, CancellationToken ct = default);

    /// <summary>Push tin nhắn mới đến tất cả member của group (trừ sender).</summary>
    Task NotifyNewMessageAsync(Guid groupId, IEnumerable<Guid> memberIds, MessageDto message, CancellationToken ct = default);

    /// <summary>Push trạng thái typing.</summary>
    Task NotifyTypingAsync(Guid groupId, IEnumerable<Guid> memberIds, Guid typingUserId, bool isTyping, CancellationToken ct = default);

    /// <summary>Push sự kiện đã xem tin nhắn.</summary>
    Task NotifyMessageSeenAsync(Guid groupId, IEnumerable<Guid> memberIds, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default);
}
```

**File:** `src/Beacon.Application/Common/Interfaces/IService/NotificationPayload.cs`

```csharp
using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Common.Interfaces.IService;

public record NotificationPayload(
    Guid NotificationId,
    NotificationType Type,
    string Title,
    string Body,
    string? Data,
    DateTime CreatedAtUtc
);
```

### 5.2 DTOs

**File:** `src/Beacon.Application/Features/Group/Dtos/NotificationDto.cs`

```csharp
using Beacon.Domain.Enums.Group;

namespace Beacon.Application.Features.Group.Dtos;

public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Body,
    string? Data,
    bool IsRead,
    DateTime? ReadAtUtc,
    DateTime CreatedAtUtc
);

public record NotificationListResponse(
    IReadOnlyList<NotificationDto> Items,
    string? NextCursor,
    bool HasNextPage,
    int UnreadCount
);

public record MarkReadResponse(int UnreadCount);
```

### 5.3 Queries & Commands

#### Query: ListNotifications

**Files:**
- `src/Beacon.Application/Features/Group/Queries/ListNotifications/ListNotificationsQuery.cs`
- `src/Beacon.Application/Features/Group/Queries/ListNotifications/ListNotificationsQueryHandler.cs`

```csharp
// ListNotificationsQuery.cs
public record ListNotificationsQuery(
    Guid CurrentUserId,
    DateTime? Cursor,
    int Limit
) : IRequest<Result<NotificationListResponse>>;

// ListNotificationsQueryHandler.cs
public class ListNotificationsQueryHandler(
    INotificationRepository _repo) : IRequestHandler<ListNotificationsQuery, Result<NotificationListResponse>>
{
    public async Task<Result<NotificationListResponse>> Handle(ListNotificationsQuery q, CancellationToken ct)
    {
        var limit = Math.Min(q.Limit, 50);
        var (items, hasNext) = await _repo.ListByReceiverAsync(q.CurrentUserId, q.Cursor, limit + 1, ct);
        var unread = await _repo.CountUnreadAsync(q.CurrentUserId, ct);

        var page = items.Take(limit).Select(MapToDto).ToList();
        var nextCursor = hasNext && page.Count == limit
            ? page.Last().CreatedAtUtc.ToString("O")
            : null;

        return Result.Success(new NotificationListResponse(page, nextCursor, hasNext, unread));
    }

    private static NotificationDto MapToDto(Notification n) => new(
        n.Id, n.Type, n.Title, n.Body, n.Data, n.IsRead, n.ReadAtUtc, n.CreatedAtUtc);
}
```

#### Command: MarkNotificationRead

**Files:**
- `src/Beacon.Application/Features/Group/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs`
- `src/Beacon.Application/Features/Group/Commands/MarkNotificationRead/MarkNotificationReadCommandHandler.cs`

```csharp
// MarkNotificationReadCommand.cs
public record MarkNotificationReadCommand(Guid NotificationId, Guid CurrentUserId)
    : IRequest<Result<MarkReadResponse>>;

// MarkNotificationReadCommandHandler.cs
public class MarkNotificationReadCommandHandler(
    INotificationRepository _repo) : IRequestHandler<MarkNotificationReadCommand, Result<MarkReadResponse>>
{
    public async Task<Result<MarkReadResponse>> Handle(MarkNotificationReadCommand cmd, CancellationToken ct)
    {
        var notification = await _repo.GetByIdAsync(cmd.NotificationId, ct);
        if (notification is null)
            return Result.Failure<MarkReadResponse>(Error.NotFound(ErrorCodes.NOTIFICATION_NOT_FOUND, "Notification not found"));

        if (notification.ReceiverUserId != cmd.CurrentUserId)
            return Result.Failure<MarkReadResponse>(Error.Forbidden(ErrorCodes.NOTIFICATION_FORBIDDEN, "Access denied"));

        notification.MarkRead();
        await _repo.SaveChangesAsync(ct);

        var unread = await _repo.CountUnreadAsync(cmd.CurrentUserId, ct);
        return Result.Success(new MarkReadResponse(unread));
    }
}
```

#### Command: MarkAllNotificationsRead

**Files:**
- `src/Beacon.Application/Features/Group/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommand.cs`
- `src/Beacon.Application/Features/Group/Commands/MarkAllNotificationsRead/MarkAllNotificationsReadCommandHandler.cs`

```csharp
public record MarkAllNotificationsReadCommand(Guid CurrentUserId) : IRequest<Result<MarkReadResponse>>;

public class MarkAllNotificationsReadCommandHandler(
    INotificationRepository _repo) : IRequestHandler<MarkAllNotificationsReadCommand, Result<MarkReadResponse>>
{
    public async Task<Result<MarkReadResponse>> Handle(MarkAllNotificationsReadCommand cmd, CancellationToken ct)
    {
        await _repo.MarkAllReadAsync(cmd.CurrentUserId, ct);
        return Result.Success(new MarkReadResponse(0));
    }
}
```

### 5.4 Validators

**File:** `src/Beacon.Application/Features/Group/Validators/Group/ListNotificationsQueryValidator.cs`

```csharp
public class ListNotificationsQueryValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 50);
    }
}
```

### 5.5 Tích hợp IRealtimeNotifier vào Handler hiện có

**SendMessageCommandHandler** — sau `SaveChangesAsync`:

```csharp
// Sau khi lưu message thành công:
var memberIds = group.Members
    .Where(m => m.UserId != cmd.SenderId)
    .Select(m => m.UserId);

await _notifier.NotifyNewMessageAsync(group.Id, memberIds, _mapper.ToDto(message), ct);
```

**SendFriendRequestCommandHandler** — sau `SaveChangesAsync`:

```csharp
var notification = Notification.Create(
    receiverUserId: cmd.TargetUserId,
    type: NotificationType.FriendRequest,
    title: "Lời mời kết bạn",
    body: $"{senderUser.FullName} đã gửi lời mời kết bạn",
    data: JsonSerializer.Serialize(new { friendRequestId = friendRequest.Id, senderId = cmd.SenderId })
);
await _notifRepo.AddAsync(notification, ct);
await _notifRepo.SaveChangesAsync(ct);

await _notifier.NotifyUserAsync(cmd.TargetUserId, new NotificationPayload(
    notification.Id, NotificationType.FriendRequest,
    notification.Title, notification.Body, notification.Data, notification.CreatedAtUtc), ct);
```

---

## 6. Infrastructure Layer

### 6.1 EF Core Configuration

**File:** `src/Beacon.Infrashtructure/Presistence/Configuration/Group/NotificationConfiguration.cs`

```csharp
using Beacon.Domain.Entities.Group;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beacon.Infrashtructure.Presistence.Configuration.Group;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications");
        b.HasKey(n => n.Id);

        b.Property(n => n.ReceiverUserId).IsRequired();
        b.Property(n => n.Type).IsRequired();
        b.Property(n => n.Title).IsRequired().HasMaxLength(256);
        b.Property(n => n.Body).IsRequired().HasMaxLength(1024);
        b.Property(n => n.Data).HasMaxLength(4000);
        b.Property(n => n.IsRead).IsRequired().HasDefaultValue(false);

        // Index thường xuyên query: list by receiver sorted by date
        b.HasIndex(n => new { n.ReceiverUserId, n.CreatedAtUtc })
            .HasDatabaseName("IX_Notifications_ReceiverUserId_CreatedAtUtc");

        // Index cho UnreadCount query
        b.HasIndex(n => new { n.ReceiverUserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_ReceiverUserId_IsRead");

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.ReceiverUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 6.2 Repository Implementation

**File:** `src/Beacon.Infrashtructure/Repository/Group/NotificationRepository.cs`

```csharp
using Beacon.Domain.Entities.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Infrashtructure.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Beacon.Infrashtructure.Repository.Group;

public class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<(IReadOnlyList<Notification> Items, bool HasNextPage)> ListByReceiverAsync(
        Guid receiverUserId, DateTime? cursor, int limit, CancellationToken ct)
    {
        var query = db.Notifications
            .Where(n => n.ReceiverUserId == receiverUserId);

        if (cursor.HasValue)
            query = query.Where(n => n.CreatedAtUtc < cursor.Value);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(ct);

        var hasNext = items.Count == limit;
        return (items, hasNext);
    }

    public Task<int> CountUnreadAsync(Guid receiverUserId, CancellationToken ct)
        => db.Notifications.CountAsync(n => n.ReceiverUserId == receiverUserId && !n.IsRead, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct)
        => await db.Notifications.AddAsync(notification, ct);

    public async Task<int> MarkAllReadAsync(Guid receiverUserId, CancellationToken ct)
        => await db.Notifications
            .Where(n => n.ReceiverUserId == receiverUserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);
}
```

### 6.3 SignalR Hub

**File:** `src/Beacon.Api/Hubs/BeaconHub.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Beacon.Api.Hubs;

/// <summary>
/// Chỉ dùng Hub này để CLIENT nhận event từ server.
/// Mọi hành động tạo dữ liệu phải đi qua REST API.
/// </summary>
[Authorize]
public class BeaconHub : Hub
{
    // Hub trống là đúng thiết kế:
    // - Client kết nối để nhận event (subscribe)
    // - Server push qua IHubContext<BeaconHub> (không qua Hub trực tiếp)
    // - Không có method nào để client gọi lên server qua Hub

    public override async Task OnConnectedAsync()
    {
        // IUserIdProvider tự ánh xạ ConnectionId → UserId từ claim
        // Không cần logic join group cá nhân
        await base.OnConnectedAsync();
    }
}
```

> **Anti-pattern đã phòng tránh:** Hub không có method `JoinPersonalRoom(userId)`. SignalR's built-in User Group (thông qua `IUserIdProvider`) tự quản lý mapping — chỉ cần `Clients.User(userId)` là đủ.

### 6.4 SignalRRealtimeNotifier

**File:** `src/Beacon.Infrashtructure/Services/SignalRRealtimeNotifier.cs`

```csharp
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Beacon.Infrashtructure.Services;

public class SignalRRealtimeNotifier(IHubContext<BeaconHub> hubContext) : IRealtimeNotifier
{
    public async Task NotifyUserAsync(Guid receiverUserId, NotificationPayload payload, CancellationToken ct)
    {
        await hubContext.Clients
            .User(receiverUserId.ToString())
            .SendAsync("notification:new", payload, ct);
    }

    public async Task NotifyNewMessageAsync(
        Guid groupId,
        IEnumerable<Guid> memberIds,
        MessageDto message,
        CancellationToken ct)
    {
        var tasks = memberIds.Select(memberId =>
            hubContext.Clients
                .User(memberId.ToString())
                .SendAsync("message:new", new { groupId, message }, ct));

        await Task.WhenAll(tasks);
    }

    public async Task NotifyTypingAsync(
        Guid groupId,
        IEnumerable<Guid> memberIds,
        Guid typingUserId,
        bool isTyping,
        CancellationToken ct)
    {
        var payload = new { groupId, userId = typingUserId, isTyping };
        var tasks = memberIds
            .Where(id => id != typingUserId)
            .Select(memberId =>
                hubContext.Clients
                    .User(memberId.ToString())
                    .SendAsync("message:typing", payload, ct));

        await Task.WhenAll(tasks);
    }

    public async Task NotifyMessageSeenAsync(
        Guid groupId,
        IEnumerable<Guid> memberIds,
        Guid seenByUserId,
        Guid lastSeenMessageId,
        CancellationToken ct)
    {
        var payload = new { groupId, seenByUserId, lastSeenMessageId, seenAtUtc = DateTime.UtcNow };
        var tasks = memberIds
            .Where(id => id != seenByUserId)
            .Select(memberId =>
                hubContext.Clients
                    .User(memberId.ToString())
                    .SendAsync("message:seen", payload, ct));

        await Task.WhenAll(tasks);
    }
}
```

> **Lưu ý quan trọng:** `IHubContext<BeaconHub>` được inject vào Infrastructure — nhưng `BeaconHub` nằm ở tầng Api. Để tránh vi phạm dependency, khai báo `interface IBeaconHub` (marker interface) tại Application hoặc dùng `IHubContext<Hub>` generic. Xem chi tiết tại Mục 8.

---

## 7. API Layer

### 7.1 JWT Authentication cho SignalR

**File:** `src/Beacon.Api/Extensions/AuthExtensions.cs` — thêm vào phần AddJwtBearer hiện có:

```csharp
builder.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        // WebSocket không hỗ trợ Authorization header
        // Client gửi: ws://host/hubs/beacon?access_token=<jwt>
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;

        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            context.Token = accessToken;

        return Task.CompletedTask;
    }
};
```

### 7.2 Program.cs — Đăng ký SignalR

```csharp
// Trong builder.Services (qua extension method)
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

// Map Hub (sau app.UseAuthentication / app.UseAuthorization)
app.MapHub<BeaconHub>("/hubs/beacon");
```

> Đóng gói `AddSignalR()` vào `AddApiSignalR(this IServiceCollection services)` trong `Api/Extensions/SignalRExtensions.cs`.

### 7.3 NotificationsController

**File:** `src/Beacon.Api/Controllers/Group/NotificationsController.cs`

```csharp
using Beacon.Application.Features.Group.Commands.MarkAllNotificationsRead;
using Beacon.Application.Features.Group.Commands.MarkNotificationRead;
using Beacon.Application.Features.Group.Queries.ListNotifications;

namespace Beacon.Api.Controllers.Group;

[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    #region
    /// <summary>Lấy danh sách thông báo của user hiện tại (cursor paged).</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: limit ngoài khoảng [1, 50].
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>{ "items": [...], "nextCursor": "ISO-8601|null", "hasNextPage": bool, "unreadCount": int }</code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var parsedCursor = DateTime.TryParse(cursor, out var dt) ? (DateTime?)dt : null;
        return HandleResult(await mediator.Send(
            new ListNotificationsQuery(currentUser.UserId, parsedCursor, limit), ct));
    }

    #region
    /// <summary>Đánh dấu một thông báo là đã đọc.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công. data = { "unreadCount": int }
    /// - <c>NOTIFICATION_NOT_FOUND</c>: Không tìm thấy thông báo.
    /// - <c>NOTIFICATION_FORBIDDEN</c>: Thông báo không thuộc user này.
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new MarkNotificationReadCommand(id, currentUser.UserId), ct));

    #region
    /// <summary>Đánh dấu toàn bộ thông báo là đã đọc.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công. data = { "unreadCount": 0 }
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        => HandleResult(await mediator.Send(new MarkAllNotificationsReadCommand(currentUser.UserId), ct));
}
```

---

## 8. Xử lý Dependency Direction — IHubContext trong Infrastructure

`BeaconHub` ở tầng Api, nhưng `SignalRRealtimeNotifier` ở tầng Infrastructure. Infrastructure không được import Api.

**Giải pháp:** Dùng marker interface tại Application.

```csharp
// Application/Common/Interfaces/IHubs/IBeaconHub.cs
namespace Beacon.Application.Common.Interfaces.IHubs;

/// <summary>Marker interface — dùng để Infrastructure inject IHubContext mà không phụ thuộc vào BeaconHub class.</summary>
public interface IBeaconHub { }
```

```csharp
// Api/Hubs/BeaconHub.cs
public class BeaconHub : Hub<IBeaconHub> { }

// Infrastructure/Services/SignalRRealtimeNotifier.cs
public class SignalRRealtimeNotifier(IHubContext<BeaconHub, IBeaconHub> hubContext)
```

> Nếu team muốn giữ đơn giản hơn: inject `IHubContext<Hub>` và dùng `hubContext.Clients.User(...)` generic. Cả hai đều valid.

---

## 9. Đăng ký Dependency Injection

**File:** `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

```csharp
// Thêm vào phần AddInfrastructure hiện có:
services.AddScoped<INotificationRepository, NotificationRepository>();
services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
```

**File:** `src/Beacon.Api/Extensions/SignalRExtensions.cs` (mới)

```csharp
using Beacon.Api.Hubs;

namespace Beacon.Api.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddApiSignalR(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false; // true chỉ cho Development
        });
        return services;
    }

    public static WebApplication MapSignalRHubs(this WebApplication app)
    {
        app.MapHub<BeaconHub>("/hubs/beacon");
        return app;
    }
}
```

**File:** `src/Beacon.Api/Program.cs`

```csharp
builder.Services.AddApiSignalR(); // Sau AddApiAuth

// ...

app.MapSignalRHubs(); // Sau app.UseAuthorization()
```

---

## 10. Error Codes

Thêm vào `src/Beacon.Shared/Constants/ErrorCodes.cs`:

```csharp
// Notification
public const string NOTIFICATION_NOT_FOUND = "NOTIFICATION_NOT_FOUND";
public const string NOTIFICATION_FORBIDDEN  = "NOTIFICATION_FORBIDDEN";
```

---

## 11. SignalR Event Dictionary (Giao thức với Frontend)

Tất cả event đều là **Server → Client** (one-way push). Client không gọi Hub method nào.

### 11.1 `notification:new`

Trigger: Bất kỳ sự kiện nào tạo Notification entity (FriendRequest, AcceptFriendRequest, GroupInvite...)

```json
{
  "notificationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "FriendRequest",
  "title": "Lời mời kết bạn",
  "body": "Nguyễn Văn A đã gửi lời mời kết bạn",
  "data": "{\"friendRequestId\": \"abc\", \"senderId\": \"xyz\"}",
  "createdAtUtc": "2026-05-09T10:30:00Z"
}
```

### 11.2 `message:new`

Trigger: `SendMessageCommandHandler` sau khi lưu message thành công.

```json
{
  "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": {
    "id": "msg-uuid",
    "senderId": "user-uuid",
    "senderName": "Nguyễn Văn A",
    "content": "Xin chào!",
    "mediaUrl": null,
    "createdAtUtc": "2026-05-09T10:30:00Z"
  }
}
```

### 11.3 `message:typing`

Trigger: Client gọi `PATCH /api/v1/message-groups/{id}/typing` (REST endpoint riêng) → Handler gọi `IRealtimeNotifier.NotifyTypingAsync`.

```json
{
  "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user-uuid",
  "isTyping": true
}
```

> **Lưu ý:** Typing indicator KHÔNG lưu database. Handler chỉ gọi `IRealtimeNotifier` rồi trả về `Result.Success`.

### 11.4 `message:seen`

Trigger: Client gọi `PATCH /api/v1/message-groups/{groupId}/seen` → Handler cập nhật `LastSeenMessageId` của member → gọi `IRealtimeNotifier.NotifyMessageSeenAsync`.

```json
{
  "groupId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seenByUserId": "user-uuid",
  "lastSeenMessageId": "msg-uuid",
  "seenAtUtc": "2026-05-09T10:31:00Z"
}
```

---

## 12. REST API Summary

| Method | Endpoint | Handler | Mô tả |
|--------|----------|---------|-------|
| `GET` | `/api/v1/notifications` | `ListNotificationsQueryHandler` | Cursor paged, trả UnreadCount |
| `PATCH` | `/api/v1/notifications/{id:guid}/read` | `MarkNotificationReadCommandHandler` | Đánh dấu 1 notification |
| `PATCH` | `/api/v1/notifications/read-all` | `MarkAllNotificationsReadCommandHandler` | Bulk mark-read |
| `PATCH` | `/api/v1/message-groups/{id:guid}/typing` | `UpdateTypingStatusCommandHandler` | Trigger `message:typing` event |
| `PATCH` | `/api/v1/message-groups/{id:guid}/seen` | `MarkGroupMessagesSeenCommandHandler` | Trigger `message:seen` event |

---

## 13. Migration Checklist

```bash
# Sau khi thêm DbSet<Notification> và NotificationConfiguration:
dotnet ef migrations add AddNotificationEntity \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

**Review migration trước khi apply** — kiểm tra:
- [ ] Table `Notifications` được tạo đúng cột
- [ ] Indexes `IX_Notifications_ReceiverUserId_CreatedAtUtc` và `IX_Notifications_ReceiverUserId_IsRead` xuất hiện
- [ ] FK đến `Users` table đúng
- [ ] Không có `DROP` ngoài ý muốn

---

## 14. Testing Strategy

### Unit Tests

| Test class | Handler | Scenarios |
|---|---|---|
| `ListNotificationsQueryHandlerTests` | `ListNotificationsQueryHandler` | Empty list, cursor pagination, unread count |
| `MarkNotificationReadCommandHandlerTests` | `MarkNotificationReadCommandHandler` | Happy path, not found, wrong owner |
| `MarkAllNotificationsReadCommandHandlerTests` | `MarkAllNotificationsReadCommandHandler` | 0 unread, N unread |

### Integration Tests

| Test class | Endpoint |
|---|---|
| `NotificationsControllerTests` | `GET /api/v1/notifications`, `PATCH /{id}/read`, `PATCH /read-all` |

### SignalR Tests (Unit — mock IHubContext)

```csharp
// Verify IRealtimeNotifier được gọi đúng từ SendMessageCommandHandler
var mockNotifier = new Mock<IRealtimeNotifier>();
// ... setup handler, call Handle()
mockNotifier.Verify(n => n.NotifyNewMessageAsync(
    It.Is<Guid>(id => id == groupId),
    It.IsAny<IEnumerable<Guid>>(),
    It.IsAny<MessageDto>(),
    It.IsAny<CancellationToken>()), Times.Once);
```

---

## 15. Tech Debt & Open Decisions

| # | Item | Ghi chú |
|---|---|---|
| 1 | **Redis Backplane** | Khi deploy multi-instance, `AddSignalR().AddStackExchangeRedis(...)` — cần cấu hình trước scale out |
| 2 | **Presence System** | Online/offline tracking cần `IConnectionMapping` — chưa implement |
| 3 | **Rate Limiting** trên Typing endpoint | Typing REST endpoint dễ bị spam — cần throttle 1 req/sec |
| 4 | **Retry + Fallback** | Nếu SignalR push thất bại, không có retry — notification vẫn lưu DB, client pull được qua REST |
| 5 | **Persistent connection cho mobile background** | Cần kiểm thử trên iOS (APNs push khi app background thay cho SignalR) |

---

## 16. File Structure Summary

```
src/
├── Beacon.Domain/
│   ├── Entities/Group/
│   │   └── Notification.cs                              ← NEW
│   ├── Enums/Group/
│   │   └── NotificationType.cs                          ← NEW
│   └── IRepository/Group/
│       └── INotificationRepository.cs                   ← NEW
│
├── Beacon.Application/
│   ├── Common/Interfaces/IService/
│   │   ├── IRealtimeNotifier.cs                         ← NEW
│   │   └── NotificationPayload.cs                       ← NEW
│   ├── Common/Interfaces/IHubs/
│   │   └── IBeaconHub.cs                                ← NEW (marker)
│   └── Features/Group/
│       ├── Commands/MarkNotificationRead/               ← NEW
│       ├── Commands/MarkAllNotificationsRead/           ← NEW
│       ├── Queries/ListNotifications/                   ← NEW
│       ├── Validators/Group/
│       │   └── ListNotificationsQueryValidator.cs       ← NEW
│       └── Dtos/
│           └── NotificationDto.cs                       ← NEW
│
├── Beacon.Infrashtructure/
│   ├── Presistence/Configuration/Group/
│   │   └── NotificationConfiguration.cs                 ← NEW
│   ├── Repository/Group/
│   │   └── NotificationRepository.cs                    ← NEW
│   └── Services/
│       └── SignalRRealtimeNotifier.cs                   ← NEW
│
└── Beacon.Api/
    ├── Controllers/Group/
    │   └── NotificationsController.cs                   ← NEW
    ├── Hubs/
    │   └── BeaconHub.cs                                 ← NEW
    └── Extensions/
        └── SignalRExtensions.cs                         ← NEW
```

---

## 17. Acceptance Criteria

- [ ] Client có thể kết nối `wss://host/hubs/beacon?access_token=<jwt>` và nhận event
- [ ] Khi A gửi friend request cho B → B nhận `notification:new` qua WebSocket ngay lập tức
- [ ] Khi A gửi tin nhắn trong group → các member khác nhận `message:new` trong < 500ms
- [ ] `GET /api/v1/notifications` trả đúng `unreadCount` và cursor paging
- [ ] `PATCH /api/v1/notifications/{id}/read` trả `unreadCount` giảm đúng 1
- [ ] Hub không có method nào để client gọi lên server (chỉ nhận event)
- [ ] Mọi unit test pass, integration test cover 3 endpoints notification
- [ ] Migration apply không lỗi, không DROP table ngoài ý muốn
