# Security — Beacon

> Authorization attribute usage → `api-conventions/RULE.md`.

---

## 🚨 Tuyệt đối không

| ❌ | ✅ |
|---|---|
| Secrets trong `appsettings.json` | User Secrets (dev) / Key Vault / AWS Secrets Manager (prod) |
| Commit `.env` / `appsettings.*.json` có secret | `.gitignore` rồi — check trước push |
| Log password, token, email, phone | Chỉ log `userId`, error code, correlation ID |
| Raw SQL string concat | EF parameterized (LINQ ưu tiên) |
| Hard delete entity nhạy cảm | `SoftDeletableEntity` → `IsDeleted = true` |

---

## Secrets

```bash
# Dev
dotnet user-secrets set "Jwt:Key" "..."

# Prod: biến môi trường / Key Vault — bind qua IConfiguration
```

`JwtService` **chỉ** đọc key từ `IConfiguration` — không hardcode, không fallback value.

---

## Auth / Password

- Password hash: **ASP.NET Core Identity `IPasswordHasher<T>`** — không dùng thư viện khác, không tự hash.
- JWT: Access 15p · Refresh 7d · single-session (revoke tokens cũ khi login lại) · rotation.
- `JwtService` (`Infrashtructure/Services/JwtService.cs`) = **nơi duy nhất** tạo/validate token.

---

## Authorization — checklist mỗi endpoint

- [ ] Có `[AllowAnonymous]` / `[Authorize]` / `[AdminOnly]` / `[HasPermission("x:y")]` đúng chưa?
- [ ] Nếu trả data user-specific: có **owner check** (`entity.UserId == currentUser.UserId`) chưa?
- [ ] Admin endpoint: có `[AdminOnly]` + `[HasPermission]` chưa?

```csharp
// ✅ Owner check trong HANDLER (không phải controller)
if (media.UploadedByUserId != userId)
    return Result.Failure<MediaDto>(Error.Forbidden("MEDIA_FORBIDDEN", "..."));
```

---

## Input Validation

`ValidationBehavior` (MediatR pipeline) **tự** chạy FluentValidation trước handler — **không** gọi validator thủ công.

Mọi Command/Query có input = **bắt buộc** có Validator:

```csharp
public class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.Length).LessThanOrEqualTo(100 * 1024 * 1024);
    }
}
```

Fail → pipeline throw `ValidationException` → middleware trả 400.

---

## SQL Injection

```csharp
// ❌ concat
db.Users.FromSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'");

// ✅ parameterized
db.Users.FromSqlRaw("SELECT * FROM Users WHERE Email = {0}", email);

// ✅ LINQ (ưu tiên)
db.Users.Where(u => u.Email == email);
```

---

## CORS & Rate Limiting

- CORS: config ở `Api/Extensions/AuthExtensions.cs` — **whitelist origin cụ thể**, không `AllowAnyOrigin()` ở prod.
- Rate limit: chưa triển khai (track ở `CLAUDE.md § Open Tech Debt`). Khi thêm: auth endpoints phải chặt hơn API thường.
