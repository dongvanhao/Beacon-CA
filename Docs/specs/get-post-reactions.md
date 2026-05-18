# Feature: Get Post Reactions (Xem danh sách người đã react bài đăng)

## Objective

Cho phép người dùng đã xác thực xem danh sách những ai đã thả reaction trên một bài đăng,
có thể lọc theo loại icon. Dùng cursor pagination để tránh làm nặng response feed chính.

---

## Target Users

| Role | Quyền |
|---|---|
| Authenticated User (xem được bài) | Xem danh sách reaction của bài mình hoặc bài bạn bè |
| Authenticated User (không xem được bài) | 403 POST_ACCESS_DENIED |
| Unauthenticated | 401 |

**Authorization rule**: kế thừa logic visibility của `GetPostDetailQueryHandler` —
bài `friends` chỉ cho chủ bài và bạn bè xem; bài `private` chỉ cho chủ bài xem.

---

## Core Features & Use Cases

### UC-1: Lấy danh sách reaction của bài đăng (có cursor pagination)

**Request**
```
GET /api/v1/posts/{postId:guid}/reactions?icon={icon}&cursor={cursor}&limit={limit}
```

| Param | Loại | Mô tả |
|---|---|---|
| `postId` | path, guid, bắt buộc | ID bài đăng |
| `icon` | query, string, tuỳ chọn | Lọc theo icon: `heart` \| `haha` \| `like` \| `sad` \| `wow` |
| `cursor` | query, string, tuỳ chọn | ISO-8601 UTC datetime — trả reactions có `createdAtUtc` < cursor |
| `limit` | query, int, tuỳ chọn | Mặc định 30, tối đa 100 |

**Acceptance Criteria**
- [ ] Trả `200` + danh sách users đã react, sắp xếp `createdAtUtc DESC`.
- [ ] Không trả `email`, `phone`, `username` — chỉ `id`, `displayName`, `avatarUrl`.
- [ ] Lọc theo `icon` nếu có; bỏ qua nếu `icon` không hợp lệ → `400 VALIDATION_ERROR`.
- [ ] Cursor pagination: `nextCursor` = `createdAtUtc` của item cuối cùng (ISO UTC); `null` khi hết trang.
- [ ] `limit` vượt 100 → `400 VALIDATION_ERROR`.
- [ ] `postId` không tồn tại / đã xóa → `404 POST_NOT_FOUND`.
- [ ] Người dùng không có quyền xem bài → `403 POST_ACCESS_DENIED`.
- [ ] Không có reaction nào → trả `items: []`, `nextCursor: null`, `summary` = tất cả 0.

**Success Response**
```json
{
  "success": true,
  "message": "Post reactions retrieved successfully",
  "code": null,
  "data": {
    "items": [
      {
        "reactionId": "4a0c4a89-70de-4d13-b4e6-37cda4b8f3b0",
        "icon": "heart",
        "reactedAtUtc": "2026-05-18T09:20:11.000Z",
        "user": {
          "id": "6e38a5b1-6f11-4649-b445-c2fcb3e4df70",
          "displayName": "Minh Anh",
          "avatarUrl": "https://cdn.example.com/avatars/u1.jpg"
        }
      }
    ],
    "summary": {
      "totalCount": 12,
      "icons": {
        "heart": 6,
        "like": 4,
        "haha": 2,
        "sad": 0,
        "wow": 0
      }
    },
    "nextCursor": "2026-05-18T09:15:00.000Z",
    "hasMore": true
  },
  "errors": null
}
```

> **Lưu ý**: `summary` luôn trả đủ 5 icon kể cả khi count = 0, để FE không cần handle missing key.
> `summary` là tổng **tất cả** reaction của bài (không filter theo `icon` param).

---

## Out of Scope

- Xem reaction của comment (feature khác).
- Export / download danh sách reactor.
- Real-time update (WebSocket) — phase sau.
- Admin xem reaction ẩn danh.

---

## Technical Approach (Clean Architecture)

### Domain Layer

**Không thêm entity mới.** Chỉ mở rộng `IPostReactionRepository`:

```csharp
// Thêm vào Domain/IRepository/Posts/IPostReactionRepository.cs
Task<(List<PostReaction> Items, bool HasMore)> GetPagedByPostIdAsync(
    Guid postId,
    string? iconFilter,
    DateTime? cursor,
    int limit,
    CancellationToken ct = default);

Task<List<PostReaction>> GetAllByPostIdAsync(
    Guid postId,
    CancellationToken ct = default);
```

> `GetAllByPostIdAsync` dùng để tính `summary` (tổng tất cả icons). Tách riêng để
> không bị ảnh hưởng bởi `iconFilter` của paged query.

### Application Layer

**Query**
```
Application/Features/Posts/Queries/GetPostReactions/
  GetPostReactionsQuery.cs
  GetPostReactionsQueryHandler.cs
```

```csharp
public record GetPostReactionsQuery(
    Guid PostId,
    Guid CurrentUserId,
    string? Icon,
    string? Cursor,
    int Limit
) : IRequest<Result<PostReactionListResponse>>;
```

