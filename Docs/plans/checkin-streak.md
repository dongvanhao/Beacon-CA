# Plan: Add Streak Field — `GET /api/v1/checkins/today-status`

**Module**: Checkins  
**Spec**: `/spec` — thêm trường `streak` (số ngày check-in liên tục) vào response today-status  
**Phạm vi**: 3 slices — không có schema change, không cần migration

---

## Tổng quan thay đổi

```
ICheckinRepository          ← thêm GetStreakAsync()
CheckinRepository           ← implement GetStreakAsync()
TodayCheckinStatusDto       ← thêm Streak field
CheckinStatusMapper         ← thêm tham số streak
GetTodayCheckinStatusQueryHandler ← gọi GetStreakAsync, truyền vào mapper
```

**Không cần:** entity mới, migration, controller mới, validator mới, mapper mới.

---

## Business Rules

| Rule | Nơi thực hiện |
|---|---|
| Hôm nay đã check-in → streak tính từ hôm nay | `CheckinRepository.GetStreakAsync` |
| Hôm nay chưa check-in, hôm qua có → streak = số ngày tính từ hôm qua ngược về | `CheckinRepository.GetStreakAsync` |
| Hôm nay chưa check-in, hôm qua không có → streak = 0 | `CheckinRepository.GetStreakAsync` |
| Chưa từng check-in → streak = 0 | `CheckinRepository.GetStreakAsync` |
| Mỗi ngày ≥ 1 bản ghi Checkin (bất kể Type) là đủ | `CheckinRepository.GetStreakAsync` |
| Query giới hạn 366 ngày gần nhất (tránh full scan) | `CheckinRepository.GetStreakAsync` |

---

## Slice 1 — Repository: `GetStreakAsync`

**Type**: Infrastructure (read-only query)  
**Files thay đổi**:
- `src/Beacon.Domain/IRepository/Checkins/ICheckinRepository.cs`
- `src/Beacon.Infrashtructure/Repository/Checkins/CheckinRepository.cs`

### Bước 1.1 — Thêm method vào Interface

File: `src/Beacon.Domain/IRepository/Checkins/ICheckinRepository.cs`

```csharp
Task<int> GetStreakAsync(Guid userId, DateOnly today, CancellationToken ct = default);
```

Interface sau khi sửa:

```csharp
public interface ICheckinRepository
{
    Task AddAsync(Checkin checkin, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<int> GetStreakAsync(Guid userId, DateOnly today, CancellationToken ct = default);
}
```

### Bước 1.2 — Implement trong Repository

File: `src/Beacon.Infrashtructure/Repository/Checkins/CheckinRepository.cs`

**Thuật toán:**
1. Lấy tất cả `CheckinDate` distinct của user trong 366 ngày ≤ today, sort DESC.
2. Nếu danh sách rỗng → trả `0`.
3. Nếu phần tử đầu tiên là `today` → bắt đầu đếm từ today.
4. Nếu phần tử đầu tiên là `today.AddDays(-1)` → bắt đầu đếm từ hôm qua.
5. Nếu không thỏa mãn → trả `0`.
6. Duyệt danh sách: mỗi bước kỳ vọng ngày tiếp theo = ngày hiện tại - 1; nếu không khớp → dừng.

```csharp
public async Task<int> GetStreakAsync(Guid userId, DateOnly today, CancellationToken ct = default)
{
    var cutoff = today.AddDays(-365);

    var checkinDates = await db.Checkins
        .Where(c => c.UserId == userId
                 && c.CheckinDate >= cutoff
                 && c.CheckinDate <= today)
        .Select(c => c.CheckinDate)
        .Distinct()
        .OrderByDescending(d => d)
        .ToListAsync(ct);

    if (checkinDates.Count == 0)
        return 0;

    // Chuỗi phải bắt đầu từ hôm nay hoặc hôm qua
    var startDate = checkinDates[0];
    if (startDate < today.AddDays(-1))
        return 0;

    int streak = 0;
    var expected = startDate;

    foreach (var date in checkinDates)
    {
        if (date != expected) break;
        streak++;
        expected = expected.AddDays(-1);
    }

    return streak;
}
```

---

## Slice 2 — Application: Wire Streak vào Response

**Type**: Application layer (DTO + Mapper + Handler)  
**Dependencies**: Slice 1 phải complete  
**Files thay đổi**:
- `src/Beacon.Application/Features/Checkins/Dtos/TodayCheckinStatusDto.cs`
- `src/Beacon.Application/Mappings/Checkins/CheckinStatusMapper.cs`
- `src/Beacon.Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQueryHandler.cs`
- `src/Beacon.Api/Controllers/Checkins/CheckinsController.cs` (chỉ cập nhật XML doc)

### Bước 2.1 — Thêm `Streak` vào DTO

File: `src/Beacon.Application/Features/Checkins/Dtos/TodayCheckinStatusDto.cs`

