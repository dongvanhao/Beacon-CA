# Plan: Search Friend & Message Group Detail

**Module**: Group / Messaging
**Phạm vi**: 2 features độc lập — 8 slices tổng cộng
**Không cần**: migration mới, entity mới

---

## Business Rules

| Rule | Vị trí enforce |
|---|---|
| Search chỉ trong danh sách bạn của current user | Repo WHERE clause (userId1/userId2 filter) |
| Chỉ thành viên group mới xem được detail | Handler — kiểm tra `group.Members.Any(m => m.UserId == userId)` |
| AvatarUrl = presigned URL (TTL 15p), nullable | Handler — `IStorageService.GetMediaUrlsBatchAsync` (batch, song song) |

---

## Phase 1: UC-1 — Search Friend

> 4 slices. `FriendDto` đã có sẵn, không cần DTO mới hay migration.

---

### Slice 1.1 — Test Skeleton (RED)

**File:** `tests/Beacon.UnitTests/Group/SearchFriendsQueryHandlerTests.cs`

Viết test với mock repo — **FAIL vì handler chưa tồn tại**:

```csharp
Handle_WithPhoneSearch_ReturnsCursorPagedFriendDtos()
// Arrange: mock SearchByUserAsync trả 1 Friend có AvatarMediaObject
// mock IStorageService.GeneratePresignedGetUrlAsync trả "https://..."
// Assert: Result.IsSuccess = true, Data[0].AvatarUrl != null

Handle_WhenNoMatchingFriends_ReturnsEmptyPage()
// Assert: Result.IsSuccess = true, Data.Count == 0, Meta.HasMore == false

Handle_SearchTerm_PassedExactlyToRepo()
// Assert: repo được gọi với search = "0912" (không transform)

Handle_LimitOver100_IsClampedTo100()
// Assert: repo được gọi với limit = 100 khi input = 999
```

**Dependencies**: Không có.

---

### Slice 1.2 — Repository Interface + Implementation

**Interface** — `src/Beacon.Domain/IRepository/Group/IFriendRepository.cs` thêm:

```csharp
Task<CursorPagedResult<Friend>> SearchByUserAsync(
    Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct);
```

**Implementation** — `src/Beacon.Infrashtructure/Repository/Group/FriendRepository.cs` thêm:

```csharp
public async Task<CursorPagedResult<Friend>> SearchByUserAsync(
    Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct)
{
    var pattern = $"%{search}%";
    var query = db.Friends
        .AsNoTracking()
        .Include(f => f.User1).ThenInclude(u => u!.AvatarMediaObject)
        .Include(f => f.User2).ThenInclude(u => u!.AvatarMediaObject)
        .Where(f =>
            (f.UserId1 == userId || f.UserId2 == userId)
            && (
                (f.UserId1 == userId
                    && f.User2.PhoneNumber != null
                    && EF.Functions.Like(f.User2.PhoneNumber, pattern))
                ||
                (f.UserId2 == userId
                    && f.User1.PhoneNumber != null
                    && EF.Functions.Like(f.User1.PhoneNumber, pattern))
            ))
        .OrderByDescending(f => f.CreatedAtUtc);

    if (cursor.HasValue)
        query = (IOrderedQueryable<Friend>)query.Where(f => f.CreatedAtUtc < cursor.Value);

    var items = await query.Take(limit + 1).ToListAsync(ct);
    var hasMore = items.Count > limit;
    if (hasMore) items.RemoveAt(items.Count - 1);

    return new CursorPagedResult<Friend>
    {
        Data = items,
        Meta = new CursorMeta
        {
            NextCursor = hasMore ? items[^1].CreatedAtUtc : null,
            Limit = limit,
            HasMore = hasMore
        }
    };
}
```

> **⚠️ Rủi ro EF translation:** Dùng boolean condition kép (không dùng ternary `? :`) để EF Core dịch được sang SQL. Verify bằng integration test bật `EnableSensitiveDataLogging`.

**Dependencies**: Slice 1.1.

---

### Slice 1.3 — Query + Handler + Validator (GREEN)

**Query** — `src/Beacon.Application/Features/Group/Queries/SearchFriends/SearchFriendsQuery.cs`:

```csharp
public record SearchFriendsQuery(string Search, DateTime? Cursor, int Limit = 20)
    : IRequest<Result<CursorPagedResult<FriendDto>>>;
```

**Validator** — `src/Beacon.Application/Features/Group/Validators/Group/SearchFriendsQueryValidator.cs`:

```csharp
public class SearchFriendsQueryValidator : AbstractValidator<SearchFriendsQuery>
{
    public SearchFriendsQueryValidator()
    {
        RuleFor(x => x.Search)
            .NotEmpty().WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MinimumLength(3).WithMessage("Từ khóa tìm kiếm phải có ít nhất 3 ký tự.");
    }
}
```

