# Plan: Get Post Reactions

**Module**: Posts  
**Phạm vi**: 3 slices (không cần migration — schema đã có)  
**Spec**: `docs/specs/get-post-reactions.md`

---

## Tổng quan thay đổi

| File | Loại thay đổi |
|---|---|
| `Domain/IRepository/Posts/IPostReactionRepository.cs` | Thêm 2 method |
| `Infrashtructure/Repository/Posts/PostReactionRepository.cs` | Implement 2 method mới |
| `Application/Features/Posts/Dtos/PostReactionItemResponse.cs` | Tạo mới |
| `Application/Features/Posts/Dtos/ReactorUserResponse.cs` | Tạo mới |
| `Application/Features/Posts/Dtos/PostReactionListResponse.cs` | Tạo mới |
| `Application/Features/Posts/Queries/GetPostReactions/GetPostReactionsQuery.cs` | Tạo mới |
| `Application/Features/Posts/Queries/GetPostReactions/GetPostReactionsQueryHandler.cs` | Tạo mới |
| `Application/Features/Posts/Validators/GetPostReactionsQueryValidator.cs` | Tạo mới |
| `Application/Mappings/Posts/PostDtoMapper.cs` | Thêm method `ToReactionItemResponse` |
| `Api/Controllers/Posts/PostsController.cs` | Thêm 1 action |
| `tests/Beacon.UnitTests/Posts/GetPostReactionsHandlerTests.cs` | Tạo mới |
| `tests/Beacon.IntergrationTests/Posts/PostReactionsControllerTests.cs` | Tạo mới |

---

## Slice 1: Repository Extension (Foundation)

**Mục tiêu**: Cung cấp data access cho handler — 2 method mới vào `IPostReactionRepository` + implementation.

**Không có unit test riêng cho slice này** — được verify gián tiếp qua integration test ở Slice 3.

---

### Bước 1.1 — Mở rộng `IPostReactionRepository`

File: `src/Beacon.Domain/IRepository/Posts/IPostReactionRepository.cs`

Thêm 2 method:

```csharp
/// <summary>Keyset cursor pagination theo CreatedAtUtc DESC, optional filter by icon.</summary>
Task<(List<PostReaction> Items, bool HasMore)> GetPagedByPostIdAsync(
    Guid postId,
    string? iconFilter,
    DateTime? cursor,
    int limit,
    CancellationToken ct = default);

/// <summary>Load tất cả reactions của bài (không cursor/limit) — dùng tính summary tổng.</summary>
Task<List<PostReaction>> GetAllByPostIdAsync(
    Guid postId,
    CancellationToken ct = default);
```

---

### Bước 1.2 — Implement trong `PostReactionRepository`

File: `src/Beacon.Infrashtructure/Repository/Posts/PostReactionRepository.cs`

**`GetPagedByPostIdAsync`** — keyset cursor, lấy `limit + 1` để detect `hasMore`:

```csharp
public async Task<(List<PostReaction> Items, bool HasMore)> GetPagedByPostIdAsync(
    Guid postId, string? iconFilter, DateTime? cursor, int limit, CancellationToken ct = default)
{
    var query = db.PostReactions
        .Where(r => r.PostId == postId);

    if (!string.IsNullOrEmpty(iconFilter))
        query = query.Where(r => r.Icon == iconFilter);

    if (cursor.HasValue)
        query = query.Where(r => r.CreatedAtUtc < cursor.Value);

    // Lấy limit + 1 để detect hasMore
    var items = await query
        .OrderByDescending(r => r.CreatedAtUtc)
        .Take(limit + 1)
        .ToListAsync(ct);

    var hasMore = items.Count > limit;
    if (hasMore) items = items.Take(limit).ToList();

    return (items, hasMore);
}
```

**`GetAllByPostIdAsync`** — load tất cả reaction của post (không filter, không cursor):

```csharp
public Task<List<PostReaction>> GetAllByPostIdAsync(
    Guid postId, CancellationToken ct = default)
    => db.PostReactions
        .Where(r => r.PostId == postId)
        .ToListAsync(ct);
```

> **Lưu ý**: `GetAllByPostIdAsync` load toàn bộ reactions để tính `summary` tổng.
> Không bị ảnh hưởng bởi `iconFilter` của paged query.
> Nếu về sau post có hàng nghìn reactions, có thể optimize bằng GROUP BY SQL — flag để theo dõi.

---

### ✅ Checkpoint Slice 1

- [ ] `dotnet build` — 0 error

---

## Slice 2: Application Layer (TDD — RED → GREEN)

**Mục tiêu**: Implement full business logic theo TDD — test fail trước, rồi implement.

---

### Bước 2.1 — Viết Unit Test (RED) trước

File: `tests/Beacon.UnitTests/Posts/GetPostReactionsHandlerTests.cs`

Test skeleton với mock repositories — **PHẢI FAIL** vì handler chưa tồn tại:

