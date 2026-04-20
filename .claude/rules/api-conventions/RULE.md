# API Conventions — Beacon

> Rule này tập trung vào **các quyết định cụ thể của Beacon** mà AI hay nhầm.
> Kiến thức REST chung (GET=idempotent, v.v.) không lặp lại ở đây.

---

## 1. Response Envelope — KHÔNG được thay đổi shape

Mọi response dùng `ApiResponse<T>` (`Beacon.Shared.Common.Responses`). **Không dùng shape khác.**

```json
// Success
{ "success": true,  "message": "...", "code": null,               "data": {...}, "errors": null }
// Error
{ "success": false, "message": "...", "code": "USER_NOT_FOUND",   "data": null,  "errors": ["..."] }
```

**Không dùng** `204 No Content` — kể cả DELETE phải trả `ApiResponse<T>` (data = null).

---

## 2. Controller Pattern

```csharp
[Route("api/v1/<resource>")]          // ✅ Tường minh — KHÔNG dùng api/[controller]
[Authorize]                            // hoặc [AdminOnly] / [AllowAnonymous]
public class XyzController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpGet("{id:guid}")]             // ✅ {id:guid} — KHÔNG dùng {id}
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetXyzQuery(id, currentUser.UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateXyzRequest req, CancellationToken ct)
        => CreatedResult("api/v1/<resource>", await mediator.Send(new CreateXyzCommand(req), ct));
}
```

### BaseController — chọn đúng method

| Tình huống | Dùng | HTTP Status |
|---|---|---|
| GET / PATCH / PUT / POST action | `HandleResult(result)` | 200 OK |
| POST tạo resource mới | `CreatedResult(location, result)` | 201 Created |
| Action không trả data (DELETE, logout) | `HandleResult(result)` | 200 OK, data=null |

`ErrorType → HTTP` được map tự động trong `BaseController.MappErrorToResponse()`:

```
Validation → 400  |  NotFound → 404  |  Conflict → 409
Unauthorized → 401  |  Forbidden → 403  |  Failure → 400
```

---

## 3. URL Rules

- Prefix: `api/v1/...` (bắt buộc) — admin: `api/v1/admin/...`
- Path segments: **kebab-case** (`/refresh-token`, `/check-email`, `/hard`)
- Collections: **danh từ số nhiều** (`/users`, `/media`, `/devices`)
- Route params: **`{id:guid}`** (không dùng `{id}`)
- Sub-resource: `PATCH /api/v1/users/me`, `PUT /api/v1/users/me/avatar`, `DELETE /api/v1/media/{id:guid}/soft`

---

## 4. Naming Conventions

| Thành phần | Convention | Ví dụ |
|---|---|---|
| JSON field | camelCase | `accessToken`, `avatarMediaObjectId` |
| Query param | camelCase | `?cursor=...&limit=20` |
| DateTime field | hậu tố `AtUtc` | `createdAtUtc`, `lastLoginAtUtc` |
| Error `code` | SCREAMING_SNAKE_CASE | `USER_NOT_FOUND`, `MEDIA_FORBIDDEN` |

---

## 5. Pagination — chọn đúng loại

### Cursor (feed/timeline — mặc định cho danh sách user-facing)

Dùng `CursorPagedResult<T>`. Query: `?cursor=<ISO-datetime-UTC>&limit=20` (max 100).

```json
// data field trong ApiResponse
{
  "data": [...],
  "meta": { "nextCursor": "2026-04-19T08:00:00Z", "limit": 20, "hasMore": true }
}
```

### Offset (admin list — khi cần biết tổng số trang)

Dùng `PaginatedList<T>.CreateAsync(source, page, pageSize, ct)`. Query: `?page=1&pageSize=20`.

```json
// data field trong ApiResponse
{
  "items": [...],
  "totalCount": 100, "page": 1, "pageSize": 20,
  "totalPages": 5, "hasNextPage": true, "hasPreviousPage": false
}
```

**Không bao giờ** dùng offset pagination cho feed/timeline → dùng cursor.