```csharp
public record TodayCheckinStatusDto(
    bool HasCheckedIn,
    string Status,
    DateTime DeadlineAtUtc,
    long? RemainingSeconds,
    DateTime? CheckedInAtUtc,
    bool IsMonitoringEnabled,
    bool IsAutoAlertEnabled,
    int Streak              // ← NEW: số ngày check-in liên tục
);
```

### Bước 2.2 — Cập nhật `CheckinStatusMapper`

File: `src/Beacon.Application/Mappings/Checkins/CheckinStatusMapper.cs`

Thêm tham số `int streak` vào `ToStatusDto()` và truyền xuống tất cả constructor call:

```csharp
public sealed class CheckinStatusMapper
{
    public TodayCheckinStatusDto ToStatusDto(
        DateTime deadlineAtUtc,
        DateTime? checkedInAtUtc,
        bool hasCheckedIn,
        bool isMonitoringEnabled,
        bool isAutoAlertEnabled,
        int streak)                 // ← NEW
    {
        if (hasCheckedIn)
            return new(true, CheckinDailyStatus.CheckedIn, deadlineAtUtc, null, checkedInAtUtc,
                isMonitoringEnabled, isAutoAlertEnabled, streak);

        if (!isMonitoringEnabled)
            return new(false, CheckinDailyStatus.Pending, deadlineAtUtc, null, null,
                isMonitoringEnabled, isAutoAlertEnabled, streak);

        var remainingSeconds = (long)(deadlineAtUtc - DateTime.UtcNow).TotalSeconds;
        var status = remainingSeconds >= 0 ? CheckinDailyStatus.Pending : CheckinDailyStatus.Overdue;
        return new(false, status, deadlineAtUtc, remainingSeconds, null,
            isMonitoringEnabled, isAutoAlertEnabled, streak);
    }
}
```

### Bước 2.3 — Cập nhật Handler

File: `src/Beacon.Application/Features/Checkins/Queries/GetTodayCheckinStatus/GetTodayCheckinStatusQueryHandler.cs`

Inject `ICheckinRepository`, gọi `GetStreakAsync`, truyền streak vào mapper:

```csharp
public class GetTodayCheckinStatusQueryHandler(
    IDailySafetyRecordRepository dailySafetyRecordRepo,
    ISafetySettingRepository safetySettingRepo,
    ICheckinRepository checkinRepo,              // ← NEW inject
    CheckinStatusMapper mapper)
    : IRequestHandler<GetTodayCheckinStatusQuery, Result<TodayCheckinStatusDto>>
{
    public async Task<Result<TodayCheckinStatusDto>> Handle(
        GetTodayCheckinStatusQuery query, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var record  = await dailySafetyRecordRepo.GetByUserIdAndDateAsync(query.UserId, today, ct);
        var setting = await safetySettingRepo.GetByUserIdAsync(query.UserId, ct);
        var streak  = await checkinRepo.GetStreakAsync(query.UserId, today, ct);  // ← NEW

        var deadlineAtUtc = record is not null
            ? record.DeadlineAtUtc
            : today.ToDateTime(setting?.DailyDeadlineLocalTime ?? new TimeOnly(23, 59), DateTimeKind.Utc);

        var isMonitoringEnabled = setting?.IsMonitoringEnabled ?? true;
        var isAutoAlertEnabled  = setting?.IsAutoAlertEnabled  ?? true;
        var hasCheckedIn        = record?.Status is SafetyStatus.CheckedIn or SafetyStatus.Resolved;

        return Result<TodayCheckinStatusDto>.Success(
            mapper.ToStatusDto(deadlineAtUtc, record?.CheckedInAtUtc, hasCheckedIn,
                isMonitoringEnabled, isAutoAlertEnabled, streak));  // ← NEW pass streak
    }
}
```

### Bước 2.4 — Cập nhật XML doc trên Controller endpoint

File: `src/Beacon.Api/Controllers/Checkins/CheckinsController.cs`

Thêm `streak` vào mô tả cấu trúc `data`:

```xml
/// Cấu trúc <c>data</c> khi thành công:
/// <code>
/// {
///   "hasCheckedIn": bool,
///   "status": "Pending | CheckedIn | Overdue",
///   "deadlineAtUtc": "datetime",
///   "remainingSeconds": long | null,
///   "checkedInAtUtc": "datetime | null",
///   "isMonitoringEnabled": bool,
///   "isAutoAlertEnabled": bool,
///   "streak": int   ← số ngày check-in liên tục (0 nếu chưa có)
/// }
/// </code>
```

---

## ✅ Checkpoint: Implementation Complete

Trước khi chuyển sang test, verify:

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] Không còn build error do signature `ToStatusDto` thay đổi

---

## Slice 3 — Tests

**Type**: Unit + Integration  
**Dependencies**: Slice 1 + Slice 2

### Bước 3.1 — Unit Test: Handler

File: `tests/Beacon.UnitTests/Checkins/GetTodayCheckinStatusQueryHandlerTests.cs`

Test cases bắt buộc:

