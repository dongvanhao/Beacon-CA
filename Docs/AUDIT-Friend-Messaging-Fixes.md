# AUDIT: Friend / Messaging / Group — Fix List & SignalR Prep

> Generated từ technical audit. Thứ tự = thứ tự thực hiện khuyến nghị.
> Mỗi item có: **Vấn đề**, **File liên quan**, **Cách fix cụ thể**, **Acceptance criteria**.

---

## Mục lục

- [Phase 1 — Critical Bugs](#phase-1--critical-bugs)
- [Phase 2 — Avatar + N+1 Fix](#phase-2--avatar--n1-fix)
- [Phase 3 — Group Chat Foundation](#phase-3--group-chat-foundation)
- [Phase 4 — Domain Events (SignalR Prep)](#phase-4--domain-events-signalr-prep)
- [Phase 5 — SignalR Integration](#phase-5--signalr-integration)
- [Phase 6 — Scale + Production Hardening](#phase-6--scale--production-hardening)

---

## Phase 1 — Critical Bugs

### BUG-1: `FriendRequest.ReceiverId` không có FK constraint

**Vấn đề:** `FriendRequestConfiguration` chỉ config FK cho `SenderId`. `ReceiverId` không có `HasOne` → không có FK trong DB → có thể insert FriendRequest trỏ tới user không tồn tại mà DB không bắt lỗi.

**Files cần sửa:**
- `src/Beacon.Domain/Entities/Group/FriendRequest.cs`
- `src/Beacon.Infrashtructure/Presistence/Configuration/Group/FriendRequestConfiguration.cs`

**Cách fix:**

```csharp
// FriendRequest.cs — thêm navigation property
public class FriendRequest : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public FriendRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User Sender { get; set; } = null!;
    public User Receiver { get; set; } = null!;   // ← thêm
}

// FriendRequestConfiguration.cs — thêm FK + explicit Status conversion
public void Configure(EntityTypeBuilder<FriendRequest> b)
{
    b.ToTable("FriendRequests");
    b.HasKey(r => r.Id);
    b.Property(r => r.Status).IsRequired().HasConversion<int>();   // ← thêm explicit
    b.Property(r => r.CreatedAtUtc).IsRequired();

    b.HasOne(r => r.Sender).WithMany()
     .HasForeignKey(r => r.SenderId).OnDelete(DeleteBehavior.Restrict);

    b.HasOne(r => r.Receiver).WithMany()                           // ← thêm
     .HasForeignKey(r => r.ReceiverId).OnDelete(DeleteBehavior.Restrict);

    // Indexes — xem BUG-5 bên dưới
}
```

**Migration:** Tạo migration `Add_FriendRequest_ReceiverFK`.

**Acceptance:** `dotnet ef migrations add` không báo lỗi; DB có FK `FK_FriendRequests_Users_ReceiverId`.

---

### BUG-2: `SendFriendRequest` không validate receiver tồn tại

**Vấn đề:** `SendFriendRequestCommandHandler` không kiểm tra `ReceiverId` có phải user hợp lệ không. Sau BUG-1 fix có FK nên DB sẽ bắt, nhưng sẽ throw `DbUpdateException` → 500 thay vì 404 đẹp.

**Files cần sửa:**
- `src/Beacon.Application/Features/Group/Commands/SendFriendRequest/SendFriendRequestCommandHandler.cs`
- `src/Beacon.Domain/IRepository/IUserRepository.cs` — thêm method nếu chưa có

**Cách fix:**

```csharp
// IUserRepository.cs — thêm nếu chưa có
Task<bool> ExistsAsync(Guid userId, CancellationToken ct = default);

// SendFriendRequestCommandHandler.cs — thêm check sau self-request check
if (!await _userRepo.ExistsAsync(command.ReceiverId, ct))
    return Result<FriendRequestDto>.Failure(
        Error.NotFound(ErrorCodes.USER_NOT_FOUND, "Người dùng không tồn tại."));
```

**Constructor:** Inject `IUserRepository userRepo`.

**Acceptance:** `POST /api/v1/friend-requests` với `receiverId = random Guid` → 404 với code `USER_NOT_FOUND`.

---

### BUG-3 & BUG-4: Avatar URL luôn `null` trong `ListFriends` và `ListFriendRequests`

> Hai bug cùng nguyên nhân — fix trong Phase 2 vì cần `IStorageService` inject + batch presign. Xem [Phase 2](#phase-2--avatar--n1-fix).

---

### BUG-5: Missing indexes trên `FriendRequests`

**Vấn đề:** `HasPendingBetweenAsync`, `ListReceivedAsync`, `ListSentAsync` thực hiện full scan `FriendRequests` vì không có composite index.

**File cần sửa:**
- `src/Beacon.Infrashtructure/Presistence/Configuration/Group/FriendRequestConfiguration.cs`

**Cách fix:**

```csharp
// Thêm vào Configure()
b.HasIndex(r => new { r.SenderId, r.ReceiverId, r.Status })
 .HasDatabaseName("IX_FriendRequests_Peers_Status");

b.HasIndex(r => new { r.ReceiverId, r.Status, r.CreatedAtUtc })
 .HasDatabaseName("IX_FriendRequests_Receiver_Status_CreatedAt");

b.HasIndex(r => new { r.SenderId, r.Status, r.CreatedAtUtc })
 .HasDatabaseName("IX_FriendRequests_Sender_Status_CreatedAt");
```

**Migration:** Có thể gộp chung với BUG-1 migration.

**Acceptance:** `EXPLAIN` / SQL Server Execution Plan không có "Table Scan" trên `FriendRequests`.

---

### BUG-6: Race condition trong `AcceptFriendRequestCommandHandler`

**Vấn đề:** Hai request đồng thời accept cùng `FriendRequestId` đều pass check `Status == Pending` trước khi SaveChanges, dẫn đến unique constraint violation trên `Friends(UserId1, UserId2)` → 500.

**File cần sửa:**
- `src/Beacon.Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommandHandler.cs`

**Cách fix — Option A: Explicit transaction + catch (recommended):**

```csharp
// Inject AppDbContext qua IDbContextFactory hoặc wrap trong repository transaction method
// Đơn giản hơn: catch DbUpdateException

public async Task<Result> Handle(AcceptFriendRequestCommand command, CancellationToken ct)
{
    var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
    if (request is null)
        return Result.Failure(Error.NotFound(...));

    if (request.ReceiverId != currentUser.UserId)
        return Result.Failure(Error.Forbidden(...));

    if (request.Status != FriendRequestStatus.Pending)
        return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời không còn ở trạng thái chờ."));

    var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
    await groupRepo.AddAsync(group, ct);

    var (u1, u2) = FriendPair.Normalize(request.SenderId, request.ReceiverId); // xem BUG-7
    var friend = new Friend
    {
        UserId1 = u1, UserId2 = u2,
        Type = FriendType.Normal,
        MessageGroupId = group.Id,
        CreatedAtUtc = DateTime.UtcNow
    };
    await friendRepo.AddAsync(friend, ct);
    group.Members.Add(new MessageGroupMember { GroupId = group.Id, UserId = request.SenderId });
    group.Members.Add(new MessageGroupMember { GroupId = group.Id, UserId = request.ReceiverId });
    request.Status = FriendRequestStatus.Accepted;

    try
    {
        await requestRepo.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
    {
        return Result.Failure(Error.Conflict(
            ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING,
            "Lời mời đã được xử lý bởi một yêu cầu khác."));
    }

    return Result.Success();
}

private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    => ex.InnerException?.Message.Contains("UNIQUE") == true
    || ex.InnerException?.Message.Contains("unique") == true;
```

> **Lưu ý:** Import `Microsoft.EntityFrameworkCore` vào Application handler vi phạm Clean Architecture nếu dùng `DbUpdateException`. Thay thế sạch hơn: định nghĩa `IDuplicateEntryException` trong Application và wrap trong repository. Hoặc dùng `IFriendRepository.TryAddAsync` returning bool.

**Cách fix — Option B (cleaner):**

Thêm `IFriendRepository.TryAddAsync` trả `bool` — infrastructure tự catch unique violation:

```csharp
// IFriendRepository — thêm method
Task<bool> TryAddAsync(Friend friend, CancellationToken ct = default);

// FriendRepository.cs — implement
public async Task<bool> TryAddAsync(Friend friend, CancellationToken ct)
{
    try { await db.Friends.AddAsync(friend, ct); return true; }
    catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { return false; }
}

// Handler — dùng TryAddAsync
if (!await friendRepo.TryAddAsync(friend, ct))
    return Result.Failure(Error.Conflict(...));
```

**Acceptance:** Gọi accept cùng lúc 2 requests → 1 thành công (200), 1 trả về 409.

---

### BUG-7: Duplicated `FriendPair.Normalize` logic

**Vấn đề:** Pattern `(u1, u2) = a < b ? (a, b) : (b, a)` xuất hiện ở `AcceptFriendRequestCommandHandler` và `FriendRepository`. Dễ implement sai ở chỗ thứ 3.

**File cần tạo:**
- `src/Beacon.Domain/Entities/Group/FriendPair.cs`

**Cách fix:**

```csharp
namespace Beacon.Domain.Entities.Group;

public static class FriendPair
{
    /// <summary>Normalize pair so UserId1 = Min, UserId2 = Max — matches DB unique index.</summary>
    public static (Guid UserId1, Guid UserId2) Normalize(Guid a, Guid b)
        => a < b ? (a, b) : (b, a);
}
```

Thay thế tất cả inline tuple normalization bằng `FriendPair.Normalize(...)`.

---

### BUG-8: Missing `CancelFriendRequest` use case

**Vấn đề:** Spec yêu cầu "Hủy lời mời đã gửi" nhưng không có command, handler, hoặc endpoint.

**Files cần tạo:**
- `src/Beacon.Application/Features/Group/Commands/CancelFriendRequest/CancelFriendRequestCommand.cs`
- `src/Beacon.Application/Features/Group/Commands/CancelFriendRequest/CancelFriendRequestCommandHandler.cs`
- `src/Beacon.Application/Features/Group/Validators/Group/CancelFriendRequestCommandValidator.cs`

**File cần sửa:**
- `src/Beacon.Api/Controllers/Group/FriendRequestsController.cs`

**Cách fix:**

```csharp
// CancelFriendRequestCommand.cs
public record CancelFriendRequestCommand(Guid RequestId) : IRequest<Result>;

// CancelFriendRequestCommandHandler.cs
public class CancelFriendRequestCommandHandler(
    IFriendRequestRepository requestRepo,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelFriendRequestCommand, Result>
{
    public async Task<Result> Handle(CancelFriendRequestCommand command, CancellationToken ct)
    {
        var request = await requestRepo.GetByIdAsync(command.RequestId, ct);
        if (request is null)
            return Result.Failure(Error.NotFound(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND, "Lời mời không tồn tại."));

        if (request.SenderId != currentUser.UserId)
            return Result.Failure(Error.Forbidden(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN, "Chỉ người gửi mới có thể hủy lời mời."));

        if (request.Status != FriendRequestStatus.Pending)
            return Result.Failure(Error.Conflict(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING, "Lời mời không còn ở trạng thái chờ."));

        request.Status = FriendRequestStatus.Cancelled;
        await requestRepo.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// FriendRequestsController.cs — thêm endpoint
/// <summary>Hủy lời mời kết bạn đã gửi.</summary>
[HttpDelete("{id:guid}")]
public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(new CancelFriendRequestCommand(id), ct));
```

**Domain:** Thêm `Cancelled` vào `FriendRequestStatus` enum.

**Acceptance:** `DELETE /api/v1/friend-requests/{id}` với sender token → 200; với receiver token → 403; sau khi cancel → 409.

---

## Phase 2 — Avatar + N+1 Fix

### FIX-1: N+1 trong `FindUserByPhoneQueryHandler`

**Vấn đề:** Với 10 kết quả: 1 search + 10×2 status queries + 10 MinIO presign calls = 21 DB roundtrips + 10 external calls trong vòng lặp.

**File cần sửa:**
- `src/Beacon.Application/Features/Group/Queries/FindUserByPhone/FindUserByPhoneQueryHandler.cs`
- `src/Beacon.Domain/IRepository/Group/IFriendRepository.cs`
- `src/Beacon.Domain/IRepository/Group/IFriendRequestRepository.cs`

**Cách fix — Batch status lookup:**

```csharp
// IFriendRepository.cs — thêm
Task<HashSet<Guid>> GetFriendIdsAsync(Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct);

// IFriendRequestRepository.cs — thêm
Task<Dictionary<Guid, FriendRequest>> GetPendingBetweenBatchAsync(
    Guid userId, IEnumerable<Guid> targetIds, CancellationToken ct);

// FindUserByPhoneQueryHandler.cs — rewrite
public async Task<Result<List<UserSearchDto>>> Handle(FindUserByPhoneQuery query, CancellationToken ct)
{
    var users = await userRepo.SearchByNameOrPhoneAsync(query.Search, currentUser.UserId, query.Limit, ct);
    if (users.Count == 0) return Result<List<UserSearchDto>>.Success([]);

    var targetIds = users.Select(u => u.Id).ToList();

    // 1 query thay vì N×2
    var friendIds = await friendRepo.GetFriendIdsAsync(currentUser.UserId, targetIds, ct);
    var pendingRequests = await friendRequestRepo.GetPendingBetweenBatchAsync(currentUser.UserId, targetIds, ct);

    // Batch presign avatars
    var avatarObjects = users
        .Where(u => u.AvatarMediaObject is not null)
        .Select(u => u.AvatarMediaObject!)
        .ToList();
    var urlMap = avatarObjects.Count > 0
        ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct)).ToDictionary(x => x.Media.Id, x => x.Url)
        : new Dictionary<Guid, string>();

    var results = users.Select(user =>
    {
        var status = ResolveStatus(user.Id, friendIds, pendingRequests);
        var avatarUrl = user.AvatarMediaObjectId.HasValue
            && urlMap.TryGetValue(user.AvatarMediaObjectId.Value, out var url) ? url : null;
        return new UserSearchDto(user.Id, user.FamilyName, user.GivenName, avatarUrl, status);
    }).ToList();

    return Result<List<UserSearchDto>>.Success(results);
}

private FriendshipStatus ResolveStatus(
    Guid targetId,
    HashSet<Guid> friendIds,
    Dictionary<Guid, FriendRequest> pendingRequests)
{
    if (friendIds.Contains(targetId)) return FriendshipStatus.Friends;
    if (!pendingRequests.TryGetValue(targetId, out var req)) return FriendshipStatus.None;
    return req.SenderId == currentUser.UserId ? FriendshipStatus.PendingSent : FriendshipStatus.PendingReceived;
}
```

**Acceptance:** Với 10 results: tổng DB queries = 3 (search + friendIds + pendingBatch) + 1 batch presign.

---

### FIX-2: Avatar luôn `null` trong `ListFriends`

**Vấn đề:** `FriendRepository.ListByUserAsync` không include `AvatarMediaObject`; handler pass `null` cứng.

**Files cần sửa:**
- `src/Beacon.Infrashtructure/Repository/Group/FriendRepository.cs`
- `src/Beacon.Application/Features/Group/Queries/ListFriends/ListFriendsQueryHandler.cs`

**Cách fix:**

```csharp
// FriendRepository.ListByUserAsync — thêm ThenInclude
var query = db.Friends
    .AsNoTracking()
    .Include(f => f.User1).ThenInclude(u => u.AvatarMediaObject)  // ← thêm
    .Include(f => f.User2).ThenInclude(u => u.AvatarMediaObject)  // ← thêm
    .Where(f => f.UserId1 == userId || f.UserId2 == userId)
    ...

// ListFriendsQueryHandler.cs — thêm IStorageService + batch presign
public class ListFriendsQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    IStorageService storage,   // ← thêm
    FriendMapper mapper)
    ...

public async Task<Result<CursorPagedResult<FriendDto>>> Handle(...)
{
    ...
    var avatarObjects = paged.Data
        .Select(f => f.GetOtherUser(userId).AvatarMediaObject)
        .Where(a => a is not null).Select(a => a!).ToList();

    var urlMap = avatarObjects.Count > 0
        ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct)).ToDictionary(x => x.Media.Id, x => x.Url)
        : new Dictionary<Guid, string>();

    var dtos = paged.Data.Select(f =>
    {
        var other = f.GetOtherUser(userId);
        var avatarUrl = other.AvatarMediaObjectId.HasValue
            && urlMap.TryGetValue(other.AvatarMediaObjectId.Value, out var url) ? url : null;
        return mapper.ToDto(f, userId, other.FamilyName, other.GivenName, avatarUrl);
    }).ToList();
    ...
}
```

**Acceptance:** `GET /api/v1/friends` trả `avatarUrl` đúng (signed URL hoặc null nếu chưa có avatar).

---

### FIX-3: Avatar luôn `null` trong `ListReceivedFriendRequests` và `ListSentFriendRequests`

**Files cần sửa:**
- `src/Beacon.Infrashtructure/Repository/Group/FriendRequestRepository.cs`
- `src/Beacon.Application/Features/Group/Queries/ListReceivedFriendRequests/ListReceivedFriendRequestsQueryHandler.cs`
- `src/Beacon.Application/Features/Group/Queries/ListSentFriendRequests/ListSentFriendRequestsQueryHandler.cs`

**Cách fix (giống FIX-2 — batch presign):**

```csharp
// FriendRequestRepository — thêm ThenInclude cho AvatarMediaObject
.Include(r => r.Sender).ThenInclude(u => u.AvatarMediaObject)

// Handler — thêm IStorageService, batch presign từ Sender.AvatarMediaObject
var avatarObjects = paged.Data
    .Where(r => r.Sender.AvatarMediaObject is not null)
    .Select(r => r.Sender.AvatarMediaObject!)
    .ToList();
var urlMap = avatarObjects.Count > 0
    ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct)).ToDictionary(x => x.Media.Id, x => x.Url)
    : new Dictionary<Guid, string>();

var dtos = paged.Data.Select(r =>
{
    var avatarUrl = r.Sender.AvatarMediaObjectId.HasValue
        && urlMap.TryGetValue(r.Sender.AvatarMediaObjectId.Value, out var url) ? url : null;
    return mapper.ToDto(r, r.Sender.FamilyName, r.Sender.GivenName, avatarUrl);
}).ToList();
```

---

## Phase 3 — Group Chat Foundation

### FEAT-1: `MessageGroupMember` cần Role + JoinedAtUtc

**Vấn đề:** Không có cơ sở để implement group permissions nếu thiếu role field.

**Files cần sửa:**
- `src/Beacon.Domain/Entities/Messaging/MessageGroupMember.cs`
- `src/Beacon.Infrashtructure/Presistence/Configuration/Messaging/MessageGroupMemberConfiguration.cs`
- **Migration:** `Add_MessageGroupMember_RoleAndJoinedAt`

**Cách fix:**

```csharp
// MessageGroupMember.cs
public class MessageGroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public GroupMemberRole Role { get; set; }     // ← thêm
    public DateTime JoinedAtUtc { get; set; }     // ← thêm

    public MessageGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}

// GroupMemberRole.cs — tạo mới tại Domain/Enums/Messaging/
public enum GroupMemberRole { Member = 0, Owner = 1 }

// MessageGroupMemberConfiguration.cs — thêm
b.Property(m => m.Role).IsRequired().HasConversion<int>();
b.Property(m => m.JoinedAtUtc).IsRequired();
```

**Migration note:** Existing 1-1 chat members cần default value. Dùng `defaultValue: 1` (Owner) cho cả 2 bên trong chat 1-1 là acceptable vì 1-1 không có owner concept.

**Cập nhật `AcceptFriendRequestCommandHandler`:**
```csharp
group.Members.Add(new MessageGroupMember {
    GroupId = group.Id, UserId = request.SenderId,
    Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow
});
group.Members.Add(new MessageGroupMember {
    GroupId = group.Id, UserId = request.ReceiverId,
    Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow
});
```

---

### FEAT-2: Tạo Group Chat

**Files cần tạo:**
```
Application/Features/Messaging/Commands/CreateGroup/
  CreateGroupCommand.cs
  CreateGroupCommandHandler.cs
Application/Features/Messaging/Dtos/
  CreateGroupRequest.cs
Application/Features/Messaging/Validators/Messaging/
  CreateGroupCommandValidator.cs
```

**Command:**
```csharp
public record CreateGroupCommand(string Name, string? AvatarUrl) : IRequest<Result<MessageGroupDetailDto>>;
```

**Handler:**
```csharp
public async Task<Result<MessageGroupDetailDto>> Handle(CreateGroupCommand command, CancellationToken ct)
{
    var group = new MessageGroup
    {
        IsPrivate = false,
        Name = command.Name,
        AvatarUrl = command.AvatarUrl,
        CreatedAtUtc = DateTime.UtcNow
    };
    group.Members.Add(new MessageGroupMember
    {
        GroupId = group.Id,
        UserId = currentUser.UserId,
        Role = GroupMemberRole.Owner,
        JoinedAtUtc = DateTime.UtcNow
    });
    await groupRepo.AddAsync(group, ct);
    await groupRepo.SaveChangesAsync(ct);

    return Result<MessageGroupDetailDto>.Success(mapper.ToDetailDto(group, group.Name, group.AvatarUrl, ...));
}
```

**Controller endpoint:**
```csharp
// POST api/v1/message-groups
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateGroupRequest req, CancellationToken ct)
    => CreatedResult("api/v1/message-groups", await mediator.Send(new CreateGroupCommand(req.Name, req.AvatarUrl), ct));
```

**Validator:**
```csharp
RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
```

---

### FEAT-3: Add Member to Group

**Business rules:**
- Chỉ Owner mới được add member
- Không add người đã là member
- Không add vào group IsPrivate

**Command:** `AddGroupMemberCommand(Guid GroupId, Guid TargetUserId)`

**Handler logic:**
```csharp
var group = await groupRepo.GetByIdWithMembersAsync(command.GroupId, ct);
if (group is null) return Result.Failure(Error.NotFound(...));
if (group.IsPrivate) return Result.Failure(Error.Validation(..., "Không thể thêm thành viên vào chat 1-1."));

var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
if (callerMember?.Role != GroupMemberRole.Owner)
    return Result.Failure(Error.Forbidden(...));

if (group.Members.Any(m => m.UserId == command.TargetUserId))
    return Result.Failure(Error.Conflict(..., "Người dùng đã là thành viên."));

group.Members.Add(new MessageGroupMember {
    GroupId = group.Id, UserId = command.TargetUserId,
    Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow
});
await groupRepo.SaveChangesAsync(ct);
```

---

### FEAT-4: Remove Member / Leave Group

**Business rules:**
- Owner remove member: owner có thể remove bất kỳ member (trừ chính mình)
- Member leave: member tự remove mình — `LeaveGroupCommand(Guid GroupId)`
- Owner leave: phải transfer ownership trước hoặc là last member (group tự dissolve)

**`RemoveGroupMemberCommand(Guid GroupId, Guid TargetUserId)`** — chỉ Owner:
```csharp
if (command.TargetUserId == currentUser.UserId)
    return Result.Failure(Error.Validation(..., "Owner không thể tự remove — dùng LeaveGroup sau khi transfer ownership."));
```

**`LeaveGroupCommand(Guid GroupId)`** — any member:
```csharp
if (callerMember.Role == GroupMemberRole.Owner && group.Members.Count > 1)
    return Result.Failure(Error.Validation(..., "Owner phải transfer ownership trước khi rời nhóm."));

group.Members.Remove(callerMember);
if (!group.Members.Any()) // last member → soft delete group
    group.IsDeleted = true; // cần thêm IsDeleted vào MessageGroup
await groupRepo.SaveChangesAsync(ct);
```

---

### FEAT-5: Transfer Ownership

**`TransferOwnershipCommand(Guid GroupId, Guid NewOwnerUserId)`:**
```csharp
var callerMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.UserId);
if (callerMember?.Role != GroupMemberRole.Owner) return Forbidden;

var newOwner = group.Members.FirstOrDefault(m => m.UserId == command.NewOwnerUserId);
if (newOwner is null) return NotFound("Người dùng không phải thành viên nhóm.");

callerMember.Role = GroupMemberRole.Member;
newOwner.Role = GroupMemberRole.Owner;
await groupRepo.SaveChangesAsync(ct);
```

---

### FEAT-6: Update Group Name / Avatar

**`UpdateGroupCommand(Guid GroupId, string? Name, string? AvatarUrl)`** — chỉ Owner:
```csharp
if (group.IsPrivate)
    return Validation("Không thể đổi tên/avatar chat 1-1 qua endpoint này.");  // hoặc cho phép — business decision

if (command.Name is not null) group.Name = command.Name;
if (command.AvatarUrl is not null) group.AvatarUrl = command.AvatarUrl;
await groupRepo.SaveChangesAsync(ct);
```

---

### FEAT-7: Fix `IMessageGroupRepository.RemoveMembersAsync` — Leaky abstraction

**Vấn đề:** `RemoveMembersAsync(groupId, userId1, userId2)` encode business rule "exactly 2 users" vào repository interface.

**Files cần sửa:**
- `src/Beacon.Domain/IRepository/Messaging/IMessageGroupRepository.cs`
- `src/Beacon.Infrashtructure/Repository/Messaging/MessageGroupRepository.cs`
- `src/Beacon.Application/Features/Group/Commands/RemoveFriend/RemoveFriendCommandHandler.cs`

**Cách fix:**
```csharp
// Interface — thay thế
Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct);

// Implementation
public async Task RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
{
    var member = await db.MessageGroupMembers
        .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
    if (member is not null) db.MessageGroupMembers.Remove(member);
}

// RemoveFriendCommandHandler — gọi 2 lần
await groupRepo.RemoveMemberAsync(friend.MessageGroupId, friend.UserId1, ct);
await groupRepo.RemoveMemberAsync(friend.MessageGroupId, friend.UserId2, ct);
await friendRepo.DeleteAsync(friend, ct);
await friendRepo.SaveChangesAsync(ct);
```

---

### FEAT-8: `AppDbContext` vi phạm `ApplyConfigurationsFromAssembly` rule

**Vấn đề:** CLAUDE.md yêu cầu auto-discover nhưng `OnModelCreating` đang đăng ký từng configuration thủ công, với 2 namespace khác nhau (`Beacon.Infrastructure.*` vs `Beacon.Infrashtructure.*`).

**File cần sửa:**
- `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`

**Cách fix:** Thay thế toàn bộ các `modelBuilder.ApplyConfiguration(new ...)` bằng:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Auto-discover tất cả IEntityTypeConfiguration<T> trong assembly này
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // Soft-delete filters (không đặt trong config class)
    modelBuilder.Entity<UserDevice>().HasQueryFilter(x => !x.IsDeleted);
    modelBuilder.Entity<MediaObject>().HasQueryFilter(x => !x.IsDeleted);

    base.OnModelCreating(modelBuilder);
}
```

**Lưu ý:** Một số config đang ở namespace `Beacon.Infrastructure.*` (không typo) trong khi assembly là `Beacon.Infrashtructure`. Cần kiểm tra và di chuyển các file đó về cùng assembly trước khi switch.

---

## Phase 4 — Domain Events (SignalR Prep)

> Phase này là **prerequisite bắt buộc** trước khi thêm SignalR. Không làm phase này thì khi thêm SignalR sẽ coupling Application → Hub.

### PREP-1: Tạo `IDomainEvent` infrastructure

**Files cần tạo:**

```
src/Beacon.Domain/Common/IDomainEvent.cs
src/Beacon.Application/Common/Behaviours/DomainEventPublishingBehavior.cs
```

```csharp
// IDomainEvent.cs — Domain layer, zero dependency
using MediatR;
namespace Beacon.Domain.Common;
public interface IDomainEvent : INotification { }

// DomainEventPublishingBehavior.cs — Application layer
// Publish domain events sau khi handler thành công
public class DomainEventPublishingBehavior<TRequest, TResponse>(IPublisher publisher)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();
        // Events sẽ được published từ handler trực tiếp (xem PREP-2)
        return response;
    }
}
```

**Cách đơn giản hơn (không cần behavior):** Inject `IPublisher` vào handler và publish sau `SaveChangesAsync`:

```csharp
// Trong SendMessageCommandHandler
await messageRepo.SaveChangesAsync(ct);
await publisher.Publish(new MessageSentEvent(command.GroupId, dto), ct);  // fire-and-forget safe
return Result<MessageDto>.Success(dto);
```

---

### PREP-2: Define Domain Events

**File cần tạo:**
```
src/Beacon.Application/Events/Messaging/MessageSentEvent.cs
src/Beacon.Application/Events/Messaging/MessageRecalledEvent.cs
src/Beacon.Application/Events/Group/FriendRequestSentEvent.cs
src/Beacon.Application/Events/Group/FriendAcceptedEvent.cs
src/Beacon.Application/Events/Group/FriendRemovedEvent.cs
src/Beacon.Application/Events/Group/GroupMemberAddedEvent.cs
src/Beacon.Application/Events/Group/GroupMemberRemovedEvent.cs
```

```csharp
// MessageSentEvent.cs
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Common;

namespace Beacon.Application.Events.Messaging;

public record MessageSentEvent(Guid GroupId, IReadOnlyList<Guid> MemberIds, MessageDto Message)
    : IDomainEvent;

// FriendRequestSentEvent.cs
public record FriendRequestSentEvent(Guid ReceiverId, FriendRequestDto Request)
    : IDomainEvent;

// FriendAcceptedEvent.cs
public record FriendAcceptedEvent(Guid UserId1, Guid UserId2, Guid MessageGroupId)
    : IDomainEvent;
```

---

### PREP-3: Wire events vào handlers

**Handlers cần inject `IPublisher` và publish sau SaveChanges:**

| Handler | Event publish |
|---|---|
| `SendMessageCommandHandler` | `MessageSentEvent(groupId, memberIds, dto)` |
| `SendFriendRequestCommandHandler` | `FriendRequestSentEvent(receiverId, dto)` |
| `AcceptFriendRequestCommandHandler` | `FriendAcceptedEvent(u1, u2, groupId)` |
| `RemoveFriendCommandHandler` | `FriendRemovedEvent(u1, u2)` |
| `AddGroupMemberCommandHandler` | `GroupMemberAddedEvent(groupId, userId)` |
| `RemoveGroupMemberCommandHandler` | `GroupMemberRemovedEvent(groupId, userId)` |

**Lưu ý quan trọng:** `MessageSentEvent` cần `MemberIds` để broadcast đúng target. Handler cần load member list từ repo hoặc cache trước khi publish.

---

### PREP-4: `IConnectionTracker` interface

**File cần tạo:**
```
src/Beacon.Application/Common/Interfaces/IService/IConnectionTracker.cs
```

```csharp
namespace Beacon.Application.Common.Interfaces.IService;

public interface IConnectionTracker
{
    Task AddAsync(Guid userId, string connectionId, CancellationToken ct = default);
    Task RemoveAsync(string connectionId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetConnectionsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);
}
```

**Implementation sẽ làm ở Phase 5:**
- `InMemoryConnectionTracker` — single server, dev/staging
- `RedisConnectionTracker` — production multi-server (Phase 6)

---

## Phase 5 — SignalR Integration

### SIG-1: Cài đặt và cấu hình SignalR

```bash
# Không cần package thêm — Microsoft.AspNetCore.SignalR đã có trong ASP.NET Core 8
# Chỉ cần register
```

```csharp
// Program.cs (qua extension method)
builder.Services.AddSignalRInfrastructure();   // extension method mới

// Api/Extensions/SignalRExtensions.cs
public static IServiceCollection AddSignalRInfrastructure(this IServiceCollection services)
{
    services.AddSignalR();
    services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();
    return services;
}

// app.MapHub<ChatHub>("/hubs/chat");
```

---

### SIG-2: `ChatHub`

**File cần tạo:**
```
src/Beacon.Api/Hubs/ChatHub.cs
```

```csharp
[Authorize]
public class ChatHub(
    IConnectionTracker tracker,
    ICurrentUserService currentUser,
    IMessageGroupRepository groupRepo) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = currentUser.UserId;
        await tracker.AddAsync(userId, Context.ConnectionId);

        // Join tất cả groups của user
        var groups = await groupRepo.GetGroupIdsForUserAsync(userId, CancellationToken.None);
        foreach (var groupId in groups)
            await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());

        await Clients.Others.SendAsync("UserOnline", userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var userId = currentUser.UserId;
        await tracker.RemoveAsync(Context.ConnectionId);

        if (!await tracker.IsOnlineAsync(userId))
            await Clients.Others.SendAsync("UserOffline", userId);

        await base.OnDisconnectedAsync(ex);
    }

    public async Task SendTyping(Guid groupId)
    {
        // Broadcast typing — không qua MediatR (low-latency, stateless)
        await Clients.OthersInGroup(groupId.ToString())
            .SendAsync("UserTyping", new { groupId, userId = currentUser.UserId });
    }
}
```

**Cần thêm `IMessageGroupRepository.GetGroupIdsForUserAsync`:**
```csharp
Task<IReadOnlyList<Guid>> GetGroupIdsForUserAsync(Guid userId, CancellationToken ct);
// Implementation: db.MessageGroupMembers.Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync()
```

---

### SIG-3: Event Handlers (Broadcast Layer)

**Files cần tạo:**
```
src/Beacon.Application/Events/Messaging/MessageSentEventHandler.cs
src/Beacon.Application/Events/Group/FriendRequestSentEventHandler.cs
src/Beacon.Application/Events/Group/FriendAcceptedEventHandler.cs
```

**Tuy nhiên:** Handlers này cần `IHubContext<ChatHub>` — để giữ Application layer sạch, định nghĩa interface:

```csharp
// Application/Common/Interfaces/IService/IRealtimeNotifier.cs
public interface IRealtimeNotifier
{
    Task NotifyGroupAsync(Guid groupId, string eventName, object payload, CancellationToken ct);
    Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct);
    Task NotifyUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken ct);
}