**Handler** — `src/Beacon.Application/Features/Group/Queries/SearchFriends/SearchFriendsQueryHandler.cs`:

```csharp
public class SearchFriendsQueryHandler(
    IFriendRepository friendRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    FriendMapper mapper)
    : IRequestHandler<SearchFriendsQuery, Result<CursorPagedResult<FriendDto>>>
{
    public async Task<Result<CursorPagedResult<FriendDto>>> Handle(
        SearchFriendsQuery query, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var limit = Math.Clamp(query.Limit, 1, 100);
        var paged = await friendRepo.SearchByUserAsync(userId, query.Search, query.Cursor, limit, ct);

        var avatarObjects = paged.Data
            .Select(f => f.GetOtherUser(userId).AvatarMediaObject)
            .Where(a => a is not null).Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var dtos = paged.Data.Select(f =>
        {
            var other = f.GetOtherUser(userId);
            var avatarUrl = other.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(other.AvatarMediaObjectId.Value, out var url)
                ? url : null;
            return mapper.ToDto(f, userId, other.Username, avatarUrl);
        }).ToList();

        return Result<CursorPagedResult<FriendDto>>.Success(new CursorPagedResult<FriendDto>
        {
            Data = dtos,
            Meta = paged.Meta
        });
    }
}
```

Chạy lại test Slice 1.1 → **phải GREEN**.

**Dependencies**: Slice 1.1, 1.2.

---

### Slice 1.4 — Controller Action + Integration Tests

**File** — `src/Beacon.Api/Controllers/Group/FriendsController.cs` thêm action sau `List`:

```csharp
#region
/// <summary>Tìm kiếm bạn bè theo số điện thoại.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Tìm trong danh sách bạn bè của user hiện tại theo số điện thoại (partial match).
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: <c>search</c> trống hoặc ngắn hơn 3 ký tự.
///
/// Cấu trúc <c>data</c> khi thành công: <c>CursorPagedResult&lt;FriendDto&gt;</c>
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
[HttpGet("search")]
public async Task<IActionResult> Search(
    [FromQuery] string search,
    [FromQuery] DateTime? cursor,
    [FromQuery] int limit = 20,
    CancellationToken ct = default)
    => HandleResult(await mediator.Send(new SearchFriendsQuery(search, cursor, limit), ct));
```

**Integration Tests** — `tests/Beacon.IntergrationTests/Group/FriendsControllerTests.cs` thêm:

```
Search_WithValidPhone_Returns200WithMatchingFriends
Search_WithShortTerm_Returns400ValidationError        ← search = "09"
Search_WithEmptySearch_Returns400ValidationError
Search_WithoutToken_Returns401
```

**Dependencies**: Slice 1.3.

---

## ✅ Checkpoint Phase 1

- [ ] `dotnet build` — 0 error
- [ ] `dotnet test tests/Beacon.UnitTests --filter "FullyQualifiedName~SearchFriends"` — GREEN
- [ ] `GET /api/v1/friends/search?search=091` trả `CursorPagedResult<FriendDto>` với `avatarUrl` populated
- [ ] `GET /api/v1/friends/search?search=09` trả 400 `VALIDATION_ERROR`

---

## Phase 2: UC-2 — Message Group Detail

> 4 slices. Cần DTO mới + Mapper mới + repo method mới. Không cần migration.

---

### Slice 2.1 — Test Skeleton (RED)

**File:** `tests/Beacon.UnitTests/Messaging/GetMessageGroupDetailQueryHandlerTests.cs`

```csharp
Handle_WithValidGroupAndMember_ReturnsDetailWithMembers()
// Assert: Result.IsSuccess = true, Data.GroupId == groupId, Data.Members.Count == 2

Handle_WhenGroupNotFound_ReturnsNotFoundError()
// Assert: Result.IsFailure = true, Error.Code == "MESSAGE_GROUP_NOT_FOUND"

Handle_WhenUserNotMember_ReturnsForbiddenError()
// Assert: Result.IsFailure = true, Error.Code == "MESSAGE_GROUP_FORBIDDEN"

Handle_WhenMemberHasNoAvatar_ReturnsNullAvatarUrl()
// Assert: member.AvatarUrl == null, IStorageService.GetMediaUrlsBatchAsync không được gọi

Handle_AvatarUrls_AreGeneratedInParallel_BatchCall()
// Assert: IStorageService.GetMediaUrlsBatchAsync gọi đúng 1 lần (không phải N lần)
```

**Dependencies**: Không có.

---

### Slice 2.2 — DTOs + Mapper + Đăng ký DI

**DTO mới:**

