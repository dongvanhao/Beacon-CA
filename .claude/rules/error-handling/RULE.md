# Error Handling — Beacon

> ErrorType→HTTP mapping ở `api-conventions/RULE.md`.

---

## 2 Track — chọn đúng

| Loại lỗi | Track | Cơ chế |
|---|---|---|
| Lỗi nghiệp vụ **dự kiến** | `Result.Failure(...)` | Handler return, BaseController tự map |
| Lỗi hệ thống **không phục hồi** | `throw` exception | `ExceptionHandlingMiddleware` bắt → 500 |

---

## Track 1 — Result (>90% trường hợp)

```csharp
var user = await _repo.GetByIdAsync(id);
if (user is null)
    return Result.Failure<UserDto>(Error.NotFound(ErrorCodes.USER_NOT_FOUND, "Không tìm thấy user"));
return Result.Success(_mapper.ToDto(user));
```

**Controller KHÔNG cần try/catch.**

Factory methods:

```csharp
Error.Validation("VALIDATION_ERROR", "...")   // → 400
Error.NotFound("USER_NOT_FOUND", "...")       // → 404
Error.Conflict("EMAIL_ALREADY_EXISTS", "...") // → 409
Error.Unauthorized("TOKEN_INVALID", "...")    // → 401
Error.Forbidden("MEDIA_FORBIDDEN", "...")     // → 403
Error.Failure("UPLOAD_FAILED", "...")         // → 400
```

Error code phải tồn tại trong `Beacon.Shared/Constants/ErrorCodes.cs` trước khi dùng.

---

## Track 2 — Exception (lỗi không phục hồi)

Dùng exception class ở `Beacon.Application/Common/Exceptions/`:

```csharp
throw new NotFoundException("User", id);        // → 404
throw new ConflictException("Email đã tồn tại"); // → 409
throw new UnauthorizedException("...");          // → 401
throw new ForbiddenException("...");             // → 403
throw new ValidationException(errors);           // → 400
```

**Khi nào throw:** lỗi sâu trong infrastructure (MinIO, DB connection), third-party ném exception. **KHÔNG** throw cho lỗi business dự kiến.

---

## 3 điểm AI hay nhầm

1. `ValidationBehavior` (MediatR pipeline) **tự** ném `ValidationException` từ FluentValidation — handler không cần tự validate.
2. Repository trả `nullable`, **handler** quyết định null = lỗi gì — không throw trong repository.
3. Controller **không bao giờ** có `try/catch` — middleware + Result đã lo.

---

## KHÔNG làm

| ❌ | ✅ |
|---|---|
| `throw` cho business error dự kiến | `Result.Failure(Error.NotFound(...))` |
| `try/catch` trong handler để nuốt lỗi | Để middleware xử lý |
| `return null` khi entity không tìm thấy | `Result.Failure(Error.NotFound(...))` |
| Log password/token/email trong error | Chỉ log error code + userId |
| Tạo error code mới tùy tiện | Thêm vào `ErrorCodes.cs` trước |

---

## Logging (Serilog)

Logging chạy qua **Serilog** (structured). Spec/plan: `docs/specs|plans/serilog-request-response-logging*`.

| Việc | Quy tắc |
|---|---|
| **Request summary** | Tự động qua `UseSerilogRequestLogging()` — **không** tự viết middleware log request song song (gây double-log) |
| **Exception chi tiết** | `ExceptionHandlingMiddleware` log `LogError(ex, ...)` kèm stack; request logging chỉ log dòng summary (method/path/status/elapsed) |
| **Identity trong log** | Chỉ `UserId` (GUID, từ `ClaimTypes.NameIdentifier`) — **không** username/email/phone |
| **PII / Secrets** | Không log `Authorization`/`Cookie` header, token, password, body. Query param nhạy cảm → mask `***` |
| **EF Core** | Giữ `EnableSensitiveDataLogging=false`; level `Warning` ở Production (tránh rò PII trong SQL log) |
| **Sink** | Console-only (stdout). File/Seq/Loki/OTel → cần ADR riêng |
| **Correlation** | Dùng `TraceId` từ `Activity` (OTel-ready), **không** `HttpContext.TraceIdentifier` |
