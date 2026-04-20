# Feature: Change Password (Self-Service)

## Objective

Cho phép **user đã đăng nhập** tự đổi mật khẩu của chính mình sau khi verify mật khẩu hiện tại, đồng thời revoke tất cả refresh token để buộc đăng nhập lại trên mọi thiết bị — giảm rủi ro khi mật khẩu bị rò rỉ hoặc session cũ bị compromise.

---

## Target Users

- **User thường** (có Access Token hợp lệ, `isAdmin` claim = `false`).
- **Không áp dụng** cho Admin/SuperAdmin — Admin có flow riêng (ngoài scope).
- Authorization: `[Authorize]` (access token user). Handler không kiểm tra thêm role vì flow này chỉ tác động lên chính user đó.

---

## Core Features & Use Cases

### UC-1: User đổi mật khẩu thành công

**Acceptance Criteria:**
- User gửi `currentPassword` + `newPassword` qua `PATCH /api/v1/users/me/password`.
- BCrypt verify `currentPassword` khớp với `PasswordHash` hiện tại.
- `newPassword` pass toàn bộ password policy (giống Register).
- `newPassword` **khác** `currentPassword`.
- Hash mới (BCrypt) được persist vào `User.PasswordHash`.
- **Tất cả refresh token** active của user bị revoke (`RevokedAtUtc = UtcNow`).
- Trả `200 OK` với `data = null`, `message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."`.
- Access token hiện tại **vẫn còn hiệu lực** đến khi hết hạn (15 phút) — client nên tự logout & login lại; backend không thể revoke access token JWT đã phát.

### UC-2: Current password sai

- BCrypt verify fail → trả `401 Unauthorized`, code = `INVALID_CURRENT_PASSWORD`.
- Không update gì trong DB.

### UC-3: New password vi phạm policy

- ValidationBehavior pipeline throw `ValidationException` → `400 Bad Request`, code = `VALIDATION_ERROR`, errors liệt kê từng rule fail.

### UC-4: New password trùng current password

- Sau khi BCrypt verify thành công, nếu `newPassword == currentPassword` → trả `400 Bad Request`, code = `NEW_PASSWORD_SAME_AS_OLD`.

### UC-5: User không tồn tại hoặc bị inactive

- `user is null` → `401 Unauthorized`, code = `TOKEN_INVALID` (token hợp lệ nhưng user bị xóa).
- `!user.IsActive` → `401 Unauthorized`, code = `ACCOUNT_INACTIVE`.

---

## Out of Scope

- Email/notification "mật khẩu đã đổi" (có thể thêm ở sprint sau — cần infrastructure email).
- Audit log (ai đổi, khi nào, IP) — chưa có audit infrastructure.
- Rate limiting đặc thù cho endpoint này — phụ thuộc feature rate limit chung (Open Tech Debt).
- Password history (không cho trùng N mật khẩu cũ gần nhất).
- Admin reset password cho user (Admin flow riêng).
- Forgot password / reset qua email OTP.

---

## Technical Approach (Clean Architecture)

### Domain Layer (Beacon.Domain)

- **Entity `User`**: thêm method domain `ChangePassword(string newPasswordHash)` trong `Entities/Identity/User.cs`:
  - Update `PasswordHash`
  - Update `UpdatedAtUtc` (nếu là `AuditableEntity`)
- **Không** thêm entity mới, không migration.
- `IUserRepository` **không cần** method mới — đã có `GetByIdAsync`, `GetActiveRefreshTokensByUserIdAsync`, `SaveChangesAsync`.

### Application Layer (Beacon.Application)

- **Command**: `Features/Identity/Commands/ChangePassword/`
  - `ChangePasswordCommand : IRequest<Result<object?>>` — wrap `ChangePasswordRequest`
  - `ChangePasswordCommandHandler`
- **DTO**: `Features/Identity/Dtos/ChangePasswordRequest.cs`
  - `string CurrentPassword`
  - `string NewPassword`
- **Validator**: `Features/Identity/Validators/Identity/ChangePasswordCommandValidator.cs` — target `ChangePasswordCommand`
  - Tái sử dụng **nguyên văn** password policy của `RegisterCommandValidator` cho `NewPassword`.
  - `CurrentPassword`: NotEmpty.
