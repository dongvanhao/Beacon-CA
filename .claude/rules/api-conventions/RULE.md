# API Conventions — Beacon

> Chỉ các quy ước **cụ thể** của Beacon. REST cơ bản đã có trong kiến thức AI.

---

## 1. Response Envelope — KHÔNG được thay đổi shape

Mọi response dùng `ApiResponse<T>` (`Beacon.Shared.Common.Responses`). **Không** dùng `204 No Content` — DELETE trả `ApiResponse<T>` với `data = null`.

```json
{ "success": true|false, "message": "...", "code": null|"ERROR_CODE", "data": {...}|null, "errors": null|["..."] }
```

---

## 2. Controller Pattern

```csharp
[Route("api/v1/<resource>")]          // ✅ tường minh — KHÔNG api/[controller]
[Authorize]                           // hoặc [AdminOnly] / [AllowAnonymous]
public class XyzController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpGet("{id:guid}")]            // ✅ {id:guid} — KHÔNG {id}
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(new GetXyzQuery(id), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateXyzRequest req, CancellationToken ct)
        => CreatedResult("api/v1/<resource>", await mediator.Send(new CreateXyzCommand(req), ct));
}
```

| Tình huống | BaseController method | HTTP |
|---|---|---|
| GET / PATCH / PUT / POST action / DELETE | `HandleResult(result)` | 200 |
| POST tạo resource mới | `CreatedResult(location, result)` | 201 |

ErrorType → HTTP (tự map): `Validation=400 · NotFound=404 · Conflict=409 · Unauthorized=401 · Forbidden=403 · Failure=400`.

---

## 3. URL Rules

- Prefix: `api/v1/...` (bắt buộc). Admin: `api/v1/admin/...`
- Path segments: **kebab-case** (`/refresh-token`, `/check-email`, `/hard`)
- Collections: danh từ **số nhiều** (`/users`, `/media`, `/devices`)
- Route params: `{id:guid}`
- Sub-resource: `PATCH /api/v1/users/me`, `PUT /api/v1/users/me/avatar`, `DELETE /api/v1/media/{id:guid}/soft`

---

## 4. Naming (field / code / error)

| Thành phần | Convention | Ví dụ |
|---|---|---|
| JSON field | camelCase | `accessToken`, `avatarMediaObjectId` |
| Query param | camelCase | `?cursor=...&limit=20` |
| DateTime field | hậu tố `AtUtc` | `createdAtUtc`, `lastLoginAtUtc` |
| Error `code` | SCREAMING_SNAKE_CASE | `USER_NOT_FOUND` |

---

## 5. Pagination — chọn đúng loại

| Loại | Khi nào | Type | Query |
|---|---|---|---|
| **Cursor** (mặc định cho feed/timeline) | User-facing danh sách | `CursorPagedResult<T>` | `?cursor=<ISO-UTC>&limit=20` (max 100) |
| **Offset** (admin list cần totalCount) | Cần biết tổng số trang | `PaginatedList<T>.CreateAsync(src, page, pageSize, ct)` | `?page=1&pageSize=20` |

**KHÔNG** dùng offset cho feed/timeline.

---

## 6. Authorization Attributes

| Attribute | Dùng khi |
|---|---|
| `[AllowAnonymous]` | Public |
| `[Authorize]` | Cần Access Token hợp lệ |
| `[AdminOnly]` | Chỉ admin token (`isAdmin` claim = true) |
| `[HasPermission("resource:action")]` | RBAC |

Token: Access 15 phút · Refresh 7 ngày · single-session · rotation.

---

## 7. File Upload

```csharp
[HttpPost, Authorize]
[RequestSizeLimit(110L * 1024 * 1024)]   // ảnh ≤ 10MB, video ≤ 100MB
public async Task<IActionResult> Upload([FromForm] UploadMediaRequest req, CancellationToken ct)
    => CreatedResult("api/v1/media", await mediator.Send(...));
```

- `multipart/form-data`, field `file`. Presigned URL trong response: **TTL 15 phút**.

---

## 8. XML Doc — BẮT BUỘC cho mọi endpoint

```csharp
#region
/// <summary>[1 câu mô tả hành động]</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c> (hoặc "Không yêu cầu token")
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: [mô tả].
/// - <c>XYZ_CODE</c>: [mô tả].
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>{ "field": "type (ghi chú)" }</code>
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
```

**Bắt buộc liệt kê toàn bộ `code`** — bao gồm success (`null`).

---

## 9. Error Codes chuẩn (trích)

Thêm code mới vào `Beacon.Shared/Constants/ErrorCodes.cs` (SCREAMING_SNAKE_CASE) trước khi dùng.

| Code | ErrorType |
|---|---|
| `VALIDATION_ERROR` | Validation |
| `INVALID_CREDENTIALS`, `ACCOUNT_INACTIVE`, `ADMIN_INACTIVE`, `TOKEN_INVALID` | Unauthorized |
| `USERNAME_ALREADY_EXISTS`, `EMAIL_ALREADY_EXISTS`, `PHONE_ALREADY_EXISTS` | Conflict |
| `USER_NOT_FOUND`, `MEDIA_NOT_FOUND` | NotFound |
| `MEDIA_FORBIDDEN` | Forbidden |
| `INVALID_FILE_TYPE`, `FILE_TOO_LARGE` | Validation |
| `UPLOAD_FAILED`, `STORAGE_UNAVAILABLE` | Failure |

---

## 10. Anti-patterns

| ❌ | ✅ |
|---|---|
| `[Route("api/[controller]")]` | Route tường minh trên từng controller |
| `return NoContent()` (204) | `HandleResult(result)` — data=null |
| `{ "error": { "code", "details" } }` | Flat envelope `{ success, message, code, data, errors }` |
| `{ "pagination": {...} }` top-level | Pagination nằm trong `data` |
| `{id}` trong route | `{id:guid}` |
| Offset pagination cho feed | `CursorPagedResult<T>` |