```
Handle_WhenPostNotFound_ReturnsNotFoundError
  → verify ErrorType.NotFound + ErrorCodes.Post.POST_NOT_FOUND

Handle_WhenPostIsPrivateAndNotOwner_ReturnsForbiddenError
  → verify ErrorType.Forbidden + ErrorCodes.Post.POST_ACCESS_DENIED

Handle_WhenPostIsFriendsAndNotFriend_ReturnsForbiddenError
  → verify ErrorType.Forbidden + ErrorCodes.Post.POST_ACCESS_DENIED

Handle_WhenOwnerViewsOwnPost_ReturnsReactionList
  → verify Result.IsSuccess = true
  → verify Items count + icon values
  → verify summary.TotalCount + summary.Icons có đủ 5 icons

Handle_WhenNoReactions_ReturnsEmptyListWithZeroSummary
  → verify Items = []
  → verify summary.TotalCount = 0
  → verify summary.Icons tất cả = 0

Handle_WhenHasMore_ReturnsNextCursorAndHasMoreTrue
  → verify nextCursor != null, hasMore = true

Handle_WhenLastPage_ReturnsNullCursorAndHasMoreFalse
  → verify nextCursor = null, hasMore = false

Handle_WhenIconFilterApplied_ReturnsOnlyMatchingReactions
  → chỉ trả reactions có icon khớp với filter
```

> Commit tại đây với test **FAIL** — đây là điểm khởi đầu TDD (RED).

---

### Bước 2.2 — DTOs

**`ReactorUserResponse`** — user sub-object trong mỗi reaction item:

```csharp
// Application/Features/Posts/Dtos/ReactorUserResponse.cs
public record ReactorUserResponse
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
```

**`PostReactionItemResponse`** — một item trong danh sách:

```csharp
// Application/Features/Posts/Dtos/PostReactionItemResponse.cs
public record PostReactionItemResponse
{
    public Guid ReactionId { get; init; }
    public string Icon { get; init; } = string.Empty;
    public DateTime ReactedAtUtc { get; init; }
    public ReactorUserResponse User { get; init; } = default!;
}
```

**`PostReactionListResponse`** — wrapper toàn bộ response:

```csharp
// Application/Features/Posts/Dtos/PostReactionListResponse.cs
public record PostReactionListResponse
{
    public List<PostReactionItemResponse> Items { get; init; } = new();
    public PostReactionSummaryResponse Summary { get; init; } = default!;
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}
```

---

### Bước 2.3 — Query

```csharp
// Application/Features/Posts/Queries/GetPostReactions/GetPostReactionsQuery.cs
public record GetPostReactionsQuery(
    Guid PostId,
    Guid CurrentUserId,
    string? Icon,
    string? Cursor,
    int Limit
) : IRequest<Result<PostReactionListResponse>>;
```

---

### Bước 2.4 — Handler

File: `Application/Features/Posts/Queries/GetPostReactions/GetPostReactionsQueryHandler.cs`

Dependencies: `IPostRepository`, `IPostReactionRepository`, `IUserRepository`,
`IMediaObjectRepository`, `IFriendRepository`, `IStorageService`, `PostDtoMapper`

**Logic**:

```
1. postRepo.GetByIdAsync → null / IsDeleted / Status != Active → POST_NOT_FOUND
2. Access control (kế thừa GetPostDetailQueryHandler):
   - Owner → OK
   - Visibility=Friends → kiểm tra friendRepo.AreFriendsAsync → không bạn → POST_ACCESS_DENIED
   - Visibility=Private + không phải owner → POST_ACCESS_DENIED
3. Parse cursor: DateTime? cursorDt = string.IsNullOrEmpty(Cursor) ? null : DateTime.Parse(Cursor, UTC)
4. reactionRepo.GetPagedByPostIdAsync(PostId, Icon, cursorDt, Limit) → (pagedItems, hasMore)
5. reactionRepo.GetAllByPostIdAsync(PostId) → tính summary
6. Build summary với đủ 5 icons (kể cả count=0):
   var icons = ReactionIcons.Supported.ToDictionary(k => k, k => allReactions.Count(r => r.Icon == k));
   summary = new PostReactionSummaryResponse { TotalCount = allReactions.Count, Icons = icons }
7. Batch load user info cho pagedItems (unique userIds):
   - loop userRepo.GetByIdAsync (bounded bởi limit ≤ 100)
   - loop mediaRepo + storage.GeneratePresignedGetUrlAsync cho avatar (nếu có)
8. Map → PostReactionItemResponse + ReactorUserResponse
9. nextCursor = hasMore ? pagedItems.Last().CreatedAtUtc.ToString("O") : null
10. Return PostReactionListResponse
```

**Bắt buộc**:
- ✅ Return `Result<PostReactionListResponse>` — không throw cho business error
- ✅ Không inject `AppDbContext`
- ✅ `CancellationToken` pass xuống tất cả async call

---

### Bước 2.5 — Validator

File: `Application/Features/Posts/Validators/GetPostReactionsQueryValidator.cs`