`src/Beacon.Application/Features/Messaging/Dtos/MessageGroupMemberDto.cs`:

```csharp
public record MessageGroupMemberDto(
    Guid UserId,
    string Username,
    string FamilyName,
    string GivenName,
    string? AvatarUrl);
```

`src/Beacon.Application/Features/Messaging/Dtos/MessageGroupDetailDto.cs`:

```csharp
public record MessageGroupDetailDto(
    Guid GroupId,
    bool IsPrivate,
    DateTime CreatedAtUtc,
    IReadOnlyList<MessageGroupMemberDto> Members);
```

**Mapper** — `src/Beacon.Application/Mappings/Messaging/MessageGroupDetailMapper.cs`:

```csharp
public sealed class MessageGroupDetailMapper
{
    public MessageGroupDetailDto ToDetailDto(
        MessageGroup group, IReadOnlyList<MessageGroupMemberDto> members)
        => new(group.Id, group.IsPrivate, group.CreatedAtUtc, members);

    public MessageGroupMemberDto ToMemberDto(MessageGroupMember member, string? avatarUrl)
        => new(
            UserId: member.UserId,
            Username: member.User.Username,
            FamilyName: member.User.FamilyName,
            GivenName: member.User.GivenName,
            AvatarUrl: avatarUrl);
}
```

**Đăng ký Singleton** — `src/Beacon.Application/DependencyInjection/ApplicationServiceExtensions.cs`:

```csharp
services.AddSingleton<MessageGroupDetailMapper>();
```

**Dependencies**: Slice 2.1.

---

### Slice 2.3 — Repository Interface + Implementation

**Interface** — `src/Beacon.Domain/IRepository/Messaging/IMessageGroupRepository.cs` thêm:

```csharp
Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct);
```

**Implementation** — `src/Beacon.Infrashtructure/Repository/Messaging/MessageGroupRepository.cs` thêm:

```csharp
public Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct)
    => db.MessageGroups
        .Include(g => g.Members)
            .ThenInclude(m => m.User)
                .ThenInclude(u => u.AvatarMediaObject)
        .FirstOrDefaultAsync(g => g.Id == id, ct);
```

**Dependencies**: Slice 2.1.

---

### Slice 2.4 — Query + Handler + Validator + Controller (GREEN)

**Query** — `src/Beacon.Application/Features/Messaging/Queries/GetMessageGroupDetail/GetMessageGroupDetailQuery.cs`:

```csharp
public record GetMessageGroupDetailQuery(Guid GroupId)
    : IRequest<Result<MessageGroupDetailDto>>;
```

**Validator** — `src/Beacon.Application/Features/Messaging/Validators/Messaging/GetMessageGroupDetailQueryValidator.cs`:

```csharp
public class GetMessageGroupDetailQueryValidator : AbstractValidator<GetMessageGroupDetailQuery>
{
    public GetMessageGroupDetailQueryValidator()
    {
        RuleFor(x => x.GroupId)
            .NotEmpty().WithMessage("GroupId không hợp lệ.");
    }
}
```

**Handler** — `src/Beacon.Application/Features/Messaging/Queries/GetMessageGroupDetail/GetMessageGroupDetailQueryHandler.cs`:

```csharp
public class GetMessageGroupDetailQueryHandler(
    IMessageGroupRepository groupRepo,
    ICurrentUserService currentUser,
    IStorageService storage,
    MessageGroupDetailMapper mapper)
    : IRequestHandler<GetMessageGroupDetailQuery, Result<MessageGroupDetailDto>>
{
    public async Task<Result<MessageGroupDetailDto>> Handle(
        GetMessageGroupDetailQuery query, CancellationToken ct)
    {
        var group = await groupRepo.GetByIdWithMembersAsync(query.GroupId, ct);
        if (group is null)
            return Result<MessageGroupDetailDto>.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND,
                    "Không tìm thấy nhóm chat."));

        var userId = currentUser.UserId;
        if (!group.Members.Any(m => m.UserId == userId))
            return Result<MessageGroupDetailDto>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN,
                    "Bạn không phải thành viên của nhóm này."));

        var avatarObjects = group.Members
            .Select(m => m.User.AvatarMediaObject)
            .Where(a => a is not null).Select(a => a!)
            .ToList();

        var urlMap = avatarObjects.Count > 0
            ? (await storage.GetMediaUrlsBatchAsync(avatarObjects, ct))
                .ToDictionary(x => x.Media.Id, x => x.Url)
            : new Dictionary<Guid, string>();

        var memberDtos = group.Members.Select(m =>
        {
            var avatarUrl = m.User.AvatarMediaObjectId.HasValue
                && urlMap.TryGetValue(m.User.AvatarMediaObjectId.Value, out var url)
                ? url : null;
            return mapper.ToMemberDto(m, avatarUrl);
        }).ToList();

        return Result<MessageGroupDetailDto>.Success(
            mapper.ToDetailDto(group, memberDtos));
    }
}
```

