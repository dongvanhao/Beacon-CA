---
name: create-auth-handler
description: Tham khảo pattern Auth (Register/Login/Logout/RefreshToken) đã implement cho User trong Beacon API
---

## Khi nào dùng

**User Auth đã implement sẵn — KHÔNG chạy lại để tạo lại.**

Dùng skill này khi:
- Cần tạo auth flow cho **entity mới** (không phải User hoặc Admin) theo cùng pattern
- Cần tra cứu quyết định thiết kế để tránh làm sai ở module khác

Để tham khảo Admin Auth (RBAC), dùng `/create-admin-auth`.

## Cách gọi

```
/create-auth-handler
```
Sau đó cho biết entity cần tạo auth (nếu không phải User).

## Files tham khảo (đã tồn tại — đọc trước khi viết)

```
src/Beacon.Application/Features/Identity/Commands/
├── RegisterCommandHandler.cs   ← mẫu: hash + create + issue tokens
├── LoginCommandHandler.cs      ← mẫu: verify BCrypt + RecordLogin + issue tokens
├── LogoutCommandHandler.cs     ← mẫu: revoke refresh token
└── RefreshTokenCommandHandler.cs ← mẫu: token rotation

src/Beacon.Application/Common/Interfaces/IService/IJwtService.cs
src/Beacon.Domain/IRepository/IUserRepository.cs
src/Beacon.Infrashtructure/Services/JwtService.cs   ← dùng IOptions<JwtSettings>
src/Beacon.Api/Controllers/AuthController.cs
```

## Thứ tự tạo (nếu tạo cho entity mới)

```
1. I{Entity}Repository          → Domain/IRepository/
2. DTOs (Request + Response)    → Application/Features/{Module}/Dtos/
3. Validators                   → Application/Features/{Module}/Validators/  (auto-discovered)
4. Commands + Handlers          → Application/Features/{Module}/Commands/
5. Repository impl              → Infrashtructure/Repository/{Module}/
6. DI trong Program.cs
7. Controller kế thừa BaseController
```

## Quyết định thiết kế không đổi

| Vấn đề | Quyết định |
|---|---|
| Password | `BCrypt.HashPassword` / `BCrypt.Verify` |
| Config JWT | `IOptions<JwtSettings>` — KHÔNG inject `IConfiguration` trực tiếp |
| Refresh token | `RandomNumberGenerator` 64 bytes (CSPRNG, không dùng `Guid`) |
| Token expiry | `GenerateAccessToken` trả tuple `(token, expiresAt)` — Handler không tự tính |
| Error message | Cùng message cho "email không tồn tại" và "sai password" → tránh user enumeration |
| ClockSkew | `TimeSpan.Zero` — token hết hạn đúng giờ |

## Gotchas

- `app.UseAuthentication()` phải đứng **trước** `app.UseAuthorization()` trong pipeline
- `[AllowAnonymous]` trên Register/Login/Refresh — thiếu sẽ bị JWT middleware block
- `RefreshToken.User` navigation property phải tồn tại để `Include(rt => rt.User)` hoạt động

## Sau khi tạo

```bash
dotnet build                    # phải clean
/write-unit-test                # viết test cho handlers vừa tạo
dotnet test --no-build
```
