# Plan: Friend & Messaging

**Module**: Group + Messaging (đang ở trạng thái 🚧 Scaffolding — thư mục rỗng)
**Spec**: `docs/specs/friend-messaging.md`
**Phạm vi**: 4 phase, 14 slices

---

## Dependency Graph

```
Phase 0 (Foundation)
  ↓
Phase 1 (Friend Requests) ← requires Phase 0
  ↓
Phase 2 (Friend Management) ← requires Phase 1 (AcceptFriendRequest tạo Friend + Group)
  ↓
Phase 3 (Messaging) ← requires Phase 2 (RemoveFriend logic biết MessageGroupId)
```

---

## Phase 0: Foundation — Domain + Infrastructure

> Không chứa business logic. Chỉ scaffolding cho các phase sau.

---

### Slice 0.1: Error Codes + Enums

**Files thay đổi:**

- `src/Beacon.Shared/Constants/ErrorCodes.cs` — thêm 2 nested classes
- `src/Beacon.Domain/Enums/Group/FriendType.cs` — enum mới
- `src/Beacon.Domain/Enums/Group/FriendRequestStatus.cs` — enum mới

**Nội dung:**

```csharp
// Trong ErrorCodes (nested)
public static class Friend
{
    public const string SELF_FRIEND_REQUEST        = "SELF_FRIEND_REQUEST";
    public const string FRIEND_REQUEST_DUPLICATE   = "FRIEND_REQUEST_DUPLICATE";
    public const string ALREADY_FRIENDS            = "ALREADY_FRIENDS";
    public const string FRIEND_REQUEST_NOT_FOUND   = "FRIEND_REQUEST_NOT_FOUND";
    public const string FRIEND_REQUEST_NOT_PENDING = "FRIEND_REQUEST_NOT_PENDING";
    public const string FRIEND_REQUEST_FORBIDDEN   = "FRIEND_REQUEST_FORBIDDEN";
    public const string FRIEND_NOT_FOUND           = "FRIEND_NOT_FOUND";
}
public static class Messaging
{
    public const string MESSAGE_GROUP_NOT_FOUND = "MESSAGE_GROUP_NOT_FOUND";
    public const string MESSAGE_GROUP_FORBIDDEN = "MESSAGE_GROUP_FORBIDDEN";
}

// Enums
public enum FriendType { Family, CloseFriend, Normal, Custom }
public enum FriendRequestStatus { Pending, Accepted, Declined }
```

**Verify**: `dotnet build` — 0 error.
**Dependencies**: Không có.

---

### Slice 0.2: Domain Entities + Repository Interfaces

**Files tạo mới:**

```
Domain/Entities/Group/Friend.cs
Domain/Entities/Group/FriendRequest.cs
Domain/Entities/Messaging/MessageGroup.cs
Domain/Entities/Messaging/MessageGroupMember.cs
Domain/Entities/Messaging/Message.cs

Domain/IRepository/Group/IFriendRepository.cs
Domain/IRepository/Group/IFriendRequestRepository.cs
Domain/IRepository/Messaging/IMessageGroupRepository.cs
Domain/IRepository/Messaging/IMessageRepository.cs
```

**Entity — Friend:**
```csharp
public class Friend : BaseEntity
{
    public Guid UserId1 { get; set; }        // Min(senderId, receiverId)
    public Guid UserId2 { get; set; }        // Max(senderId, receiverId)
    public FriendType Type { get; set; }
    public Guid MessageGroupId { get; set; } // FK — set khi AcceptFriendRequest
    public DateTime CreatedAtUtc { get; set; }
    public User User1 { get; set; } = null!;
    public User User2 { get; set; } = null!;
    public MessageGroup MessageGroup { get; set; } = null!;
}
```

