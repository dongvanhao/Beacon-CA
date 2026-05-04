# Feature: Search Friends & Message Group Detail

## Objective

Bổ sung hai endpoint còn thiếu trong module Group/Messaging:
1. **Search Friend** — tìm bạn bè theo số điện thoại (có thể mở rộng thêm field sau).
2. **Message Group Detail** — xem thông tin chi tiết nhóm chat kèm danh sách thành viên và avatarUrl.

## Target Users

- **Search Friend**: User đã xác thực (`[Authorize]`), chỉ tìm trong danh sách bạn bè của chính họ.
- **Message Group Detail**: User đã xác thực và là thành viên của nhóm.

---

## Core Features & Use Cases

### UC-1: Search Friend by phone number

**Endpoint:** `GET /api/v1/friends/search?search=<term>&cursor=<ISO-UTC>&limit=20`

**Acceptance Criteria:**
- `search` là chuỗi tìm kiếm (validate: required, minLength = 3).
- Hiện tại chỉ tìm theo `PhoneNumber` (`Contains`, case-insensitive).
- Kết quả chỉ bao gồm những user **đã là bạn** với current user.
- Trả `CursorPagedResult<FriendDto>` (cùng shape với `GET /api/v1/friends`).
- `FriendDto.AvatarUrl` phải được populate (presigned URL từ MinIO, TTL 15p).
- Nếu `PhoneNumber` của friend là `null` → không xuất hiện trong kết quả tìm SĐT.

**Validation:**
```
search: NotEmpty, MinimumLength(3)
limit:  1–100 (clamp, default 20)
```

**Response shape:** giống `ListFriends` — `ApiResponse<CursorPagedResult<FriendDto>>`.

---

### UC-2: Get Message Group Detail

**Endpoint:** `GET /api/v1/message-groups/{groupId:guid}`

**Acceptance Criteria:**
- Nếu group không tồn tại → `MESSAGE_GROUP_NOT_FOUND` (404).
- Nếu current user không phải thành viên → `MESSAGE_GROUP_FORBIDDEN` (403).
- Trả thông tin cơ bản của group + danh sách thành viên.
- Mỗi thành viên gồm: `userId`, `username`, `familyName`, `givenName`, `avatarUrl` (presigned URL, nullable).
- `avatarUrl` populate bất đồng bộ song song cho tất cả thành viên có avatar.

**Response shape:**
```json
{
  "success": true,
  "message": "...",
  "code": null,
  "data": {
    "groupId": "guid",
    "isPrivate": true,
    "createdAtUtc": "2025-01-01T00:00:00Z",
    "members": [
      {
        "userId": "guid",
        "username": "string",
        "familyName": "string",
        "givenName": "string",
        "avatarUrl": "string | null"
      }
    ]
  },
  "errors": null
}
```

---

## Out of Scope

- Search friend theo `username` hoặc `givenName`/`familyName` — thiết kế theo hướng extensible nhưng **chưa implement** trong iteration này.
- Phân trang cho danh sách thành viên trong group detail (thường nhóm chat nhỏ, trả tất cả).
- Group name / group avatar (chưa có trong schema hiện tại).
- Search trong nhóm (tìm kiếm thành viên theo tên).

---

## Technical Approach (Clean Architecture)

### Domain Layer

**Không thêm entity mới.** Chỉ mở rộng repository interfaces.

#### `IFriendRepository` — thêm method:
```csharp
Task<CursorPagedResult<Friend>> SearchByUserAsync(
    Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct);
```

#### `IMessageGroupRepository` — thêm method:
```csharp
Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct);
```
> Method này include `Members` → `User` → `AvatarMediaObject` (3 cấp Include).

---

### Application Layer

#### UC-1: Search Friend

**Query:**
```
Application/Features/Group/Queries/SearchFriends/
  SearchFriendsQuery.cs          — record: string Search, DateTime? Cursor, int Limit
  SearchFriendsQueryHandler.cs
Validators/Group/SearchFriendsQueryValidator.cs
```