**Handler logic**
1. Load post → `POST_NOT_FOUND` nếu null.
2. Kiểm tra quyền xem (visibility + friendship) → `POST_ACCESS_DENIED` nếu không có quyền.
3. Parse `cursor` string → `DateTime? cursorDt`.
4. Gọi `GetPagedByPostIdAsync(postId, icon, cursorDt, limit + 1)` — lấy dư 1 để detect `hasMore`.
5. Gọi `GetAllByPostIdAsync(postId)` → tính `summary` bằng `ReactionSummaryHelper`.
6. Map items → `PostReactionItemResponse` (join với User info qua navigation property).
7. Return `PostReactionListResponse`.

**DTOs**
```
Application/Features/Posts/Dtos/
  PostReactionItemResponse.cs      ← item trong danh sách
  ReactorUserResponse.cs           ← user sub-object (id, displayName, avatarUrl)
  PostReactionListResponse.cs      ← wrapper (items, summary, nextCursor, hasMore)
```

**Validator**
```
Application/Features/Posts/Validators/
  GetPostReactionsQueryValidator.cs
```
- `Limit` trong `[1, 100]`.
- `Icon` nếu có phải thuộc `ReactionIcons.Supported`.
- `Cursor` nếu có phải parse được thành `DateTime`.

### Infrastructure Layer

**Repository** — `Infrastructure/Repository/Posts/PostReactionRepository.cs`

Thêm 2 method mới:
- `GetPagedByPostIdAsync`: keyset cursor trên `CreatedAtUtc DESC`, optional filter `Icon`.
- `GetAllByPostIdAsync`: load tất cả reaction của postId (không có cursor/limit).

> Cả hai method dùng **projection** — không load navigation property Post, chỉ load
> `PostReaction` + `User` (join trong EF để lấy `DisplayName`, `AvatarUrl`).

**EF join cần thiết**: `PostReaction` → `User` qua `UserId`. Kiểm tra xem
`PostReactionConfiguration` đã cấu hình FK chưa — nếu chưa thì thêm.

### Presentation / API Layer

**Controller** — thêm action vào `PostsController`:

```csharp
GET /api/v1/posts/{postId:guid}/reactions
```

Không tạo controller mới.

---

## Error Codes (dùng code đã có)

| Code | ErrorType | HTTP | Khi nào |
|---|---|---|---|
| `VALIDATION_ERROR` | Validation | 400 | `icon` không hợp lệ, `limit` > 100, `cursor` sai format |
| `POST_NOT_FOUND` | NotFound | 404 | `postId` không tồn tại / đã soft-delete |
| `POST_ACCESS_DENIED` | Forbidden | 403 | Không có quyền xem bài |

> Tất cả 3 code đã có trong `ErrorCodes.cs` — không cần thêm mới.

---

## Code Style & Architecture

- Validator target `GetPostReactionsQuery` (MediatR pipeline tự intercept).
- Handler không inject `AppDbContext` — chỉ qua `IPostReactionRepository` và `IPostRepository`.
- Mapper: thêm method `ToReactionItemResponse` vào `PostDtoMapper` (mapper đã có) thay vì tạo mapper mới.
- Response `summary` luôn include đủ 5 icon — dùng `ReactionSummaryHelper` hiện có.

---

## Testing Strategy

### Unit Tests — `tests/Beacon.UnitTests/Posts/`

| File | Scenario cần cover |
|---|---|
| `GetPostReactionsHandlerTests.cs` | Post not found → NotFound |
| | Post access denied → Forbidden |
| | Icon filter invalid → (validator catch trước handler) |
| | Happy path: trả đúng items + summary + cursor |
| | Empty reactions → items=[], summary all zeros |
| | HasMore=true khi còn trang tiếp |
| | HasMore=false khi hết |

### Integration Tests — `tests/Beacon.IntergrationTests/Posts/`

| File | Scenario |
|---|---|
| `PostReactionsControllerTests.cs` | `GET /api/v1/posts/{id}/reactions` → 200 với dữ liệu thật |
| | Icon filter → chỉ trả reaction đúng loại |
| | Unauthenticated → 401 |
| | Post private, non-owner → 403 |
| | Cursor pagination → page 2 đúng |

---

## Boundaries

### Always Do
- `summary` tính từ **tất cả** reaction của bài, không bị ảnh hưởng bởi `icon` filter.
- Join User trong DB query (không load rời rồi loop trong memory).
- Dùng keyset pagination (`WHERE CreatedAtUtc < @cursor`) thay vì `Skip()`.

### Ask First
- Thêm index DB mới nếu query chậm (sẽ xem sau khi có integration test).
- Mở rộng thêm visibility rule mới (bạn bè của bạn bè, public, v.v.).

### Never Do
- Trả `email`, `phone`, `username` trong response.
- Dùng offset pagination (`Skip/Take`) cho endpoint này.
- Đặt business logic (kiểm tra quyền) trong Repository hoặc Controller.