- **Handler logic**:
  1. Lấy `userId` từ `ICurrentUserService`.
  2. `userRepository.GetByIdAsync(userId)` — null → `TOKEN_INVALID` (401).
  3. Check `IsActive` — false → `ACCOUNT_INACTIVE` (401).
  4. `BCrypt.Verify(current, user.PasswordHash)` — fail → `INVALID_CURRENT_PASSWORD` (401).
  5. `current == new` → `NEW_PASSWORD_SAME_AS_OLD` (400).
  6. `user.ChangePassword(BCrypt.HashPassword(new))`.
  7. `activeTokens = GetActiveRefreshTokensByUserIdAsync(userId)`; `foreach t: t.Revoke()`.
  8. `SaveChangesAsync` (một lần — cùng transaction scope của EF).
  9. Return `Result.Success<object?>(null)`.

- **Mapper**: không cần (response không có `data`).

### Infrastructure Layer (Beacon.Infrashtructure)

- **Không thay đổi gì** — tất cả method repository đã có sẵn.

### API Layer (Beacon.Api)

- **Endpoint**: thêm vào `UsersController` (controller `Api/Controllers/Identity/UsersController.cs` hoặc nơi đang host `PATCH /api/v1/users/me`).
  - `PATCH /api/v1/users/me/password`
  - `[Authorize]`
  - Body: `ChangePasswordRequest`
  - Trả `HandleResult(...)` — 200 khi success, errors tự map.
- **XML doc**: bắt buộc theo template `.claude/rules/api-conventions/RULE.md § 8` — liệt kê đủ `null`, `VALIDATION_ERROR`, `INVALID_CURRENT_PASSWORD`, `NEW_PASSWORD_SAME_AS_OLD`, `ACCOUNT_INACTIVE`, `TOKEN_INVALID`.

### Shared Layer (Beacon.Shared)

- **Thêm error codes** vào `Beacon.Shared/Constants/ErrorCodes.cs` → `static class Identity`:
  ```csharp
  public const string INVALID_CURRENT_PASSWORD = "INVALID_CURRENT_PASSWORD";
  public const string NEW_PASSWORD_SAME_AS_OLD = "NEW_PASSWORD_SAME_AS_OLD";
  ```

---

## Code Style & Architecture

- Clean Architecture + CQRS: Command (không Query) vì là write operation.
- Validator target `ChangePasswordCommand` (không target `ChangePasswordRequest`) — để `ValidationBehavior` pipeline intercept được.
- Controller thin: chỉ mediator.Send + `HandleResult`.
- Handler return `Result<object?>` với `Success(null)` — nhất quán với pattern khi không có payload (kiểm tra nếu project đã có alias `Result` non-generic thì dùng, nếu không dùng `Result<object?>`).
- Không throw exception cho business error — dùng `Result.Failure` + `Error.Unauthorized/Validation`.
- Password hash dùng **BCrypt.Net** — nhất quán với `LoginCommandHandler`, `RegisterCommandHandler` hiện tại.

> ⚠️ **Note tech debt đã phát hiện**: `CLAUDE.md § Auth/Password` ghi "dùng `IPasswordHasher<T>` của ASP.NET Core Identity" nhưng code thực tế dùng **BCrypt.Net**. SPEC này theo code thực tế (BCrypt). Cần raise ADR để chốt hoặc migrate — **không giải quyết trong feature này**.

---

## Testing Strategy

### Unit Tests (`tests/Beacon.UnitTests/Identity/ChangePasswordCommandHandlerTests.cs`)

| Method | Scenario | Expected |
|---|---|---|
| `Handle_WithValidCredentials_ChangesPasswordAndRevokesTokens` | User + current đúng + new hợp lệ | Success, hash mới ≠ hash cũ, tất cả refresh token `RevokedAtUtc != null` |
| `Handle_WhenUserNotFound_ReturnsTokenInvalid` | Repo trả null | Failure với `TOKEN_INVALID` |
| `Handle_WhenUserInactive_ReturnsAccountInactive` | `user.IsActive = false` | Failure với `ACCOUNT_INACTIVE` |
| `Handle_WithWrongCurrentPassword_ReturnsInvalidCurrentPassword` | BCrypt verify fail | Failure với `INVALID_CURRENT_PASSWORD`, không save |
| `Handle_WhenNewSameAsCurrent_ReturnsSameAsOld` | current == new | Failure với `NEW_PASSWORD_SAME_AS_OLD` |

Validator tests (`ChangePasswordCommandValidatorTests.cs`): test 6 rule policy (empty, min 8, max 100, hoa, thường, số, đặc biệt).