**Handler logic:**
1. Lấy `userId` từ `ICurrentUserService`.
2. Gọi `friendRepo.SearchByUserAsync(userId, search, cursor, limit, ct)`.
3. Với mỗi `Friend`, lấy `otherUser = friend.GetOtherUser(userId)`.
4. Nếu `otherUser.AvatarMediaObjectId != null` → generate presigned URL qua `IStorageService.GeneratePresignedGetUrlAsync`.
5. Map sang `FriendDto` và trả `CursorPagedResult<FriendDto>`.

**Lấy AvatarUrl:** `IStorageService.GetMediaUrlsBatchAsync(avatarMediaObjects, ct)` — song song, tái dụng extension method có sẵn. Cần `Include(u => u.AvatarMediaObject)` trong repo query.

#### UC-2: Message Group Detail

**DTO mới:**
```
Application/Features/Messaging/Dtos/
  MessageGroupDetailDto.cs       — record: GroupId, IsPrivate, CreatedAtUtc, Members
  MessageGroupMemberDto.cs       — record: UserId, Username, FamilyName, GivenName, AvatarUrl?
```

**Query:**
```
Application/Features/Messaging/Queries/GetMessageGroupDetail/
  GetMessageGroupDetailQuery.cs          — record: Guid GroupId
  GetMessageGroupDetailQueryHandler.cs
Validators/Messaging/GetMessageGroupDetailQueryValidator.cs   (chỉ validate GroupId != Guid.Empty)
```

**Handler logic:**
1. Gọi `messageGroupRepo.GetByIdWithMembersAsync(groupId, ct)`.
2. Nếu null → `MESSAGE_GROUP_NOT_FOUND`.
3. Kiểm tra `currentUser.UserId` có trong `group.Members` → nếu không → `MESSAGE_GROUP_FORBIDDEN`.
4. Collect các `AvatarMediaObject` khác null từ members.
5. Gọi `storage.GetMediaUrlsBatchAsync(avatarObjects, ct)` → build dictionary `UserId → avatarUrl`.
6. Map sang `MessageGroupDetailDto` với `Members`.

**Mapper mới:**
```
Application/Mappings/Messaging/MessageGroupDetailMapper.cs
```
Methods:
- `ToDetailDto(MessageGroup group, Dictionary<Guid, string> avatarUrlMap) → MessageGroupDetailDto`
- `ToMemberDto(MessageGroupMember member, string? avatarUrl) → MessageGroupMemberDto`

---

### Infrastructure Layer

#### `FriendRepository` — implement `SearchByUserAsync`:
```csharp
public async Task<CursorPagedResult<Friend>> SearchByUserAsync(
    Guid userId, string search, DateTime? cursor, int limit, CancellationToken ct)
{
    var query = db.Friends
        .AsNoTracking()
        .Include(f => f.User1).ThenInclude(u => u.AvatarMediaObject)
        .Include(f => f.User2).ThenInclude(u => u.AvatarMediaObject)
        .Where(f => (f.UserId1 == userId || f.UserId2 == userId)
            && ((f.UserId1 == userId
                    ? f.User2.PhoneNumber
                    : f.User1.PhoneNumber) != null)
            && EF.Functions.Like(
                f.UserId1 == userId ? f.User2.PhoneNumber! : f.User1.PhoneNumber!,
                $"%{search}%"))
        .OrderByDescending(f => f.CreatedAtUtc);

    if (cursor.HasValue)
        query = query.Where(f => f.CreatedAtUtc < cursor.Value).OrderByDescending(f => f.CreatedAtUtc);

    var items = await query.Take(limit + 1).ToListAsync(ct);
    // cursor + hasMore logic giống ListByUserAsync
}
```
> **Lưu ý:** `EF.Functions.Like` dùng wildcard `%`. Nếu EF Core không dịch được biểu thức conditional trong WHERE, tách thành 2 subquery Union hoặc dùng `.AsEnumerable()` sau khi filter bằng userId (chấp nhận load thêm 1 field để filter in-memory nếu cần — ghi chú trong code). Quyết định cụ thể khi implement sau khi test EF translation.