```csharp
public class GetPostReactionsQueryValidator : AbstractValidator<GetPostReactionsQuery>
{
    public GetPostReactionsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Số lượng kết quả phải từ 1 đến 100.");

        RuleFor(x => x.Icon)
            .Must(icon => icon == null || ReactionIcons.IsValid(icon))
            .WithMessage($"Icon không hợp lệ. Chỉ chấp nhận: {string.Join(", ", ReactionIcons.Supported)}.");

        RuleFor(x => x.Cursor)
            .Must(cursor => cursor == null || DateTime.TryParse(cursor, out _))
            .WithMessage("Cursor phải là định dạng ISO-8601 UTC datetime hợp lệ.");
    }
}
```

---

### Bước 2.6 — Extend PostDtoMapper

Thêm method vào `Application/Mappings/Posts/PostDtoMapper.cs`:

```csharp
public PostReactionItemResponse ToReactionItemResponse(
    PostReaction reaction, string displayName, string? avatarUrl) => new()
{
    ReactionId = reaction.Id,
    Icon = reaction.Icon,
    ReactedAtUtc = reaction.CreatedAtUtc,
    User = new ReactorUserResponse
    {
        Id = reaction.UserId,
        DisplayName = displayName,
        AvatarUrl = avatarUrl
    }
};
```

> Không cần đăng ký mapper mới — `PostDtoMapper` đã là Singleton trong DI.

---

### Bước 2.7 — Make Tests GREEN

Chạy lại unit tests từ Bước 2.1 — tất cả phải pass.

```bash
dotnet test tests/Beacon.UnitTests --filter "GetPostReactionsHandlerTests"
```

---

### ✅ Checkpoint Slice 2

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN (bao gồm GetPostReactionsHandlerTests)

---

## Slice 3: API Layer + Integration Tests

**Mục tiêu**: Wire controller endpoint và verify E2E với DB thật.

---

### Bước 3.1 — Thêm Action vào PostsController

File: `src/Beacon.Api/Controllers/Posts/PostsController.cs`

Thêm action mới (không tạo controller mới):

```csharp
[HttpGet("{postId:guid}/reactions")]
public async Task<IActionResult> GetReactions(
    Guid postId,
    [FromQuery] string? icon,
    [FromQuery] string? cursor,
    [FromQuery] int limit = 30,
    CancellationToken ct = default)
    => HandleResult(await mediator.Send(
        new GetPostReactionsQuery(postId, currentUser.UserId, icon, cursor, limit), ct));
```

Kèm XML doc đầy đủ theo convention (xem `api-conventions/RULE.md § 8`):

```csharp
/// <summary>Lấy danh sách người dùng đã react trên một bài đăng.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: icon không hợp lệ, limit ngoài [1,100], cursor sai format.
/// - <c>POST_NOT_FOUND</c>: Bài đăng không tồn tại hoặc đã bị xóa.
/// - <c>POST_ACCESS_DENIED</c>: Bạn không có quyền xem bài đăng này.
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// {
///   "items": [{ "reactionId": "guid", "icon": "heart", "reactedAtUtc": "datetime",
///               "user": { "id": "guid", "displayName": "string", "avatarUrl": "string?" } }],
///   "summary": { "totalCount": "int", "icons": { "heart": "int", "like": "int", ... } },
///   "nextCursor": "string? (null khi hết)",
///   "hasMore": "bool"
/// }
/// </code>
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
```

---

### Bước 3.2 — Integration Tests

File: `tests/Beacon.IntergrationTests/Posts/PostReactionsControllerTests.cs`

**Test cases bắt buộc**:

```
GET_PostReactions_WhenUnauthenticated_Returns401
GET_PostReactions_WhenPostNotFound_Returns404
GET_PostReactions_WhenPrivatePostNotOwner_Returns403
GET_PostReactions_WhenFriendsPostNotFriend_Returns403
GET_PostReactions_WhenOwnerNoReactions_ReturnsEmptyList
GET_PostReactions_WhenOwnerHasReactions_ReturnsCorrectData
  → verify items[0].icon, items[0].user.displayName
  → verify summary.totalCount, summary.icons có đủ 5 icons
GET_PostReactions_WithIconFilter_ReturnsOnlyMatchingIcon
GET_PostReactions_WithInvalidIcon_Returns400
GET_PostReactions_WithLimitOver100_Returns400
GET_PostReactions_Pagination_SecondPageReturnsDifferentItems
  → seed > 30 reactions, page 1 lấy 30, dùng nextCursor lấy page 2
```

---

### ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] Swagger: `GET /api/v1/posts/{postId}/reactions` hiện đúng route + auth
- [ ] Response shape khớp với mock FE đã thống nhất (điều chỉnh `code: null` thay `POST_REACTIONS_RETRIEVED`)

---

## Thứ tự thực hiện

```
Slice 1: Repository     →  dotnet build ✅
Slice 2: Application    →  RED test → implement → GREEN test ✅
Slice 3: API + Int test →  wire controller → E2E test ✅
```

**Không có migration** — `PostReactions` table và index đã tồn tại, FK đến `Users` đã cấu hình.