### Integration Tests (`tests/Beacon.IntergrationTests/Identity/ChangePasswordTests.cs`)

| Test | Kỳ vọng |
|---|---|
| `PATCH /users/me/password` không token → 401 | |
| Happy path với seed user → 200, login bằng new password thành công, old refresh token fail `/refresh` | |
| Current password sai → 401 + `INVALID_CURRENT_PASSWORD` | |
| New password < 8 ký tự → 400 + `VALIDATION_ERROR` | |
| New trùng current → 400 + `NEW_PASSWORD_SAME_AS_OLD` | |

---

## API Contract

**Request:**
```http
PATCH /api/v1/users/me/password
Authorization: Bearer <access-token>
Content-Type: application/json

{ "currentPassword": "OldPass123!", "newPassword": "NewPass456@" }
```

**Response 200 (success):**
```json
{ "success": true, "message": "Đổi mật khẩu thành công. Vui lòng đăng nhập lại.", "code": null, "data": null, "errors": null }
```

**Response 401 (current sai):**
```json
{ "success": false, "message": "Mật khẩu hiện tại không đúng.", "code": "INVALID_CURRENT_PASSWORD", "data": null, "errors": ["Mật khẩu hiện tại không đúng."] }
```

**Response 400 (new trùng current):**
```json
{ "success": false, "message": "Mật khẩu mới phải khác mật khẩu hiện tại.", "code": "NEW_PASSWORD_SAME_AS_OLD", "data": null, "errors": ["..."] }
```

---

## Boundaries

### Always Do
- Giữ Controller thin (mediator.Send + HandleResult).
- Validator target Command, KHÔNG target Request DTO.
- Message lỗi localized tiếng Việt (nhất quán với `RegisterCommandValidator`).
- Error codes mới thêm vào `ErrorCodes.cs` **trước** khi dùng trong handler.
- Revoke refresh token **cùng** `SaveChangesAsync` với update password (một transaction EF).

### Ask First
- Có thêm email notification "mật khẩu đã đổi" không? (hiện **Out of Scope**).
- Có migrate BCrypt → ASP.NET Core Identity `IPasswordHasher` không? (tech debt riêng, cần ADR).
- Có throttle số lần đổi password liên tiếp không? (rate limit — Open Tech Debt).

### Never Do
- KHÔNG throw exception cho business error (current sai, policy fail) — dùng `Result.Failure`.
- KHÔNG log `currentPassword`, `newPassword`, hash — chỉ log `userId` + error code.
- KHÔNG revoke **access token** hiện tại (JWT stateless, không revoke được; client tự xử lý).
- KHÔNG tạo endpoint `PUT /users/me/password` — dùng PATCH vì đây là partial update (chỉ password).
- KHÔNG reference Infrastructure hoặc Api từ Application/Domain.
- KHÔNG đụng `DbContext` trực tiếp trong handler — qua `IUserRepository`.

---

## Files sẽ tạo / sửa

**Tạo mới:**
- `src/Beacon.Application/Features/Identity/Dtos/ChangePasswordRequest.cs`
- `src/Beacon.Application/Features/Identity/Commands/ChangePassword/ChangePasswordCommand.cs`
- `src/Beacon.Application/Features/Identity/Commands/ChangePassword/ChangePasswordCommandHandler.cs`
- `src/Beacon.Application/Features/Identity/Validators/Identity/ChangePasswordCommandValidator.cs`
- `tests/Beacon.UnitTests/Identity/ChangePasswordCommandHandlerTests.cs`
- `tests/Beacon.UnitTests/Identity/ChangePasswordCommandValidatorTests.cs`
- `tests/Beacon.IntergrationTests/Identity/ChangePasswordTests.cs`

**Sửa:**
- `src/Beacon.Domain/Entities/Identity/User.cs` — thêm method `ChangePassword(string newHash)`
- `src/Beacon.Shared/Constants/ErrorCodes.cs` — thêm 2 const `INVALID_CURRENT_PASSWORD`, `NEW_PASSWORD_SAME_AS_OLD`
- `src/Beacon.Api/Controllers/Identity/UsersController.cs` (hoặc controller đang host `/users/me`) — thêm `PATCH /me/password` action

**Không đụng:**
- Migration (không schema change)
- Infrastructure repository implement (không method mới)
- DI extensions (handler + validator auto-scan)