#### `MessageGroupRepository` — implement `GetByIdWithMembersAsync`:
```csharp
public Task<MessageGroup?> GetByIdWithMembersAsync(Guid id, CancellationToken ct)
    => db.MessageGroups
        .Include(g => g.Members)
            .ThenInclude(m => m.User)
                .ThenInclude(u => u.AvatarMediaObject)
        .FirstOrDefaultAsync(g => g.Id == id, ct);
```

---

### Presentation / API Layer

#### `FriendsController` — thêm action:
```csharp
#region
/// <summary>Tìm kiếm bạn bè theo số điện thoại.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: search trống hoặc ngắn hơn 3 ký tự.
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

#### `MessageGroupsController` — thêm action:
```csharp
#region
/// <summary>Xem chi tiết nhóm chat kèm danh sách thành viên.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>MESSAGE_GROUP_NOT_FOUND</c>: Nhóm không tồn tại.
/// - <c>MESSAGE_GROUP_FORBIDDEN</c>: Không phải thành viên nhóm.
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// {
///   "groupId": "guid",
///   "isPrivate": true,
///   "createdAtUtc": "datetime",
///   "members": [{ "userId", "username", "familyName", "givenName", "avatarUrl" }]
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

---

## Error Codes

Các code sau đã tồn tại trong `ErrorCodes.cs` — **không cần thêm mới**:

| Code | ErrorType | Dùng cho |
|---|---|---|
| `VALIDATION_ERROR` | Validation | Search param invalid |
| `MESSAGE_GROUP_NOT_FOUND` | NotFound | Group không tồn tại |
| `MESSAGE_GROUP_FORBIDDEN` | Forbidden | Không là thành viên |

---

## Testing Strategy

### Unit Tests (`Beacon.UnitTests`)

**UC-1 — `SearchFriendsQueryHandlerTests`:**
- `Handle_WithValidSearch_ReturnsFriendDtos` — repo trả 2 friends, verify mapping + AvatarUrl populated.
- `Handle_WhenNoFriendsMatch_ReturnsEmptyPage` — repo trả 0 results.
- `Handle_LimitClamped_PassesCorrectLimitToRepo` — limit=200 → repo nhận 100.

**Validator tests (`SearchFriendsQueryValidatorTests`):**
- `Validate_SearchEmpty_ReturnsError`
- `Validate_SearchTooShort_ReturnsError` (length < 3)
- `Validate_ValidSearch_PassesValidation`

**UC-2 — `GetMessageGroupDetailQueryHandlerTests`:**
- `Handle_WithValidGroupAndMember_ReturnsDetailDto` — group có 2 thành viên, verify members list.
- `Handle_WhenGroupNotFound_ReturnsNotFoundError`
- `Handle_WhenUserNotMember_ReturnsForbiddenError`
- `Handle_MemberWithoutAvatar_ReturnsNullAvatarUrl`
- `Handle_MemberWithAvatar_CallsStorageAndReturnsUrl`

### Integration Tests (`Beacon.IntergrationTests`)

- `FriendsController_Search_ReturnsFriendsMatchingPhone`
- `FriendsController_Search_RequiresAuth_Returns401`
- `MessageGroupsController_GetDetail_ReturnsMembersWithAvatarUrl`
- `MessageGroupsController_GetDetail_WhenNotMember_Returns403`

---

## Boundaries

### Always Do
- `SearchFriendsQueryHandler` inject `IStorageService` để populate avatarUrl (không pass `null`).
- Owner check (`IsMember`) trong **handler**, không trong controller.
- Cursor pagination cho SearchFriends — cùng meta shape với `ListFriends`.

### Ask First
- Nếu EF Core không dịch được conditional PhoneNumber expression trong `SearchByUserAsync` → cần quyết định Union hoặc load in-memory.
- Nếu muốn thêm search theo `username` / `name` song song với SĐT trong cùng iteration.

### Never Do
- Không để `AvatarUrl = null` hardcode trong handler (khác với ListFriends/GetFriendDetail hiện tại — đây là tech debt của các handler cũ, spec mới phải populate đúng).
- Không expose toàn bộ `MessageGroup.Members` qua navigation prop mà không include rõ ràng.
- Không thêm business logic vào `MessageGroupDetailMapper`.