Chạy lại test Slice 2.1 → **phải GREEN**.

**Controller** — `src/Beacon.Api/Controllers/Messaging/MessageGroupsController.cs` thêm trước action `Send`:

```csharp
#region
/// <summary>Xem thông tin chi tiết nhóm chat kèm danh sách thành viên.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Chỉ thành viên của nhóm mới được xem.
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại (HTTP 404).
/// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên nhóm (HTTP 403).
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// {
///   "groupId": "guid",
///   "isPrivate": true,
///   "createdAtUtc": "datetime",
///   "members": [
///     { "userId": "guid", "username": "string", "familyName": "string", "givenName": "string", "avatarUrl": "string|null" }
///   ]
/// }
/// </code>
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
[HttpGet("{groupId:guid}")]
public async Task<IActionResult> GetDetail(Guid groupId, CancellationToken ct)
    => HandleResult(await mediator.Send(new GetMessageGroupDetailQuery(groupId), ct));
```

**Integration Tests** — `tests/Beacon.IntergrationTests/Messaging/MessageGroupsControllerTests.cs` thêm:

```
GetDetail_AsMember_Returns200WithMembersAndAvatarUrl
GetDetail_WhenGroupNotFound_Returns404
GetDetail_WhenNotMember_Returns403
GetDetail_WithoutToken_Returns401
```

**Dependencies**: Slice 2.1, 2.2, 2.3.

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN (bao gồm 2 class test mới)
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] `GET /api/v1/friends/search?search=091` → 200, `avatarUrl` populated
- [ ] `GET /api/v1/message-groups/{id}` → 200, `members[].avatarUrl` populated
- [ ] Swagger hiển thị đúng 2 endpoint mới với XML doc

---

## Thứ tự thực hiện & Dependencies

```
Slice 1.1 (Test RED)
    └─► Slice 1.2 (Repo impl)
            └─► Slice 1.3 (Handler GREEN)
                    └─► Slice 1.4 (Controller)

Slice 2.1 (Test RED)
    ├─► Slice 2.2 (DTO + Mapper)
    └─► Slice 2.3 (Repo impl)
            └─► Slice 2.4 (Handler GREEN + Controller)
```

Hai feature hoàn toàn độc lập — có thể làm song song.

---

## Files sẽ thay đổi

| File | Thao tác |
|---|---|
| `Domain/IRepository/Group/IFriendRepository.cs` | Thêm `SearchByUserAsync` |
| `Infrashtructure/Repository/Group/FriendRepository.cs` | Implement `SearchByUserAsync` |
| `Application/Features/Group/Queries/SearchFriends/SearchFriendsQuery.cs` | Tạo mới |
| `Application/Features/Group/Queries/SearchFriends/SearchFriendsQueryHandler.cs` | Tạo mới |
| `Application/Features/Group/Validators/Group/SearchFriendsQueryValidator.cs` | Tạo mới |
| `Api/Controllers/Group/FriendsController.cs` | Thêm action `Search` |
| `Domain/IRepository/Messaging/IMessageGroupRepository.cs` | Thêm `GetByIdWithMembersAsync` |
| `Infrashtructure/Repository/Messaging/MessageGroupRepository.cs` | Implement `GetByIdWithMembersAsync` |
| `Application/Features/Messaging/Dtos/MessageGroupMemberDto.cs` | Tạo mới |
| `Application/Features/Messaging/Dtos/MessageGroupDetailDto.cs` | Tạo mới |
| `Application/Mappings/Messaging/MessageGroupDetailMapper.cs` | Tạo mới |
| `Application/DependencyInjection/ApplicationServiceExtensions.cs` | Đăng ký `MessageGroupDetailMapper` |
| `Application/Features/Messaging/Queries/GetMessageGroupDetail/GetMessageGroupDetailQuery.cs` | Tạo mới |
| `Application/Features/Messaging/Queries/GetMessageGroupDetail/GetMessageGroupDetailQueryHandler.cs` | Tạo mới |
| `Application/Features/Messaging/Validators/Messaging/GetMessageGroupDetailQueryValidator.cs` | Tạo mới |
| `Api/Controllers/Messaging/MessageGroupsController.cs` | Thêm action `GetDetail` |
| `tests/Beacon.UnitTests/Group/SearchFriendsQueryHandlerTests.cs` | Tạo mới |
| `tests/Beacon.UnitTests/Messaging/GetMessageGroupDetailQueryHandlerTests.cs` | Tạo mới |