// Infrastructure/Services/SignalRNotifier.cs — implement
public class SignalRNotifier(IHubContext<ChatHub> hub, IConnectionTracker tracker) : IRealtimeNotifier
{
    public Task NotifyGroupAsync(Guid groupId, string eventName, object payload, CancellationToken ct)
        => hub.Clients.Group(groupId.ToString()).SendAsync(eventName, payload, ct);

    public async Task NotifyUserAsync(Guid userId, string eventName, object payload, CancellationToken ct)
    {
        var connections = await tracker.GetConnectionsAsync(userId, ct);
        await hub.Clients.Clients(connections).SendAsync(eventName, payload, ct);
    }
}

// MessageSentEventHandler.cs
public class MessageSentEventHandler(IRealtimeNotifier notifier)
    : INotificationHandler<MessageSentEvent>
{
    public Task Handle(MessageSentEvent notification, CancellationToken ct)
        => notifier.NotifyGroupAsync(notification.GroupId, "MessageReceived", notification.Message, ct);
}

// FriendRequestSentEventHandler.cs
public class FriendRequestSentEventHandler(IRealtimeNotifier notifier)
    : INotificationHandler<FriendRequestSentEvent>
{
    public Task Handle(FriendRequestSentEvent notification, CancellationToken ct)
        => notifier.NotifyUserAsync(notification.ReceiverId, "FriendRequestReceived", notification.Request, ct);
}
```

---

### SIG-4: Client Events Specification

| Event name | Direction | Payload | Trigger |
|---|---|---|---|
| `MessageReceived` | Server → Client | `MessageDto` | Khi có tin nhắn mới |
| `MessageRecalled` | Server → Client | `{ groupId, messageId }` | Khi tin nhắn bị thu hồi |
| `UserTyping` | Server → Client | `{ groupId, userId }` | Khi user đang gõ |
| `UserOnline` | Server → Client | `userId` | Khi user kết nối |
| `UserOffline` | Server → Client | `userId` | Khi user disconnect |
| `FriendRequestReceived` | Server → Client | `FriendRequestDto` | Khi nhận lời mời |
| `FriendAccepted` | Server → Client | `{ friendId, messageGroupId }` | Khi lời mời được chấp nhận |
| `GroupMemberAdded` | Server → Client | `{ groupId, member }` | Khi thêm member vào group |
| `GroupMemberRemoved` | Server → Client | `{ groupId, userId }` | Khi member bị remove/leave |
| `SendTyping` | Client → Server | Hub method | User đang gõ |

---

## Phase 6 — Scale + Production Hardening

### SCALE-1: Redis backplane cho SignalR

```csharp
// Khi chạy multi-instance
builder.Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration["Redis:ConnectionString"]!);

