# Ví dụ output review tốt — Beacon Code Reviewer

> Scenario: Review PR #14 — Feature "Tạo Checkin mới" (Checkins module)
> Scope: `CreateCheckinCommand` + `CreateCheckinHandler` + `CheckinController` + `CreateCheckinCommandValidator`

---

## Review Summary

**Overall**: REQUEST CHANGES

### Critical Issues

* **Critical:** `[CreateCheckinHandler.cs:38]** Handler inject `AppDbContext` trực tiếp thay vì dùng Repository interface.
  ```csharp
  // ❌ Hiện tại
  private readonly AppDbContext _db;
  var checkin = new Checkin { ... };
  _db.Checkins.Add(checkin);
  await _db.SaveChangesAsync();

  // ✅ Sửa thành
  private readonly ICheckinRepository _checkinRepo;
  await _checkinRepo.AddAsync(checkin, cancellationToken);
  ```
  Rule bị vi phạm: `no-direct-db` — handlers MUST go through repository interface.

* **Critical:** `[CreateCheckinCommandValidator.cs:1]` Validator extend `AbstractValidator<CreateCheckinDto>` thay vì `AbstractValidator<CreateCheckinCommand>`.
  `ValidationBehavior` pipeline chỉ intercept **Command** — validator này không bao giờ chạy.
  ```csharp
  // ❌ Hiện tại
  public class CreateCheckinValidator : AbstractValidator<CreateCheckinDto>

  // ✅ Sửa thành
  public class CreateCheckinCommandValidator : AbstractValidator<CreateCheckinCommand>
  ```

---

### Important

* `[CheckinController.cs:29]` POST endpoint trả `HandleResult()` thay vì `CreatedResult()`.
  POST tạo mới resource phải trả 201 Created theo convention.
  ```csharp
  // ❌
  return HandleResult(await _mediator.Send(cmd));

  // ✅
  return CreatedResult("GetCheckin", await _mediator.Send(cmd));
  ```

* `[CreateCheckinHandler.cs:52]` `CancellationToken` không được pass xuống `_locationRepo.GetByIdAsync()`.
  ```csharp
  // ❌
  var location = await _locationRepo.GetByIdAsync(command.LocationId);

  // ✅
  var location = await _locationRepo.GetByIdAsync(command.LocationId, cancellationToken);
  ```

* `[CreateCheckinHandler.cs:60]` Business logic (tính khoảng cách) nằm trong handler, nên chuyển vào Domain method.
  ```csharp
  // ❌ Trong handler
  var distance = Math.Sqrt(Math.Pow(lat2 - lat1, 2) + Math.Pow(lng2 - lng1, 2));
  if (distance > maxRadius) return Result.Failure<CheckinDto>(...);

  // ✅ Chuyển vào Checkin.IsWithinRadius(location, maxRadius) — Domain method
  if (!location.IsWithinRadius(command.Latitude, command.Longitude))
      return Result.Failure<CheckinDto>(Error.Validation(...));
  ```

---

### Suggestions

* `[CheckinMapper.cs:15]` Optional: `ToCheckinDto()` trả về `DateTime` local thay vì UTC.
  Nên chuẩn hóa sang `DateTimeOffset` hoặc document rõ convention timezone.

* `[CreateCheckinCommandValidator.cs:18]` Nit: message lỗi dùng tiếng Anh — convention hiện tại của dự án là **tiếng Việt**.
  ```csharp
  // ❌
  .WithMessage("Latitude must be between -90 and 90")

  // ✅
  .WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90.")
  ```

* `[CheckinController.cs:12]` Nit: `[Route("api/[controller]")]` — nên dùng route cố định `[Route("api/v1/checkins")]` để tránh thay đổi tên class ảnh hưởng đến route.

* `[CreateCheckinHandler.cs:1]` FYI: Handler đang ở 85 dòng — vẫn trong ngưỡng chấp nhận (~70 dòng), nhưng nếu thêm validation logic nên tách ra Domain method để giữ handler gọn.

* `[CheckinController.cs]` FYI: Rate limiting chưa được apply (`"api"` policy). Hiện tại feature chưa implement ở dự án — track as tech debt, không block merge.

---

### Positives

* ✅ `CheckinMapper` đúng convention: `sealed class`, Singleton, method `ToCheckinDto()` — thuần property assignment, không có I/O hay business logic.
* ✅ `CreateCheckinCommand` implement đúng `IRequest<Result<CheckinDto>>`.
* ✅ Controller kế thừa `BaseController`, inject `IMediator` (không inject Handler trực tiếp).
* ✅ `[Authorize]` được đặt đúng trên controller — checkin là private resource.
* ✅ Không có `.Result` / `.Wait()` — toàn bộ async/await đúng.
* ✅ `null` từ repository được convert đúng thành `Result.Failure.NotFound` với `ErrorCodes`.

---

## Score

**Điểm: 5/10**

| Axis | Score | Lý do |
|------|-------|-------|
| Correctness | 4/10 | Validator không chạy do target sai; CancellationToken bị drop |
| Readability | 7/10 | Tên file và class nhất quán; handler hơi dài |
| Architecture | 4/10 | Direct DbContext usage — vi phạm `no-direct-db` |
| Security | 7/10 | Auth đúng; rate limit chưa có nhưng feature chưa build |
| Performance | 8/10 | Không có N+1; async đúng |

**Ưu tiên:**
1. (Critical) Đổi `AppDbContext` → `ICheckinRepository` trong handler
2. (Critical) Đổi validator target từ `CreateCheckinDto` → `CreateCheckinCommand`
3. (Important) Đổi `HandleResult()` → `CreatedResult()` cho POST endpoint