| Method | Scenario | Expected |
|---|---|---|
| `Handle_ReturnsStreak_WhenUserHasConsecutiveCheckins` | Mock repo trả streak = 5 | `result.Value.Streak == 5` |
| `Handle_ReturnsZeroStreak_WhenUserHasNeverCheckedIn` | Mock repo trả streak = 0 | `result.Value.Streak == 0` |
| `Handle_ReturnsStreak_WhenCheckedInTodayOnly` | Mock repo trả streak = 1, hasCheckedIn = true | `result.Value.Streak == 1`, `Status == "CheckedIn"` |

```csharp
public class GetTodayCheckinStatusQueryHandlerTests
{
    private readonly Mock<IDailySafetyRecordRepository> _safetyRecordRepo = new();
    private readonly Mock<ISafetySettingRepository> _safetySettingRepo = new();
    private readonly Mock<ICheckinRepository> _checkinRepo = new();
    private readonly CheckinStatusMapper _mapper = new();

    private GetTodayCheckinStatusQueryHandler CreateHandler() => new(
        _safetyRecordRepo.Object,
        _safetySettingRepo.Object,
        _checkinRepo.Object,
        _mapper);

    [Fact]
    public async Task Handle_ReturnsStreak_WhenUserHasConsecutiveCheckins()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _safetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(userId, today, default)).ReturnsAsync((DailySafetyRecord?)null);
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((SafetySetting?)null);
        _checkinRepo.Setup(r => r.GetStreakAsync(userId, today, default)).ReturnsAsync(5);

        var result = await CreateHandler().Handle(new GetTodayCheckinStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Streak.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ReturnsZeroStreak_WhenUserHasNeverCheckedIn()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _safetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(userId, today, default)).ReturnsAsync((DailySafetyRecord?)null);
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((SafetySetting?)null);
        _checkinRepo.Setup(r => r.GetStreakAsync(userId, today, default)).ReturnsAsync(0);

        var result = await CreateHandler().Handle(new GetTodayCheckinStatusQuery(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Streak.Should().Be(0);
    }
}
```

### Bước 3.2 — Unit Test: Repository Streak Logic

File: `tests/Beacon.UnitTests/Checkins/CheckinRepositoryStreakTests.cs`  
*(Dùng InMemory EF hoặc mock DbSet nếu không có integration DB)*

Nếu không có InMemory DB sẵn → cover bằng integration test thay thế.

### Bước 3.3 — Unit Test: Mapper

File: `tests/Beacon.UnitTests/Checkins/CheckinStatusMapperTests.cs`

Verify `streak` được forward đúng trong cả 3 code path (CheckedIn, MonitoringDisabled, Pending/Overdue):

```csharp
[Theory]
[InlineData(true, 3)]    // CheckedIn path
[InlineData(false, 0)]   // Pending path
public void ToStatusDto_SetsStreak_Correctly(bool hasCheckedIn, int streak)
{
    var mapper = new CheckinStatusMapper();
    var dto = mapper.ToStatusDto(DateTime.UtcNow.AddHours(1), null, hasCheckedIn, true, true, streak);
    dto.Streak.Should().Be(streak);
}
```

### Bước 3.4 — Integration Test: Endpoint

File: `tests/Beacon.IntergrationTests/Checkins/CheckinsControllerTests.cs`

Test case bắt buộc:

```csharp
[Fact]
public async Task GetTodayStatus_ReturnsStreakField_ForAuthenticatedUser()
{
    // Arrange: seed user + checkin records cho 3 ngày liên tiếp gần nhất
    // Act: GET /api/v1/checkins/today-status
    // Assert:
    //   response.success == true
    //   response.data.streak == 3  (hoặc > 0 nếu seeded)
    //   response.data chứa đủ các field cũ: hasCheckedIn, status, deadlineAtUtc...
}

[Fact]
public async Task GetTodayStatus_ReturnsZeroStreak_WhenNoCheckinHistory()
{
    // Arrange: user mới, không có checkin nào
    // Assert: response.data.streak == 0
}
```

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN (bao gồm 3 test cases mới)
- [ ] `dotnet test tests/Beacon.IntergrationTests` — endpoint trả đúng `streak`
- [ ] Response JSON có trường `streak: int`
- [ ] Không regression trên các trường cũ: `hasCheckedIn`, `status`, `deadlineAtUtc`, v.v.

---

## Thứ tự thực hiện (TDD)

```
1. [Slice 1]  Viết test RED cho handler (GetStreakAsync mock trả 5)
2. [Slice 1]  Thêm GetStreakAsync vào ICheckinRepository
3. [Slice 1]  Implement GetStreakAsync trong CheckinRepository
4. [Slice 2]  Thêm Streak vào TodayCheckinStatusDto
5. [Slice 2]  Cập nhật CheckinStatusMapper (thêm tham số streak)
6. [Slice 2]  Cập nhật Handler (inject ICheckinRepository, gọi GetStreakAsync)
7. [Slice 3]  Chạy unit tests → phải GREEN
8. [Slice 3]  Viết integration test, chạy → GREEN
9.            dotnet build sạch → done
```

**Không cần**: migration, entity mới, controller mới, validator, mapper mới, DI registration mới.
