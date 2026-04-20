# Security — Beacon

> Chỉ ghi quyết định cụ thể của Beacon. Authorization attribute usage → xem `api-conventions/RULE.md`.

---

## 🚨 Tuyệt đối không làm

| ❌ | ✅ |
|---|---|
| Secrets trong `appsettings.json` | User Secrets (dev) / Azure Key Vault / AWS Secrets Manager (prod) |
| Commit `.env` / `appsettings.*.json` chứa secret | `.gitignore` đã có — kiểm tra trước khi push |
| Log password, token, email, phone | Chỉ log `userId`, error code, correlation ID |
| Raw SQL string concatenation | EF Core parameterized queries (mặc định an toàn) |
| Hard delete entity nhạy cảm | `SoftDeletableEntity` — chỉ đánh `IsDeleted = true` |

---

## Secrets Management

```json
// ❌ appsettings.json — KHÔNG đặt secret ở đây
{ "Jwt": { "Key": "super-secret-key" } }

// ✅ User Secrets (dev): dotnet user-secrets set "Jwt:Key" "..."
// ✅ Prod: biến môi trường hoặc Key Vault — bind qua IConfiguration
```

`JwtService` đọc key từ `IConfiguration` — không bao giờ hardcode.

---

## Authentication & Password

- Password hash: **ASP.NET Core Identity `IPasswordHasher<T>`** — không dùng thư viện khác, không tự hash.
- JWT: Access Token 15 phút, Refresh Token 7 ngày, single-session (revoke all cũ khi login lại), có token rotation.
- `JwtService` (`Beacon.Infrashtructure/Services/JwtService.cs`) là nơi duy nhất tạo/validate token.

---

## Authorization — Checklist mỗi endpoint mới

Trước khi merge endpoint mới, kiểm tra:

- [ ] Có attribute `[AllowAnonymous]` / `[Authorize]` / `[AdminOnly]` / `[HasPermission("x:y")]` đúng không?
- [ ] Nếu trả data của một user cụ thể: có kiểm tra **owner** (`uploadedByUserId == currentUser.UserId`) không?
- [ ] Admin endpoint: có `[AdminOnly]` + `[HasPermission]` không?

```csharp
// ✅ Owner check trong handler — KHÔNG kiểm tra trong controller
if (media.UploadedByUserId != userId)
    return Result.Failure<MediaDto>(Error.Forbidden("MEDIA_FORBIDDEN", "..."));
```

---

## Input Validation

`ValidationBehavior` (MediatR pipeline) tự động chạy FluentValidation trước mọi handler — **không cần gọi validator thủ công trong handler**.

Mọi `Command` / `Query` có dữ liệu đầu vào **bắt buộc** có class `Validator` tương ứng:

```csharp
// Beacon.Application/Features/{Module}/Validators/{Module}/XyzValidator.cs
public class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.Length).LessThanOrEqualTo(100 * 1024 * 1024);
    }
}
```

Khi validation fail → pipeline tự ném `ValidationException` → `ExceptionHandlingMiddleware` trả 400.

---

## SQL Injection

EF Core dùng parameterized query mặc định — an toàn miễn là không dùng `FromSqlRaw` với string concatenation:

```csharp
// ❌
_db.Users.FromSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'");

// ✅
_db.Users.FromSqlRaw("SELECT * FROM Users WHERE Email = {0}", email);
// hoặc dùng LINQ (ưu tiên)
_db.Users.Where(u => u.Email == email);
```

---

## CORS & HTTP Security

Cấu hình CORS trong `Beacon.Api/Extensions/AuthExtensions.cs` — chỉ whitelist origin cụ thể, không dùng `AllowAnyOrigin()` trong production.

Rate limiting: chưa triển khai (tracked trong CLAUDE.md → Operations). Khi thêm: auth endpoints giới hạn chặt hơn API thông thường.