---

## 6. Authorization Attributes

| Attribute | Dùng khi |
|---|---|
| `[AllowAnonymous]` | Public — không cần token |
| `[Authorize]` | Cần Access Token hợp lệ |
| `[AdminOnly]` | Chỉ admin token (`isAdmin` claim = true) |
| `[HasPermission("x:y")]` | RBAC — permission format: `resource:action` |

Token: Access = 15 phút, Refresh = 7 ngày, single-session, có rotation.

---

## 7. File Upload

```csharp
[HttpPost]
[Authorize]
[RequestSizeLimit(110L * 1024 * 1024)]   // ảnh ≤ 10MB, video ≤ 100MB
public async Task<IActionResult> Upload([FromForm] UploadMediaRequest request, CancellationToken ct)
    => CreatedResult("api/v1/media", await mediator.Send(...));
```

- Body: `multipart/form-data`, field tên `file`.
- Presigned URL trong response có hiệu lực **15 phút**.

---

## 8. XML Doc — BẮT BUỘC cho mọi endpoint

Template (copy nguyên, điền vào chỗ trống):

```csharp
#region
/// <summary>
/// [1 câu mô tả hành động]
/// </summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>  ← hoặc "Không yêu cầu token" nếu public
///
/// Các giá trị <c>code</c> có thể xuất hiện trong response:
///
/// - <c>null</c>: Thành công (success = true)[, data = null].
/// - <c>VALIDATION_ERROR</c>: [mô tả trường hợp].
/// - <c>XYZ_CODE</c>: [mô tả].
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// { "field": "type  (ghi chú)" }
/// </code>
///
/// Format response chuẩn: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
```

**Bắt buộc liệt kê toàn bộ `code`** có thể trả về — bao gồm cả success (`null`).

---

## 9. Error Codes chuẩn

Thêm code mới vào `Beacon.Shared/Constants/ErrorCodes.cs` (SCREAMING_SNAKE_CASE).

| Code | ErrorType | Mô tả |
|---|---|---|
| `VALIDATION_ERROR` | Validation | Input không hợp lệ |
| `INVALID_CREDENTIALS` | Unauthorized | Sai username/password |
| `USERNAME_ALREADY_EXISTS` | Conflict | Username đã tồn tại |
| `EMAIL_ALREADY_EXISTS` | Conflict | Email đã tồn tại |
| `PHONE_ALREADY_EXISTS` | Conflict | SĐT đã tồn tại |
| `ACCOUNT_INACTIVE` | Unauthorized | Tài khoản bị vô hiệu hóa |
| `ADMIN_INACTIVE` | Unauthorized | Tài khoản admin bị vô hiệu hóa |
| `TOKEN_INVALID` | Unauthorized | Refresh token hết hạn/đã revoke |
| `USER_NOT_FOUND` | NotFound | Không tìm thấy user |
| `MEDIA_NOT_FOUND` | NotFound | Không tìm thấy media |
| `MEDIA_FORBIDDEN` | Forbidden | Không phải chủ sở hữu |
| `INVALID_FILE_TYPE` | Validation | Loại file không hỗ trợ |
| `FILE_TOO_LARGE` | Validation | Vượt dung lượng cho phép |
| `UPLOAD_FAILED` | Failure | Lỗi upload lên MinIO |
| `STORAGE_UNAVAILABLE` | Failure | MinIO không khả dụng |

---

## 10. Anti-patterns — KHÔNG làm

| ❌ Không làm | ✅ Thay bằng |
|---|---|
| `[Route("api/[controller]")]` trên BaseController | Route tường minh trên từng controller |
| `return NoContent()` (204) | `return HandleResult(result)` — data=null |
| `{ "error": { "code", "details" } }` | Flat: `{ success, message, code, data, errors }` |
| `{ "pagination": {...} }` top-level | Pagination nằm trong `data` object |
| `{id}` trong route | `{id:guid}` |
| Offset pagination cho feed/timeline | `CursorPagedResult<T>` |