// Replace InMemoryConnectionTracker → RedisConnectionTracker
// Key pattern: "beacon:connections:{userId}" → List<connectionId> (Redis Set)
// TTL: không set TTL — remove on disconnect
```

---

### SCALE-2: `MessageReadReceipt` — Seen status

**Migration:** Thêm bảng `MessageReadReceipts (GroupId, UserId, LastReadAtUtc)`, PK = `(GroupId, UserId)`.

**Command:** `MarkGroupAsReadCommand(Guid GroupId)`:
```csharp
// Upsert LastReadAtUtc cho (GroupId, currentUserId)
// Publish MessageGroupReadEvent → broadcast "MessageSeen" tới other members
```

---

### SCALE-3: Recall Message

**Migration:** Thêm `Message.IsRecalled (bool)`, `Message.RecalledAtUtc (DateTime?)`.

**Command:** `RecallMessageCommand(Guid MessageId)`:
```csharp
// Chỉ sender mới được recall
// Chỉ recall trong vòng 24h (business rule — configurable)
// Publish MessageRecalledEvent
```

---

### SCALE-4: Fix cursor datetime collision

**Vấn đề:** `WHERE CreatedAtUtc < cursor` bỏ sót messages cùng timestamp.

**Fix:** Compound cursor `(DateTime, Guid)`:
```csharp
// CursorMeta mở rộng
public record CursorMeta
{
    public string? NextCursor { get; init; }  // ISO datetime
    public Guid? NextCursorId { get; init; }  // tiebreaker
    ...
}