**Entity — FriendRequest:**
```csharp
public class FriendRequest : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public FriendRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

**Entity — MessageGroup:**
```csharp
public class MessageGroup : BaseEntity
{
    public bool IsPrivate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<MessageGroupMember> Members { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
```

**Entity — MessageGroupMember (NO BaseEntity — composite PK):**
```csharp
public class MessageGroupMember
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public MessageGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
```

**Entity — Message:**
```csharp
public class Message : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public MessageGroup Group { get; set; } = null!;
    public User Sender { get; set; } = null!;
}
```

**Repository Interfaces — đầy đủ tất cả methods (kể cả query pagination):**

```csharp
// IFriendRepository
public interface IFriendRepository
{
    Task<Friend?> GetByUsersAsync(Guid userId1, Guid userId2, CancellationToken ct);
    Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct);
    Task<CursorPagedResult<Friend>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
    Task AddAsync(Friend friend, CancellationToken ct);
    Task DeleteAsync(Friend friend, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// IFriendRequestRepository
public interface IFriendRequestRepository
{
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct); // cả 2 chiều
    Task<CursorPagedResult<FriendRequest>> ListReceivedAsync(Guid receiverId, DateTime? cursor, int limit, CancellationToken ct);
    Task<CursorPagedResult<FriendRequest>> ListSentAsync(Guid senderId, DateTime? cursor, int limit, CancellationToken ct);
    Task AddAsync(FriendRequest req, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// IMessageGroupRepository
public interface IMessageGroupRepository
{
    Task<MessageGroup?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<CursorPagedResult<MessageGroupSummary>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct);
    Task AddAsync(MessageGroup group, CancellationToken ct);
    Task RemoveMembersAsync(Guid groupId, Guid userId1, Guid userId2, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// IMessageRepository
public interface IMessageRepository
{
    Task<CursorPagedResult<Message>> ListByGroupAsync(Guid groupId, DateTime? cursor, int limit, CancellationToken ct);
    Task AddAsync(Message message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

> `MessageGroupSummary` là internal projection record dùng trong `IMessageGroupRepository.ListByUserAsync` —
> chứa group info + last message preview. Định nghĩa trong `Domain/IRepository/Messaging/`.

**Verify**: `dotnet build` — 0 error.
**Dependencies**: Slice 0.1.

---

### Slice 0.3: EF Configurations + DbContext + Migration

**Files tạo mới:**

```
Infrashtructure/Presistence/Configuration/Group/FriendConfiguration.cs
Infrashtructure/Presistence/Configuration/Group/FriendRequestConfiguration.cs
Infrashtructure/Presistence/Configuration/Messaging/MessageGroupConfiguration.cs
Infrashtructure/Presistence/Configuration/Messaging/MessageGroupMemberConfiguration.cs
Infrashtructure/Presistence/Configuration/Messaging/MessageConfiguration.cs
```

**FriendConfiguration:**
```csharp
public class FriendConfiguration : IEntityTypeConfiguration<Friend>
{
    public void Configure(EntityTypeBuilder<Friend> b)
    {
        b.ToTable("Friends");
        b.HasKey(f => f.Id);
        b.HasIndex(f => new { f.UserId1, f.UserId2 }).IsUnique(); // canonical pair
        b.Property(f => f.Type).IsRequired();
        b.Property(f => f.CreatedAtUtc).IsRequired();
        b.HasOne(f => f.MessageGroup).WithMany()
         .HasForeignKey(f => f.MessageGroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(f => f.User1).WithMany()
         .HasForeignKey(f => f.UserId1).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(f => f.User2).WithMany()
         .HasForeignKey(f => f.UserId2).OnDelete(DeleteBehavior.Restrict);
    }
}
```

**FriendRequestConfiguration:**
```csharp
public class FriendRequestConfiguration : IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> b)
    {
        b.ToTable("FriendRequests");
        b.HasKey(r => r.Id);
        b.Property(r => r.Status).IsRequired();
        b.Property(r => r.CreatedAtUtc).IsRequired();
        // Không có DB unique — bidirectional check ở application layer
    }
}
```

**MessageGroupMemberConfiguration — composite PK:**
```csharp
public class MessageGroupMemberConfiguration : IEntityTypeConfiguration<MessageGroupMember>
{
    public void Configure(EntityTypeBuilder<MessageGroupMember> b)
    {
        b.ToTable("MessageGroupMembers");
        b.HasKey(m => new { m.GroupId, m.UserId }); // composite PK
        b.HasIndex(m => m.UserId);                   // IX_MessageGroupMembers_UserId — cho ListByUser
        b.HasOne(m => m.Group).WithMany(g => g.Members)
         .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.User).WithMany()
         .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

**MessageConfiguration:**
```csharp
public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Content).IsRequired().HasMaxLength(4000);
        b.Property(m => m.CreatedAtUtc).IsRequired();
        b.HasIndex(m => new { m.GroupId, m.CreatedAtUtc }); // IX_Messages_GroupId_CreatedAtUtc
        b.HasOne(m => m.Group).WithMany(g => g.Messages)
         .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.Sender).WithMany()
         .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

**AppDbContext — thêm 5 DbSet:**
```csharp
public DbSet<Friend> Friends => Set<Friend>();
public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
public DbSet<MessageGroup> MessageGroups => Set<MessageGroup>();
public DbSet<MessageGroupMember> MessageGroupMembers => Set<MessageGroupMember>();
public DbSet<Message> Messages => Set<Message>();
```

**Migration:**
```bash
dotnet ef migrations add Add_Friend_And_Messaging_Tables \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```
Review file migration — confirm không có DROP TABLE/COLUMN ngoài ý muốn.

**Verify**: `dotnet build` + `dotnet ef database update` — 0 error.
**Dependencies**: Slice 0.2.

---

### Slice 0.4: Repository Implementations + DI

**Files tạo mới:**

```
Infrashtructure/Repository/Group/FriendRepository.cs
Infrashtructure/Repository/Group/FriendRequestRepository.cs
Infrashtructure/Repository/Messaging/MessageGroupRepository.cs
Infrashtructure/Repository/Messaging/MessageRepository.cs
```

**FriendRepository — ví dụ GetByUsersAsync (canonicalize trước khi query):**
```csharp
public Task<Friend?> GetByUsersAsync(Guid userA, Guid userB, CancellationToken ct)
{
    var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
    return _db.Friends.FirstOrDefaultAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
}

public Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct)
{
    var (u1, u2) = userA < userB ? (userA, userB) : (userB, userA);
    return _db.Friends.AnyAsync(f => f.UserId1 == u1 && f.UserId2 == u2, ct);
}

public Task<CursorPagedResult<Friend>> ListByUserAsync(Guid userId, DateTime? cursor, int limit, CancellationToken ct)
{
    // WHERE UserId1 = userId OR UserId2 = userId
    // ORDER BY CreatedAtUtc DESC, cursor filter
}
```

**FriendRequestRepository — HasPendingBetweenAsync (2 chiều):**
```csharp
public Task<bool> HasPendingBetweenAsync(Guid userA, Guid userB, CancellationToken ct)
    => _db.FriendRequests.AnyAsync(r =>
        r.Status == FriendRequestStatus.Pending &&
        ((r.SenderId == userA && r.ReceiverId == userB) ||
         (r.SenderId == userB && r.ReceiverId == userA)), ct);
```

**MessageGroupRepository — RemoveMembersAsync:**
```csharp
public async Task RemoveMembersAsync(Guid groupId, Guid userId1, Guid userId2, CancellationToken ct)
{
    var members = await _db.MessageGroupMembers
        .Where(m => m.GroupId == groupId && (m.UserId == userId1 || m.UserId == userId2))
        .ToListAsync(ct);
    _db.MessageGroupMembers.RemoveRange(members);
}
```

**Đăng ký DI** trong `InfrastructureServiceExtensions.cs`:
```csharp
services.AddScoped<IFriendRepository, FriendRepository>();
services.AddScoped<IFriendRequestRepository, FriendRequestRepository>();
services.AddScoped<IMessageGroupRepository, MessageGroupRepository>();
services.AddScoped<IMessageRepository, MessageRepository>();
```

**Verify**: `dotnet build` — 0 error.
**Dependencies**: Slice 0.3.

---

### Slice 0.5: Mappers + DI

**Files tạo mới:**

```
Application/Mappings/Group/FriendRequestMapper.cs
Application/Mappings/Group/FriendMapper.cs
Application/Mappings/Messaging/MessageMapper.cs
Application/Mappings/Messaging/MessageGroupMapper.cs
```

**FriendRequestMapper:**
```csharp
public sealed class FriendRequestMapper
{
    public FriendRequestDto ToDto(FriendRequest r, string senderUsername, string? senderAvatarUrl)
        => new() { Id = r.Id, SenderId = r.SenderId, SenderUsername = senderUsername,
                   SenderAvatarUrl = senderAvatarUrl, CreatedAtUtc = r.CreatedAtUtc };
}
```

**FriendMapper:**
```csharp
public sealed class FriendMapper
{
    public FriendDto ToDto(Friend f, Guid currentUserId, string username, string? avatarUrl)
        => new() { UserId = currentUserId == f.UserId1 ? f.UserId2 : f.UserId1,
                   Username = username, AvatarUrl = avatarUrl,
                   Type = f.Type, CreatedAtUtc = f.CreatedAtUtc, MessageGroupId = f.MessageGroupId };
}
```

**Đăng ký Singleton** trong `ApplicationServiceExtensions.cs`:
```csharp
services.AddSingleton<FriendRequestMapper>();
services.AddSingleton<FriendMapper>();
services.AddSingleton<MessageMapper>();
services.AddSingleton<MessageGroupMapper>();
```

**Verify**: `dotnet build` — 0 error.
**Dependencies**: Slice 0.2.

---

## ✅ Checkpoint 0: Foundation Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] 5 entities có đầy đủ EF config
- [ ] Migration apply thành công, 5 bảng tồn tại trong DB
- [ ] 4 repositories đăng ký DI
- [ ] 4 mappers đăng ký Singleton

---

## Phase 1: Friend Request Module

---

### Slice 1.1: SendFriendRequest

**Type**: Command (write) | **Module**: Group

**Files:**
```
Application/Features/Group/Commands/SendFriendRequest/SendFriendRequestCommand.cs
Application/Features/Group/Commands/SendFriendRequest/SendFriendRequestCommandHandler.cs
Application/Features/Group/Validators/Group/SendFriendRequestCommandValidator.cs
Application/Features/Group/Dtos/SendFriendRequestRequest.cs
Api/Controllers/Group/FriendRequestsController.cs   ← tạo mới
```

**Bước 1 — Test (RED):**
```
tests/Beacon.UnitTests/Group/SendFriendRequestCommandHandlerTests.cs

- Handle_ShouldReturnValidationError_WhenSelfRequest
  → currentUserId == ReceiverId → ErrorType.Validation, Code = SELF_FRIEND_REQUEST

- Handle_ShouldReturnConflict_WhenDuplicatePendingRequest
  → HasPendingBetweenAsync = true → ErrorType.Conflict, Code = FRIEND_REQUEST_DUPLICATE

- Handle_ShouldReturnConflict_WhenAlreadyFriends
  → AreFriendsAsync = true → ErrorType.Conflict, Code = ALREADY_FRIENDS

- Handle_ShouldReturnSuccess_WhenValidRequest
  → Result.IsSuccess = true, FriendRequest được AddAsync
```

**Bước 2 — Command + Handler:**
```csharp
public record SendFriendRequestCommand(Guid ReceiverId) : IRequest<Result<FriendRequestDto>>;

// Handler guards theo thứ tự:
// 1. currentUser.UserId == command.ReceiverId → Error.Validation(SELF_FRIEND_REQUEST)
// 2. HasPendingBetweenAsync → Error.Conflict(FRIEND_REQUEST_DUPLICATE)
// 3. AreFriendsAsync → Error.Conflict(ALREADY_FRIENDS)
// 4. AddAsync(new FriendRequest { ... Status = Pending, CreatedAtUtc = UtcNow })
// 5. SaveChangesAsync → return Result.Success(mapper.ToDto(...))
```

**Bước 3 — Validator:**
```csharp
public class SendFriendRequestCommandValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestCommandValidator()
    {
        RuleFor(x => x.ReceiverId)
            .NotEmpty().WithMessage("Id người nhận không được để trống.");
    }
}
```

**Bước 4 — Controller:**
```csharp
[Route("api/v1/friend-requests")]
[Authorize]
public class FriendRequestsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendFriendRequestRequest req, CancellationToken ct)
        => CreatedResult("api/v1/friend-requests",
            await mediator.Send(new SendFriendRequestCommand(req.ReceiverId), ct));
}
```

**Acceptance Criteria:**
- [ ] Unit test: 4 cases GREEN
- [ ] `POST /api/v1/friend-requests` → 201 + `ApiResponse<FriendRequestDto>`
- [ ] Self-request → 400 + code = `SELF_FRIEND_REQUEST`
- [ ] Duplicate → 409 + code = `FRIEND_REQUEST_DUPLICATE`
- [ ] Already friends → 409 + code = `ALREADY_FRIENDS`
- [ ] Không token → 401

**Dependencies**: Checkpoint 0.

---

### Slice 1.2: AcceptFriendRequest ⚠️ (Most Complex)

**Type**: Command | **Module**: Group

**Files:**
```
Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommand.cs
Application/Features/Group/Commands/AcceptFriendRequest/AcceptFriendRequestCommandHandler.cs
```

**Bước 1 — Test (RED):**
```
tests/Beacon.UnitTests/Group/AcceptFriendRequestCommandHandlerTests.cs

- Handle_ShouldReturnNotFound_WhenRequestDoesNotExist
- Handle_ShouldReturnForbidden_WhenNotReceiver
  → request.ReceiverId != currentUser.UserId → ErrorType.Forbidden, FRIEND_REQUEST_FORBIDDEN
- Handle_ShouldReturnConflict_WhenAlreadyAccepted
  → request.Status != Pending → ErrorType.Conflict, FRIEND_REQUEST_NOT_PENDING
- Handle_ShouldReturnSuccess_AndCreateFriendAndGroup
  → Result.IsSuccess = true
  → FriendRepository.AddAsync called once
  → MessageGroupRepository.AddAsync called once
  → FriendRequest.Status = Accepted
  → SaveChangesAsync called once
```

**Bước 2 — Handler (3 repos, 1 SaveChanges):**
```csharp
// Thứ tự:
// 1. GetByIdAsync(id) → 404
// 2. request.ReceiverId != currentUser.UserId → 403
// 3. request.Status != Pending → 409
// 4. var group = new MessageGroup { IsPrivate=true, CreatedAtUtc=UtcNow }
// 5. await _groupRepo.AddAsync(group, ct)  ← track group để có group.Id
// 6. var (u1,u2) = Min/Max(senderId, receiverId)
//    var friend = new Friend { UserId1=u1, UserId2=u2, Type=Normal,
//                              MessageGroupId=group.Id, CreatedAtUtc=UtcNow }
// 7. await _friendRepo.AddAsync(friend, ct)
// 8. group.Members.Add(new MessageGroupMember { GroupId=group.Id, UserId=senderId })
//    group.Members.Add(new MessageGroupMember { GroupId=group.Id, UserId=receiverId })
// 9. request.Status = Accepted
// 10. await _friendRequestRepo.SaveChangesAsync(ct)  ← 1 lần duy nhất, flush tất cả
```

**Bước 3 — Controller (thêm vào FriendRequestsController):**
```csharp
[HttpPatch("{id:guid}/accept")]
public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(new AcceptFriendRequestCommand(id), ct));
```

**Acceptance Criteria:**
- [ ] Unit test: 4 cases GREEN, verify 3 repos được call đúng
- [ ] `PATCH /api/v1/friend-requests/{id}/accept` → 200
- [ ] Non-receiver → 403 + code = `FRIEND_REQUEST_FORBIDDEN`
- [ ] Double-accept → 409 + code = `FRIEND_REQUEST_NOT_PENDING`
- [ ] DB: `Friends`, `MessageGroups`, `MessageGroupMembers` có record sau accept

**Dependencies**: Slice 1.1.

---

### Slice 1.3: DeclineFriendRequest

**Type**: Command | **Module**: Group

**Files:**
```
Application/Features/Group/Commands/DeclineFriendRequest/DeclineFriendRequestCommand.cs
Application/Features/Group/Commands/DeclineFriendRequest/DeclineFriendRequestCommandHandler.cs
```

**Bước 1 — Test (RED):**
```
- Handle_ShouldReturnNotFound_WhenRequestDoesNotExist
- Handle_ShouldReturnForbidden_WhenNotReceiver
- Handle_ShouldReturnConflict_WhenNotPending
- Handle_ShouldReturnSuccess_AndMarkDeclined
```

**Handler:**
```
// 1. GetByIdAsync → 404
// 2. receiverId != currentUser → 403 FRIEND_REQUEST_FORBIDDEN
// 3. Status != Pending → 409 FRIEND_REQUEST_NOT_PENDING
// 4. request.Status = Declined
// 5. SaveChangesAsync
// 6. Result.Success()  ← Result (không phải Result<T>), data = null
```

**Controller:**
```csharp
[HttpPatch("{id:guid}/decline")]
public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(new DeclineFriendRequestCommand(id), ct));
```

**Dependencies**: Slice 1.1.

---

### Slice 1.4 + 1.5: ListReceivedFriendRequests + ListSentFriendRequests

**Type**: Query (read-only) — 2 slices tương tự, implement song song

**Files:**
```
Application/Features/Group/Queries/ListReceivedFriendRequests/
Application/Features/Group/Queries/ListSentFriendRequests/
```

**Query:**
```csharp
public record ListReceivedFriendRequestsQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<FriendRequestDto>>>;
```

**Handler:**
```
// _repo.ListReceivedAsync(currentUser.UserId, cursor, limit, ct)
// Map mỗi FriendRequest → FriendRequestDto (cần JOIN User để lấy senderUsername + avatarUrl)
// Trả CursorPagedResult<FriendRequestDto>
```

**Controller:**
```csharp
[HttpGet("received")]
public async Task<IActionResult> GetReceived([FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    => HandleResult(await mediator.Send(new ListReceivedFriendRequestsQuery(cursor, limit), ct));

[HttpGet("sent")]
public async Task<IActionResult> GetSent([FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    => HandleResult(await mediator.Send(new ListSentFriendRequestsQuery(cursor, limit), ct));
```

**Note**: Repository cần JOIN User table để lấy `SenderUsername` + `SenderAvatarUrl` — dùng projection `Select` tránh N+1.

**Dependencies**: Checkpoint 0.

---

## ✅ Checkpoint 1: Friend Request Module Complete

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN (12+ cases)
- [ ] Integration: POST send → 201, PATCH accept → 200, PATCH decline → 200, GET lists → 200
- [ ] Edge cases: self-send 400, duplicate 409, forbidden 403, double-accept 409
- [ ] DB schema: bảng `FriendRequests` có data, `Friends` + `MessageGroups` có data sau accept

---

## Phase 2: Friend Management

---

### Slice 2.1: ListFriends

**Files:**
```
Application/Features/Group/Queries/ListFriends/ListFriendsQuery.cs
Application/Features/Group/Queries/ListFriends/ListFriendsQueryHandler.cs
Api/Controllers/Group/FriendsController.cs   ← tạo mới
```

**Query + Handler:**
```csharp
public record ListFriendsQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<FriendDto>>>;

// Handler:
// _friendRepo.ListByUserAsync(currentUser.UserId, cursor, limit, ct)
// Repo: WHERE UserId1=me OR UserId2=me, ORDER BY CreatedAtUtc DESC
// Map → FriendDto, lấy "other user" (UserId != currentUserId)
```

**Controller:**
```csharp
[Route("api/v1/friends")]
[Authorize]
public class FriendsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
        => HandleResult(await mediator.Send(new ListFriendsQuery(cursor, limit), ct));
}
```

**Dependencies**: Checkpoint 1.

---

### Slice 2.2: GetFriendDetail

**Files:**
```
Application/Features/Group/Queries/GetFriendDetail/GetFriendDetailQuery.cs
Application/Features/Group/Queries/GetFriendDetail/GetFriendDetailQueryHandler.cs
```

**Handler:**
```
// GetByUsersAsync(currentUser, targetUserId) → 404 FRIEND_NOT_FOUND nếu null
// Map → FriendDetailDto (bao gồm MessageGroupId, Type, CreatedAtUtc)
```

**Controller:**
```csharp
[HttpGet("{userId:guid}")]
public async Task<IActionResult> GetDetail(Guid userId, CancellationToken ct)
    => HandleResult(await mediator.Send(new GetFriendDetailQuery(userId), ct));
```

**Dependencies**: Slice 2.1.

---

### Slice 2.3: UpdateFriendType

**Files:**
```
Application/Features/Group/Commands/UpdateFriendType/UpdateFriendTypeCommand.cs
Application/Features/Group/Commands/UpdateFriendType/UpdateFriendTypeCommandHandler.cs
Application/Features/Group/Validators/Group/UpdateFriendTypeCommandValidator.cs
Application/Features/Group/Dtos/UpdateFriendTypeRequest.cs
```

**Handler:**
```
// GetByUsersAsync(currentUser, targetUserId) → 404 FRIEND_NOT_FOUND nếu null
// friend.Type = command.NewType
// SaveChangesAsync
// Result.Success()
```

**Validator:**
```csharp
RuleFor(x => x.NewType).IsInEnum().WithMessage("Loại bạn bè không hợp lệ.");
```

**Controller:**
```csharp
[HttpPatch("{userId:guid}/type")]
public async Task<IActionResult> UpdateType(Guid userId, [FromBody] UpdateFriendTypeRequest req, CancellationToken ct)
    => HandleResult(await mediator.Send(new UpdateFriendTypeCommand(userId, req.NewType), ct));
```

**Dependencies**: Slice 2.1.

---

### Slice 2.4: RemoveFriend

**Files:**
```
Application/Features/Group/Commands/RemoveFriend/RemoveFriendCommand.cs
Application/Features/Group/Commands/RemoveFriend/RemoveFriendCommandHandler.cs
```

**Bước 1 — Test (RED):**
```
- Handle_ShouldReturnNotFound_WhenNotFriends
- Handle_ShouldReturnSuccess_AndDeleteFriendAndMembers
  → FriendRepo.DeleteAsync called
  → MessageGroupRepo.RemoveMembersAsync(friend.MessageGroupId, userId1, userId2) called
  → SaveChangesAsync called once
```

**Handler (2 repos, 1 SaveChanges):**
```
// 1. GetByUsersAsync(currentUser, targetUserId) → 404
// 2. _groupRepo.RemoveMembersAsync(friend.MessageGroupId, friend.UserId1, friend.UserId2, ct)
// 3. _friendRepo.DeleteAsync(friend, ct)
// 4. _friendRepo.SaveChangesAsync(ct)  ← flush cả RemoveRange + Delete
// 5. Result.Success()
```

**Controller:**
```csharp
[HttpDelete("{userId:guid}")]
public async Task<IActionResult> Remove(Guid userId, CancellationToken ct)
    => HandleResult(await mediator.Send(new RemoveFriendCommand(userId), ct));
// → 200, data: null (KHÔNG phải 204)
```

**Dependencies**: Slice 2.1.

---

## ✅ Checkpoint 2: Friend Management Complete

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test` — tất cả GREEN
- [ ] GET /friends → 200, paginated
- [ ] GET /friends/{userId} → 200 / 404
- [ ] PATCH /friends/{userId}/type → 200 / 404 / 400
- [ ] DELETE /friends/{userId} → **200**, data = null (KHÔNG 204)
- [ ] Sau DELETE: `Friends` row xóa, `MessageGroupMembers` 2 rows xóa, `MessageGroups` còn

---

## Phase 3: Private Messaging

---

### Slice 3.1: SendMessage

**Files:**
```
Application/Features/Messaging/Commands/SendMessage/SendMessageCommand.cs
Application/Features/Messaging/Commands/SendMessage/SendMessageCommandHandler.cs
Application/Features/Messaging/Validators/Messaging/SendMessageCommandValidator.cs
Application/Features/Messaging/Dtos/SendMessageRequest.cs
Api/Controllers/Messaging/MessageGroupsController.cs   ← tạo mới
```

**Bước 1 — Test (RED):**
```
- Handle_ShouldReturnForbidden_WhenNotMember
  → IsMemberAsync = false → ErrorType.Forbidden, MESSAGE_GROUP_FORBIDDEN
- Handle_ShouldReturnSuccess_WhenMember
  → Result.IsSuccess, MessageRepository.AddAsync called
```

**Handler:**
```
// 1. IsMemberAsync(command.GroupId, currentUser.UserId) → 403 MESSAGE_GROUP_FORBIDDEN
// 2. var msg = new Message { GroupId, SenderId=currentUser, Content, CreatedAtUtc=UtcNow }
// 3. AddAsync(msg, ct) → SaveChangesAsync
// 4. Result.Success(mapper.ToDto(msg, currentUser.Username))
```

**Validator:**
```csharp
RuleFor(x => x.GroupId).NotEmpty().WithMessage("Id nhóm không được để trống.");
RuleFor(x => x.Content).NotEmpty().WithMessage("Nội dung tin nhắn không được để trống.")
    .MaximumLength(4000).WithMessage("Nội dung không vượt quá 4000 ký tự.");
```

**Controller:**
```csharp
[Route("api/v1/message-groups")]
[Authorize]
public class MessageGroupsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpPost("{groupId:guid}/messages")]
    public async Task<IActionResult> Send(Guid groupId, [FromBody] SendMessageRequest req, CancellationToken ct)
        => CreatedResult($"api/v1/message-groups/{groupId}/messages",
            await mediator.Send(new SendMessageCommand(groupId, req.Content), ct));
}
```

**Dependencies**: Checkpoint 1 (MessageGroup được tạo bởi AcceptFriendRequest).

---

### Slice 3.2: ListMessages

**Files:**
```
Application/Features/Messaging/Queries/ListMessages/ListMessagesQuery.cs
Application/Features/Messaging/Queries/ListMessages/ListMessagesQueryHandler.cs
```

**Query:**
```csharp
public record ListMessagesQuery(Guid GroupId, DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<MessageDto>>>;
```

**Handler:**
```
// 1. IsMemberAsync(groupId, currentUser) → 403 MESSAGE_GROUP_FORBIDDEN
// 2. _messageRepo.ListByGroupAsync(groupId, cursor, limit, ct)
//    → ORDER BY CreatedAtUtc DESC, WHERE CreatedAtUtc < cursor (nếu có)
// 3. Map → CursorPagedResult<MessageDto>
//    nextCursor = items.Last().CreatedAtUtc (nếu hasMore)
```

**Controller:**
```csharp
[HttpGet("{groupId:guid}/messages")]
public async Task<IActionResult> List(Guid groupId, [FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    => HandleResult(await mediator.Send(new ListMessagesQuery(groupId, cursor, limit), ct));
```

**Dependencies**: Slice 3.1.

---

### Slice 3.3: ListMyMessageGroups

**Files:**
```
Application/Features/Messaging/Queries/ListMyMessageGroups/ListMyMessageGroupsQuery.cs
Application/Features/Messaging/Queries/ListMyMessageGroupsQueryHandler.cs
```

**Query:**
```csharp
public record ListMyMessageGroupsQuery(DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<MessageGroupSummaryDto>>>;
```

**Handler:**
```
// _groupRepo.ListByUserAsync(currentUser.UserId, cursor, limit, ct)
// Repository dùng JOIN/subquery để lấy last message:
//   SELECT mg.*, u.Username, u.AvatarUrl,
//          last_msg.Content AS LastMessage, last_msg.CreatedAtUtc AS LastMessageAtUtc
//   FROM MessageGroups mg
//   JOIN MessageGroupMembers me ON me.GroupId = mg.Id AND me.UserId = @userId   ← dùng IX
//   JOIN MessageGroupMembers other ON other.GroupId = mg.Id AND other.UserId != @userId
//   JOIN Users u ON u.Id = other.UserId
//   LEFT JOIN LATERAL (SELECT TOP 1 ... FROM Messages WHERE GroupId = mg.Id ORDER BY CreatedAtUtc DESC) last_msg
//   WHERE mg.IsPrivate = true
//   ORDER BY COALESCE(last_msg.CreatedAtUtc, mg.CreatedAtUtc) DESC
// Map → CursorPagedResult<MessageGroupSummaryDto>
```

**Controller:**
```csharp
[HttpGet]
public async Task<IActionResult> ListGroups([FromQuery] DateTime? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    => HandleResult(await mediator.Send(new ListMyMessageGroupsQuery(cursor, limit), ct));
```

**Note**: Đây là query phức tạp nhất — nếu LINQ không diễn đạt được lateral join, dùng `FromSqlRaw` với parameterized query.

**Dependencies**: Slice 3.1.

---

## ✅ Final Checkpoint: All Phases Complete

**Build & Tests:**
- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN

**Integration Test Coverage:**
- [ ] `FriendRequestsControllerTests`: POST 201, PATCH accept 200, PATCH decline 200, GET received 200, GET sent 200, auth 401
- [ ] `FriendsControllerTests`: GET list 200, GET detail 200/404, PATCH type 200/400, DELETE 200 (not 204)
- [ ] `MessageGroupsControllerTests`: POST message 201, GET messages 200/403, GET groups 200

**Security:**
- [ ] Mọi endpoint có `[Authorize]`
- [ ] Owner check nằm trong **handler** (không phải controller)
- [ ] Không có PII trong error message / log

**Schema:**
- [ ] Bảng `Friends` có UNIQUE INDEX (UserId1, UserId2)
- [ ] Bảng `MessageGroupMembers` có INDEX trên UserId
- [ ] Bảng `Messages` có INDEX (GroupId, CreatedAtUtc)

**Ready for `/review` → `/deploy`**

---

## Tóm tắt Slice Order

| Slice | Mô tả | Dep |
|---|---|---|
| 0.1 | ErrorCodes + Enums | — |
| 0.2 | Entities + Repo Interfaces | 0.1 |
| 0.3 | EF Config + DbContext + Migration | 0.2 |
| 0.4 | Repo Implementations + DI | 0.3 |
| 0.5 | Mappers + DI | 0.2 |
| **CP0** | ✅ Foundation | |
| 1.1 | SendFriendRequest | CP0 |
| 1.2 | AcceptFriendRequest ⚠️ | 1.1 |
| 1.3 | DeclineFriendRequest | 1.1 |
| 1.4 | ListReceivedFriendRequests | CP0 |
| 1.5 | ListSentFriendRequests | CP0 |
| **CP1** | ✅ Friend Requests | |
| 2.1 | ListFriends | CP1 |
| 2.2 | GetFriendDetail | 2.1 |
| 2.3 | UpdateFriendType | 2.1 |
| 2.4 | RemoveFriend ⚠️ | 2.1 |
| **CP2** | ✅ Friend Management | |
| 3.1 | SendMessage | CP1 |
| 3.2 | ListMessages | 3.1 |
| 3.3 | ListMyMessageGroups ⚠️ | 3.1 |
| **CP3** | ✅ Final | |

> ⚠️ = slice phức tạp, ưu tiên làm sớm trong phase.
