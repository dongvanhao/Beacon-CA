# Error Handling — Beacon

> Rule này chỉ nói **quyết định cụ thể của Beacon**. ErrorType→HTTP mapping xem `api-conventions/RULE.md`.

---

## Nguyên tắc cốt lõi

**Beacon có 2 track xử lý lỗi — chọn đúng track:**

| Loại lỗi | Track | Cơ chế |
|---|---|---|
| Lỗi nghiệp vụ **dự kiến** | `Result.Failure(...)` | Handler trả về, BaseController tự map HTTP |
| Lỗi hệ thống **không phục hồi** | `throw` exception | `ExceptionHandlingMiddleware` bắt, trả 500 |

---

## Track 1 — Result Pattern (mặc định, dùng >90% trường hợp)

```csharp
// Handler
var user = await _repo.GetByIdAsync(id);
if (user is null)
    return Result.Failure<UserDto>(Error.NotFound("USER_NOT_FOUND", "Không tìm thấy user"));

return Result.Success(mapper.ToDto(user));

// Controller — KHÔNG cần try/catch
public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(new GetUserQuery(id), ct));
```

**Tạo Error đúng factory method:**

```csharp
Error.Validation("VALIDATION_ERROR", "...")   // → 400
Error.NotFound("USER_NOT_FOUND", "...")        // → 404
Error.Conflict("EMAIL_ALREADY_EXISTS", "...")  // → 409
Error.Unauthorized("TOKEN_INVALID", "...")     // → 401
Error.Forbidden("MEDIA_FORBIDDEN", "...")      // → 403
Error.Failure("UPLOAD_FAILED", "...")          // → 400
```

Error code phải tồn tại trong `Beacon.Shared/Constants/ErrorCodes.cs`.

---

## Track 2 — Exceptions (chỉ cho lỗi không phục hồi)

Dùng các exception class trong `Beacon.Application/Common/Exceptions/`:

```csharp
// Chỉ throw khi không thể tiếp tục bình thường
throw new NotFoundException("User", id);        // → 404
throw new ConflictException("Email đã tồn tại"); // → 409
throw new UnauthorizedException("...");          // → 401
throw new ForbiddenException("...");             // → 403
throw new ValidationException(errors);           // → 400
```

`ExceptionHandlingMiddleware` bắt tất cả — **không cần try/catch trong controller hay handler**.

**Khi nào dùng exception thay vì Result:**
- Lỗi xảy ra sâu trong infrastructure (MinIO, DB connection) không thể trả về qua call stack
- Third-party library ném exception ra ngoài tầm kiểm soát

---

## Những gì KHÔNG được làm

| ❌ | ✅ |
|---|---|
| `throw` exception cho lỗi nghiệp vụ dự kiến | `return Result.Failure(Error.NotFound(...))` |
| `try/catch` trong handler để nuốt lỗi | Để `ExceptionHandlingMiddleware` xử lý |
| `return null` khi không tìm thấy entity | `return Result.Failure(Error.NotFound(...))` |
| Log password, token, email trong error message | Chỉ log mã lỗi + user ID |
| Tạo error code mới tùy tiện | Thêm vào `ErrorCodes.cs` trước khi dùng |

---

## 3 điểm AI hay nhầm trong Beacon

1. **`FluentValidation` tự ném `ValidationException`** qua `ValidationBehavior` — không cần tự handle validation error trong handler.
2. **Repository trả `nullable`**, handler mới quyết định đó có phải lỗi không — không throw trong repository.
3. **Controller không bao giờ có `try/catch`** — mọi thứ đã được bắt bởi middleware hoặc trả về qua Result.