// Query
.Where(m => m.CreatedAtUtc < cursor || (m.CreatedAtUtc == cursor && m.Id < cursorId))
```

---

### SCALE-5: `ListByUserAsync` — Fix correlated subquery N+1

**Vấn đề:** 4 correlated subqueries trên Messages per group.

**Long-term fix:** Thêm `MessageGroup.LastMessageId (Guid? FK)` — denormalized, update khi có message mới:

```csharp
// MessageGroup.cs — thêm
public Guid? LastMessageId { get; set; }
public Message? LastMessage { get; set; }

// MessageGroupConfiguration.cs
b.HasOne(g => g.LastMessage).WithMany()
 .HasForeignKey(g => g.LastMessageId).OnDelete(DeleteBehavior.SetNull);

// SendMessageCommandHandler — thêm sau save
group.LastMessageId = message.Id;
```

`ListByUserAsync` chỉ cần `Include(g => g.LastMessage).ThenInclude(m => m.Sender)` — không có subquery.

---

## Checklist Tổng Hợp

### Phase 1 — Critical Bugs
- [ ] BUG-1: FK cho `FriendRequest.ReceiverId`
- [ ] BUG-2: Validate receiver exists trong `SendFriendRequest`
- [ ] BUG-5: Thêm 3 indexes trên `FriendRequests`
- [ ] BUG-6: Fix race condition `AcceptFriendRequest`
- [ ] BUG-7: Extract `FriendPair.Normalize`
- [ ] BUG-8: Implement `CancelFriendRequestCommand`
- [ ] Migration: `Add_FriendRequest_ReceiverFK_And_Indexes`

### Phase 2 — Avatar + N+1
- [ ] FIX-1: Batch status lookup trong `FindUserByPhone`
- [ ] FIX-2: Avatar URLs trong `ListFriends`
- [ ] FIX-3: Avatar URLs trong `ListFriendRequests` (received + sent)

### Phase 3 — Group Chat
- [ ] FEAT-1: `MessageGroupMember.Role` + `JoinedAtUtc` + migration
- [ ] FEAT-2: `CreateGroupCommand`
- [ ] FEAT-3: `AddGroupMemberCommand`
- [ ] FEAT-4: `RemoveGroupMemberCommand` + `LeaveGroupCommand`
- [ ] FEAT-5: `TransferOwnershipCommand`
- [ ] FEAT-6: `UpdateGroupCommand`
- [ ] FEAT-7: Refactor `RemoveMembersAsync` → `RemoveMemberAsync`
- [ ] FEAT-8: Switch `AppDbContext` sang `ApplyConfigurationsFromAssembly`

### Phase 4 — Domain Events (SignalR Prep)
- [ ] PREP-1: `IDomainEvent` interface
- [ ] PREP-2: Define tất cả event records
- [ ] PREP-3: Wire events vào handlers (inject `IPublisher`)
- [ ] PREP-4: `IConnectionTracker` interface + `InMemoryConnectionTracker`
- [ ] PREP-5: `IRealtimeNotifier` interface

### Phase 5 — SignalR
- [ ] SIG-1: Register SignalR + Hub routing
- [ ] SIG-2: `ChatHub` với OnConnected/OnDisconnected
- [ ] SIG-3: Event handlers (MessageSent, FriendRequestSent, FriendAccepted)
- [ ] SIG-4: `SignalRNotifier` implement `IRealtimeNotifier`
- [ ] SIG-5: `GetGroupIdsForUserAsync` trên repository

### Phase 6 — Scale
- [ ] SCALE-1: Redis backplane
- [ ] SCALE-2: `MessageReadReceipt` + `MarkGroupAsReadCommand`
- [ ] SCALE-3: `RecallMessageCommand`
- [ ] SCALE-4: Compound cursor `(DateTime, Guid)`
- [ ] SCALE-5: `MessageGroup.LastMessageId` denormalization
