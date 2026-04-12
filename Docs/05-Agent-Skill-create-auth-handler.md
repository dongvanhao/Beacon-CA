# Agent Skill: `/create-auth-handler`

> Tài liệu hướng dẫn sử dụng Agent Skill cho Identity/Auth module  
> Cập nhật: 2026-04-12

---

## 1. CLO4 là gì và skill này đáp ứng thế nào

**CLO4** yêu cầu sinh viên:
- Sử dụng thành thạo công cụ AI để tối ưu quy trình viết mã, kiểm thử, xử lý lỗi
- Có **phương pháp sinh mã theo best practice** (file `SKILL.md`, `CLAUDE.md`)
- **Kiểm soát đầu ra** — không chấp nhận code AI sinh ra mà không hiểu

Skill `/create-auth-handler` đáp ứng CLO4 theo cách sau:

| Yêu cầu CLO4 | Cách skill đáp ứng |
|---|---|
| Phương pháp sinh mã | 9 bước có thứ tự bắt buộc, mỗi bước có namespace + file path chính xác |
| Best practice | Tuân thủ Clean Architecture, Result Pattern, không throw exception cho business failure |
| Kiểm soát bảo mật | Có mục _Security notes_ riêng (BCrypt, user enumeration prevention, CSPRNG) |
| Hiểu mã sinh ra | Mỗi file có giải thích mục đích, dependency, và _gotchas_ thường gặp |

---

## 2. Khi nào dùng skill này

Dùng `/create-auth-handler` **thay cho** `/create-endpoint` khi feature là **Authentication**:

```
/create-endpoint  →  CRUD thông thường (GET/POST/PUT/DELETE trên một resource)
/create-auth-handler  →  Register / Login / Logout với JWT + BCrypt
```

**Không dùng skill này khi:**
- Refresh token endpoint (chưa có trong skill — cần implement thủ công)
- OAuth2 / social login
- Admin-only auth flows khác với User flow

---

## 3. Cách gọi skill

Trong Claude Code CLI (terminal hoặc VSCode extension):

```
/create-auth-handler
```

Claude sẽ đọc `.claude/skills/create-auth-handler/SKILL.md` và sinh code theo 9 bước sau:

```
1. IUserRepository          → src/Beacon.Domain/IRepository/
2. IJwtService              → src/Beacon.Application/Common/Interfaces/IService/
3. DTOs                     → src/Beacon.Application/Features/Identity/Dtos/
4. Validators               → src/Beacon.Application/Features/Identity/Validators/
5. Commands + Handlers      → src/Beacon.Application/Features/Identity/Commands/
6. UserRepository impl      → src/Beacon.Infrashtructure/Repository/Identity/
7. JwtService impl          → src/Beacon.Infrashtructure/Services/
8. DI registration          → src/Beacon.Api/Program.cs
9. AuthController           → src/Beacon.Api/Controllers/
```

---

## 4. Checklist kiểm soát đầu ra (BẮT BUỘC trước khi commit)

> Đây là điểm cốt lõi của CLO4: **không được dùng AI mà không hiểu code sinh ra**.

Với **mỗi file** Claude tạo ra, bạn phải trả lời được 3 câu này:

### Câu 1 — File này làm gì? (1 câu mô tả)

| File | Mô tả bắt buộc phải hiểu |
|---|---|
| `IUserRepository.cs` | Contract để truy vấn/lưu User và RefreshToken — Domain layer không biết EF Core |
| `IJwtService.cs` | Contract để sinh access token (JWT) và refresh token (random bytes) |
| `RegisterCommandHandler.cs` | Kiểm tra email conflict → hash password → tạo User → sinh tokens → lưu DB |
| `LoginCommandHandler.cs` | Tìm user → kiểm tra IsActive → verify BCrypt → ghi LastLoginAtUtc → sinh tokens |
| `LogoutCommandHandler.cs` | Tìm refresh token đang active → gọi `Revoke()` → lưu DB |
| `UserRepository.cs` | EF Core implementation của `IUserRepository` — query trực tiếp `AppDbContext` |
| `JwtService.cs` | Đọc `JwtSettings` từ config → ký token bằng HMAC-SHA256 → sinh refresh token bằng CSPRNG |
| `AuthController.cs` | HTTP layer — nhận request, gửi MediatR command, trả `ApiResponse<T>` |

### Câu 2 — Nó phụ thuộc vào class/interface nào?

```
RegisterCommandHandler
  ├── IUserRepository (Domain) ← UserRepository (Infrastructure)
  ├── IJwtService (Application) ← JwtService (Infrastructure)
  ├── User.Create() (Domain entity method)
  └── RefreshToken.Create() (Domain entity method)

AuthController
  ├── IMediator (MediatR)
  └── BaseController (Beacon.Api)
```

### Câu 3 — Nếu xóa file này, lỗi xảy ra ở đâu?

| Xóa file | Lỗi tại |
|---|---|
| `IUserRepository.cs` | Build error tại `RegisterCommandHandler`, `LoginCommandHandler`, `LogoutCommandHandler`, `UserRepository` |
| `UserRepository.cs` | Runtime error: DI container không resolve `IUserRepository` |
| `IJwtService.cs` | Build error tại `RegisterCommandHandler`, `LoginCommandHandler`, `JwtService` |
| `JwtService.cs` | Runtime error: DI container không resolve `IJwtService` |
| `AuthController.cs` | Endpoints `/api/v1/auth/*` không tồn tại — 404 |

