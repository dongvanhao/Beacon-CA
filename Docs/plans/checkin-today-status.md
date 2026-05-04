# Plan: GET /api/v1/checkins/today-status

**Module**: Checkins  
**Type**: Query (read-only)  
**Phạm vi**: 3 slices — không có schema change  

---

## Context & Business Rules

### Mục tiêu
Frontend gọi endpoint này để hiển thị trạng thái checkin ngày hôm đó và đồng hồ đếm ngược đến deadline.

### Business Rules
- **BR-1**: Deadline lấy từ `SafetySetting.DailyDeadlineLocalTime` của user. Nếu chưa setting → default `23:59` UTC.
- **BR-2**: Nếu chưa có `DailySafetyRecord` hôm nay → vẫn tính deadline từ `SafetySetting` (không tạo record mới).
- **BR-3**: `remainingSeconds = (DeadlineAtUtc − DateTime.UtcNow).TotalSeconds` — dương nếu còn thời gian, âm nếu đã trễ.
- **BR-4**: Status mapping:

  | Domain `SafetyStatus`              | API `status`  | `remainingSeconds` |
  |------------------------------------|---------------|-------------------|
  | `CheckedIn`, `Resolved`            | `CheckedIn`   | `null`            |
  | Còn lại + remainingSeconds >= 0    | `Pending`     | >= 0              |
  | Còn lại + remainingSeconds < 0     | `Overdue`     | < 0               |
  | Không có record (chưa checkin)     | `Pending` hoặc `Overdue` theo remainingSeconds | computed |

### Response Shape (đã chốt)
```json
{
  "hasCheckedIn": false,
  "status": "Pending | CheckedIn | Overdue",
  "deadlineAtUtc": "2025-04-27T23:59:00Z",
  "remainingSeconds": 3661,
  "checkedInAtUtc": null
}
```

### Không cần
- Schema change / EF migration (chỉ đọc dữ liệu đã có)
- Repository interface mới (`GetByUserIdAndDateAsync` và `GetByUserIdAsync` đã tồn tại)
- Domain entity mới

---

## Dependencies đã có

| Thành phần | File | Status |
|---|---|---|
| `DailySafetyRecord` entity | `Domain/Entities/Safety/DailySafetyRecord.cs` | ✅ có |
| `SafetySetting` entity | `Domain/Entities/Settings/SafetySetting.cs` | ✅ có |
| `IDailySafetyRecordRepository.GetByUserIdAndDateAsync` | `Domain/IRepository/Safety/` | ✅ có |
| `ISafetySettingRepository.GetByUserIdAsync` | `Domain/IRepository/Settings/` | ✅ có |
| `CheckinsController` tại `api/v1/checkins` | `Api/Controllers/Checkins/` | ✅ có |
| `ICurrentUserService` | `Application/Common/Interfaces/IService/` | ✅ có |

---

## Phase 1: Query Handler (TDD)

### Slice 1.1: DTO + Mapper

**Type**: DTO + Mapper (foundation cho handler)  
**Dependencies**: Không có  

#### Files cần tạo

**1. Response DTO**  
`src/Beacon.Application/Features/Checkins/Dtos/TodayCheckinStatusDto.cs`

```csharp
public record TodayCheckinStatusDto(
    bool HasCheckedIn,
    string Status,           // "Pending" | "CheckedIn" | "Overdue"
    DateTime DeadlineAtUtc,
    long? RemainingSeconds,  // null khi CheckedIn
    DateTime? CheckedInAtUtc
);
```

**2. Mapper**  
`src/Beacon.Application/Mappings/Checkins/CheckinStatusMapper.cs`

```csharp
public sealed class CheckinStatusMapper
{
    public TodayCheckinStatusDto ToStatusDto(
        DateTime deadlineAtUtc,
        DateTime? checkedInAtUtc,
        bool hasCheckedIn)
    {
        if (hasCheckedIn)
            return new(true, "CheckedIn", deadlineAtUtc, null, checkedInAtUtc);

        var remaining = (long)(deadlineAtUtc - DateTime.UtcNow).TotalSeconds;
        var status = remaining >= 0 ? "Pending" : "Overdue";
        return new(false, status, deadlineAtUtc, remaining, null);
    }
}
```

**3. Đăng ký Singleton**  
`src/Beacon.Application/DependencyInjection/ApplicationServiceExtensions.cs`  
→ Thêm `services.AddSingleton<CheckinStatusMapper>()`

#### Acceptance Criteria
- [ ] `dotnet build` — 0 error

---

### Slice 1.2: Query + Handler (TDD)

**Type**: Query  
**Dependencies**: Slice 1.1  

#### Bước 1 — Viết Test (RED) trước

**File**: `tests/Beacon.UnitTests/Checkins/GetTodayCheckinStatusQueryHandlerTests.cs`

Test cases (skeleton — FAIL vì handler chưa tồn tại):