---

## 5. Quy trình đầy đủ sau khi dùng skill

```bash
# Bước 1: Chạy skill
/create-auth-handler

# Bước 2: Verify từng file theo checklist ở mục 4

# Bước 3: Build
dotnet build
# → Phải clean, 0 error, 0 warning quan trọng

# Bước 4: Viết unit test cho handlers
/write-unit-test
# → Test: RegisterCommandHandler, LoginCommandHandler, LogoutCommandHandler

# Bước 5: Chạy test
dotnet test --no-build

# Bước 6: Commit nếu pass
# feat(identity): add Register/Login/Logout command handlers with JWT auth
```

---

## 6. Những điều skill đã làm đúng (best practices)

### 6.1. Không expose chi tiết lỗi để tránh user enumeration

```csharp
// ĐÚNG — cùng một message cho cả "email không tồn tại" và "sai password"
return Result<AuthResponse>.Failure(
    Error.Unauthorized(ErrorCodes.Identity.INVALID_CREDENTIALS, "Invalid email or password."));

// SAI — attacker biết được email nào đã đăng ký
return Result.Failure(Error.NotFound(..., "Email not found"));
```

### 6.2. Refresh token dùng CSPRNG, không dùng Guid

```csharp
// ĐÚNG — cryptographically secure random
var bytes = new byte[64];
using var rng = RandomNumberGenerator.Create();
rng.GetBytes(bytes);

// SAI — Guid có thể predictable
return Guid.NewGuid().ToString();
```

### 6.3. ClockSkew = Zero để token hết hạn đúng lúc

```csharp
ClockSkew = TimeSpan.Zero  // Nếu không set, token hết hạn vẫn valid thêm 5 phút
```

### 6.4. Thứ tự middleware đúng

```csharp
app.UseAuthentication();  // PHẢI trước UseAuthorization
app.UseAuthorization();
```

---

## 7. Những điểm skill chưa bao gồm (hạn chế hiện tại)

| Thiếu sót | Mức độ | Cách xử lý thủ công |
|---|---|---|
| Refresh token endpoint (`POST /auth/refresh`) | Quan trọng | Tạo `RefreshTokenCommand` + handler thủ công |
| `AccessTokenExpiresAt` hardcode trong handler | Nhỏ | Đọc `ExpiryMinutes` từ config thay vì `AddMinutes(15)` |
| Logout revoke tất cả devices | Tùy yêu cầu | Thêm method `RevokeAllByUserId` vào `IUserRepository` |
| Rate limiting cho login endpoint | Quan trọng | Cấu hình `AspNetCoreRateLimit` riêng |

---

## 8. Ví dụ minh họa: tracing một request Register

Khi client gửi `POST /api/v1/auth/register`:

```
HTTP Request
    ↓
ExceptionHandlingMiddleware     ← bắt unexpected exception
    ↓
AuthController.Register()       ← nhận RegisterRequest từ body
    ↓
FluentValidation                ← validate email format, password length, fullName
    ↓ (nếu valid)
mediator.Send(RegisterCommand)
    ↓
RegisterCommandHandler.Handle()
    ├── ExistsByEmailAsync()    ← query DB: email đã tồn tại chưa?
    ├── BCrypt.HashPassword()   ← hash password (cost factor mặc định = 12)
    ├── User.Create()           ← tạo domain entity
    ├── AddAsync(user)          ← thêm vào DbContext (chưa save)
    ├── GenerateAccessToken()   ← ký JWT với HMAC-SHA256
    ├── GenerateRefreshToken()  ← 64 random bytes → Base64
    ├── RefreshToken.Create()   ← tạo domain entity
    ├── AddRefreshTokenAsync()  ← thêm vào DbContext
    └── SaveChangesAsync()      ← 1 lần commit duy nhất
    ↓
Result<AuthResponse>.Success()
    ↓
BaseController.CreatedResult()  ← wrap vào ApiResponse<T>, trả HTTP 201
```

---

## 9. Đánh giá đạt/chưa đạt CLO4

| Tiêu chí | Đánh giá | Ghi chú |
|---|---|---|
| Có file instruction (SKILL.md) | **Đạt** | 9 bước, code template đầy đủ |
| Sinh mã theo best practice | **Đạt** | Clean Architecture, Result Pattern, Security notes |
| Kiểm soát đầu ra | **Gần đạt** | Cần thực hiện checklist mục 4 của doc này |
| Không dùng AI mà không hiểu | **Tùy người dùng** | Doc này hỗ trợ — nhưng dev phải tự verify |
| Bao phủ testing | **Thiếu một phần** | Skill không tự gọi `/write-unit-test` — cần làm thủ công |

**Kết luận:** Skill đạt yêu cầu CLO4 nếu dev **kết hợp** với checklist ở mục 4 và gọi `/write-unit-test` sau khi sinh code. Skill không tự động đảm bảo CLO4 — người dùng phải chủ động kiểm soát đầu ra.

---

## 10. Tham khảo

- [SKILL.md](../.claude/skills/create-auth-handler/SKILL.md) — source of truth cho skill
- [CLAUDE.md](../.claude/CLAUDE.md) — conventions toàn project
- [03- Chuẩn Result Pattern và ApiResponse.md](./03-%20Chuẩn%20Result%20Parttern%20và%20ApiResponse.md)
- [04- Hướng dẫn chung cho BaseController.md](./04-%20Hướng%20dẫn%20chung%20cho%20BaseController.md)