```
Handle_ShouldReturnPending_WhenNoRecordAndBeforeDeadline
  → remainingSeconds > 0, status = "Pending", hasCheckedIn = false

Handle_ShouldReturnPending_WhenRecordExistsAndBeforeDeadline
  → remainingSeconds > 0, status = "Pending"

Handle_ShouldReturnCheckedIn_WhenAlreadyCheckedIn
  → remainingSeconds = null, hasCheckedIn = true, checkedInAtUtc populated

Handle_ShouldReturnOverdue_WhenNoCheckinAndPastDeadline
  → remainingSeconds < 0, status = "Overdue"

Handle_ShouldUseDefaultDeadline_WhenNoSafetySetting
  → deadline = today + 23:59, status computed từ đó
```

#### Bước 2 — Query

**File**: `src/Beacon.Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQuery.cs`

```csharp
public record GetTodayCheckinStatusQuery(Guid UserId)
    : IRequest<Result<TodayCheckinStatusDto>>;
```

#### Bước 3 — Handler

**File**: `src/Beacon.Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQueryHandler.cs`

Logic:
1. `today = DateOnly.FromDateTime(DateTime.UtcNow)`
2. `record = await dailySafetyRecordRepo.GetByUserIdAndDateAsync(userId, today, ct)`
3. Tính `deadlineAtUtc`:
   - Nếu `record != null` → dùng `record.DeadlineAtUtc`
   - Nếu `record == null` → query `SafetySetting`, lấy `DailyDeadlineLocalTime` (default `23:59`), tính `today.ToDateTime(deadlineTime, DateTimeKind.Utc)`
4. `hasCheckedIn = record?.Status == SafetyStatus.CheckedIn || record?.Status == SafetyStatus.Resolved`
5. Return `Result.Success(mapper.ToStatusDto(deadlineAtUtc, record?.CheckedInAtUtc, hasCheckedIn))`

**Inject**: `IDailySafetyRecordRepository`, `ISafetySettingRepository`, `CheckinStatusMapper`

#### Acceptance Criteria
- [ ] Unit tests GREEN (tất cả 5 cases)
- [ ] `dotnet build` — 0 error, 0 warning

---

## ✅ Checkpoint: Phase 1 Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN

---

## Phase 2: Controller + Integration Tests

### Slice 2.1: Controller Endpoint

**Type**: API endpoint  
**Dependencies**: Slice 1.2  

**File**: `src/Beacon.Api/Controllers/Checkins/CheckinsController.cs`  
→ Thêm action method vào controller hiện có (không tạo file mới)

```csharp
[HttpGet("today-status")]
public async Task<IActionResult> GetTodayStatus(CancellationToken ct)
    => HandleResult(await mediator.Send(
        new GetTodayCheckinStatusQuery(currentUser.UserId), ct));
```

XML doc bắt buộc:
```
/// <summary>Lấy trạng thái check-in và thời gian đếm ngược đến deadline hôm nay.</summary>
/// Codes: null (thành công), VALIDATION_ERROR
/// data: { hasCheckedIn, status, deadlineAtUtc, remainingSeconds, checkedInAtUtc }
```

**Route kết quả**: `GET /api/v1/checkins/today-status`  
**Auth**: `[Authorize]` (kế thừa từ controller)

#### Acceptance Criteria
- [ ] Swagger hiển thị đúng route và auth requirement
- [ ] `dotnet build` — 0 error

---

### Slice 2.2: Integration Tests

**File**: `tests/Beacon.IntergrationTests/Checkins/CheckinsControllerTests.cs`  
(Tạo mới nếu chưa có, thêm vào nếu đã có)

Test cases bắt buộc:

| Case | HTTP | Verify |
|---|---|---|
| Happy path — Pending (chưa checkin, còn thời gian) | 200 | `status="Pending"`, `remainingSeconds >= 0`, `hasCheckedIn=false` |
| Happy path — CheckedIn (đã checkin hôm nay) | 200 | `status="CheckedIn"`, `remainingSeconds=null`, `checkedInAtUtc` populated |
| Happy path — Overdue (chưa checkin, qua deadline) | 200 | `status="Overdue"`, `remainingSeconds < 0` |
| Không có token | 401 | `success=false` |

#### Acceptance Criteria
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] Response shape đúng `ApiResponse<TodayCheckinStatusDto>`

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN
- [ ] `GET /api/v1/checkins/today-status` hiển thị trên Swagger với auth
- [ ] Response luôn là `ApiResponse<TodayCheckinStatusDto>`
- [ ] Không có `DbContext` inject trực tiếp vào handler
- [ ] Không có business logic trong controller

---

## File Change Summary

| Action | File |
|---|---|
| **Tạo mới** | `Application/Features/Checkins/Dtos/TodayCheckinStatusDto.cs` |
| **Tạo mới** | `Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQuery.cs` |
| **Tạo mới** | `Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQueryHandler.cs` |
| **Tạo mới** | `Application/Mappings/Checkins/CheckinStatusMapper.cs` |
| **Tạo mới** | `tests/Beacon.UnitTests/Checkins/GetTodayCheckinStatusQueryHandlerTests.cs` |
| **Tạo mới** | `tests/Beacon.IntergrationTests/Checkins/CheckinsControllerTests.cs` |
| **Sửa** | `Application/DependencyInjection/ApplicationServiceExtensions.cs` — đăng ký `CheckinStatusMapper` |
| **Sửa** | `Api/Controllers/Checkins/CheckinsController.cs` — thêm `GetTodayStatus` action |
