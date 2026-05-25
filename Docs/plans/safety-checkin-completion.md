# Plan: Safety/Checkin — Completion Sprint

**Spec**: `docs/specs/safety-checkin-completion.md`  
**Module**: Safety + Checkins  
**Phạm vi**: 10 slices, 5 phases

## Dependency Graph

```
Phase 0: Foundation (Slice 0.1 → 0.2 → 0.3)
    │
    ├── Phase 1: Fix Checkin Flow (Slice 1.1)
    │
    ├── Phase 2: Background Jobs (Slice 2.1 → 2.2)
    │
    ├── Phase 3: Checkin History (Slice 3.1) ← độc lập
    │
    └── Phase 4: EmergencyContact CRUD (Slice 4.1 → 4.2 → 4.3)
```

---

## Phase 0: Foundation

> Không phải use case — prerequisite bắt buộc. Không viết unit test cho phase này.

### Slice 0.1 — Domain Patch + Enable Entities + Repositories

**Bước 1 — Thêm `ClearPrimary()` vào `EmergencyContact`:**

```
src/Beacon.Domain/Entities/Safety/EmergencyContact.cs
```
```csharp
public void ClearPrimary() => IsPrimary = false;
```
> Cần cho `SetPrimaryEmergencyContactCommandHandler` — thiếu method này sẽ vi phạm encapsulation.

**Bước 2 — Bỏ `#if false` trong 2 EF configs:**
- `src/Beacon.Infrashtructure/Presistence/Configuration/Safety/AlertIncidentConfiguration.cs`
- `src/Beacon.Infrashtructure/Presistence/Configuration/Safety/EmergencyContactConfiguration.cs`

**Bước 3 — Uncomment 2 DbSet trong `AppDbContext.cs`:**
```csharp
public DbSet<AlertIncident> AlertIncidents => Set<AlertIncident>();
public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
```

**Bước 4 — Thêm soft-delete query filter trong `AppDbContext.OnModelCreating`:**
```csharp
modelBuilder.Entity<EmergencyContact>().HasQueryFilter(e => !e.IsDeleted);
```

**Bước 5 — Tạo `IAlertIncidentRepository`:**
```
src/Beacon.Domain/IRepository/Safety/IAlertIncidentRepository.cs
```
```csharp
public interface IAlertIncidentRepository
{
    Task AddAsync(AlertIncident incident, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**Bước 6 — Implement `AlertIncidentRepository`:**
```
src/Beacon.Infrashtructure/Repository/Safety/AlertIncidentRepository.cs
```
Đăng ký trong `InfrastructureServiceExtensions.cs`.

**Bước 7 — Tạo `IEmergencyContactRepository`:**
```
src/Beacon.Domain/IRepository/Safety/IEmergencyContactRepository.cs
```
```csharp
public interface IEmergencyContactRepository
{
    Task<IReadOnlyList<EmergencyContact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmergencyContact?> GetPrimaryByUserIdAsync(Guid userId, CancellationToken ct = default);
    // Chỉ đếm contacts có IsActive = true và IsDeleted = false (EF filter tự lọc IsDeleted)
    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(EmergencyContact contact, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**Bước 8 — Implement `EmergencyContactRepository`:**
```
src/Beacon.Infrashtructure/Repository/Safety/EmergencyContactRepository.cs
```
Đăng ký trong `InfrastructureServiceExtensions.cs`.

**Bước 9 — Thêm error codes vào `Beacon.Shared/Constants/ErrorCodes.cs`:**
```csharp
public const string EMERGENCY_CONTACT_NOT_FOUND = "EMERGENCY_CONTACT_NOT_FOUND";
public const string EMERGENCY_CONTACT_LIMIT_EXCEEDED = "EMERGENCY_CONTACT_LIMIT_EXCEEDED";
public const string EMERGENCY_CONTACT_FORBIDDEN = "EMERGENCY_CONTACT_FORBIDDEN";
```

**Acceptance criteria:**
- [ ] `dotnet build` — 0 error (cả sau khi bỏ `#if false`)
- [ ] 2 DbSet mới xuất hiện trong `AppDbContext`
- [ ] Soft-delete filter `EmergencyContact` đăng ký trong `OnModelCreating`
- [ ] 2 repo interfaces + implementations compile và đăng ký DI

**Dependencies**: Không có

---

### Slice 0.2 — EF Migration

```bash
dotnet ef migrations add Enable_AlertIncident_And_EmergencyContact \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api

dotnet ef database update \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

**Review migration file trước khi apply — verify:**
- Tạo bảng `AlertIncidents` với `UNIQUE INDEX IX_AlertIncidents_DailySafetyRecordId`
- Tạo bảng `EmergencyContacts` (có `IsDeleted` column)
- Không có `DROP` nào ngoài ý muốn

**Dependencies**: Slice 0.1

---

### Slice 0.3 — Hangfire Infrastructure

**Thêm NuGet vào `Beacon.Api`:**
```bash
dotnet add src/Beacon.Api package Hangfire.AspNetCore
dotnet add src/Beacon.Api package Hangfire.SqlServer
```

**Tạo `Api/Extensions/HangfireExtensions.cs`:**
```csharp
public static class HangfireExtensions
{
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(config.GetConnectionString("DefaultConnection"),
                new SqlServerStorageOptions { SchemaName = "Hangfire" }));
        services.AddHangfireServer();

        // Đăng ký job classes là Scoped — Hangfire tạo scope mới cho mỗi lần chạy
        services.AddScoped<SafetyReminderJob>();
        services.AddScoped<SafetyMissedCheckerJob>();

        return services;
    }

    public static void MapHangfireDashboard(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
            app.MapHangfireDashboard("/hangfire");
    }

    public static void RegisterRecurringJobs(this IApplicationBuilder app)
    {
        RecurringJob.AddOrUpdate<SafetyReminderJob>(
            "safety-reminder",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));

        RecurringJob.AddOrUpdate<SafetyMissedCheckerJob>(
            "safety-missed-checker",
            job => job.ExecuteAsync(),
            Cron.MinuteInterval(5));
    }
}
```

Gọi từ `Program.cs` qua extension — không đăng ký trực tiếp trong `Program.cs`.

**Dependencies**: Slice 0.1

---

## ✅ Checkpoint: Foundation Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] Migration file review: 2 bảng mới, unique index `AlertIncidents`
- [ ] `dotnet ef database update` thành công
- [ ] Hangfire NuGet resolve — project build sau khi thêm package

---

## Phase 1: Fix Existing Checkin Flow

### Slice 1.1 — Auto-resolve AlertIncident + CheckinType.Recovery

**Type**: Command (modify existing)  
**Files sửa**:
- `CreateCheckinCommandHandler.cs`
- `DailySafetyRecordRepository.cs` (thêm Include)

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Checkins/CreateCheckinCommandHandlerTests.cs
```

Test cases bổ sung:
```csharp
Handle_WhenRecordIsPending_ShouldCreateCheckinWithTypeManual_AndStatusCheckedIn()
Handle_WhenRecordIsMissed_ShouldCreateCheckinWithTypeRecovery_AndStatusResolved()
Handle_WhenRecordIsAlerted_WithIncident_ShouldResolveIncident_AndMarkRecordResolved()
Handle_WhenRecordIsResolved_ShouldReturnAlreadyCheckedInError()
```

Mock `GetByUserIdAndDateAsync` trả record với các trạng thái khác nhau. Test FAIL vì handler chưa có logic phân nhánh.

---

**Bước 2 — Sửa `DailySafetyRecordRepository.GetByUserIdAndDateAsync`:**
```csharp
public Task<DailySafetyRecord?> GetByUserIdAndDateAsync(
    Guid userId, DateOnly date, CancellationToken ct)
    => db.DailySafetyRecords
        .Include(r => r.AlertIncident)   // ← thêm
        .FirstOrDefaultAsync(r => r.UserId == userId && r.Date == date, ct);
```

**Bước 3 — Sửa `CreateCheckinCommandHandler.Handle` — tách 2 nhánh:**

```csharp
// Guard: đã CheckedIn hoặc Resolved → không cho checkin lại
if (record.Status is SafetyStatus.CheckedIn or SafetyStatus.Resolved)
    return Result<CheckinDto>.Failure(
        Error.Conflict(ErrorCodes.Safety.ALREADY_CHECKED_IN, "Bạn đã check-in hôm nay rồi."));

// Nhánh A — Pending (checkin đúng hạn)
if (record.Status == SafetyStatus.Pending)
{
    var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Manual, today,
        req.Note, req.Latitude, req.Longitude);
    if (req.MediaId.HasValue)
        checkin.MediaItems.Add(CheckinMedia.Create(checkin.Id, req.MediaId.Value, isPrimary: true));

    record.MarkCheckedIn(checkin.CheckedInAtUtc);

    await checkinRepo.AddAsync(checkin, ct);
    await checkinRepo.SaveChangesAsync(ct);
    return Result<CheckinDto>.Success(mapper.ToDto(checkin, req.MediaId));
}

// Nhánh B — Missed hoặc Alerted (checkin muộn / recovery)
{
    var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Recovery, today,
        req.Note, req.Latitude, req.Longitude);
    if (req.MediaId.HasValue)
        checkin.MediaItems.Add(CheckinMedia.Create(checkin.Id, req.MediaId.Value, isPrimary: true));

    if (record.AlertIncident is not null
        && record.AlertIncident.Status != AlertIncidentStatus.Resolved)
        record.AlertIncident.Resolve();

    record.MarkResolved();

    await checkinRepo.AddAsync(checkin, ct);
    await checkinRepo.SaveChangesAsync(ct);
    return Result<CheckinDto>.Success(mapper.ToDto(checkin, req.MediaId));
}
```

**Bước 4 — Chạy lại test (GREEN).**

**Acceptance criteria:**
- [ ] `Pending` → `CheckinType.Manual`, `DailySafetyRecord.Status = CheckedIn`
- [ ] `Missed/Alerted` → `CheckinType.Recovery`, `Status = Resolved`, `AlertIncident.Status = Resolved` nếu có
- [ ] `CheckedIn` hoặc `Resolved` → `Error.Conflict(ALREADY_CHECKED_IN)`
- [ ] Unit tests GREEN — 4 scenarios

**Dependencies**: Slice 0.1, 0.2

---

## ✅ Checkpoint: Phase 1 Complete

- [ ] `dotnet build` — 0 error
- [ ] Unit tests `CreateCheckinCommandHandlerTests` — GREEN (4 cases mới)
- [ ] Manual: POST `/api/v1/checkins` khi record `Missed` → `type = "Recovery"` trong response

---

## Phase 2: Background Jobs

### Slice 2.1 — SafetyReminderJob

**Type**: Background Job (system use case)

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Safety/SafetyReminderJobTests.cs
```
```csharp
ExecuteAsync_WhenRecordsPendingNeedReminder_ShouldSendFcmAndRecordSent()
ExecuteAsync_WhenNoRecordsPending_ShouldNotCallFcm()
ExecuteAsync_WhenFcmThrows_ShouldContinueNextRecord_AndNotThrow()
```

---

**Bước 2 — Thêm method vào `IDailySafetyRecordRepository`:**
```csharp
Task<IReadOnlyList<DailySafetyRecord>> GetPendingNeedingReminderAsync(
    DateTimeOffset now, CancellationToken ct = default);
```

**Bước 3 — Implement trong `DailySafetyRecordRepository`:**
```csharp
public async Task<IReadOnlyList<DailySafetyRecord>> GetPendingNeedingReminderAsync(
    DateTimeOffset now, CancellationToken ct)
    => await db.DailySafetyRecords
        .Join(db.SafetySettings, r => r.UserId, s => s.UserId, (r, s) => new { r, s })
        .Where(x => x.r.Status == SafetyStatus.Pending
                 && x.r.ReminderSentAtUtc == null
                 && x.s.IsMonitoringEnabled
                 && x.r.DeadlineAtUtc <= now.UtcDateTime.AddMinutes(x.s.ReminderBeforeMinutes))
        .Select(x => x.r)
        .ToListAsync(ct);
```

**Bước 4 — Tạo `SafetyReminderJob`:**

```
src/Beacon.Api/Backgroundjobs/SafetyReminderJob.cs
```
```csharp
public class SafetyReminderJob(
    IDailySafetyRecordRepository repo,
    IFcmService fcm,
    ILogger<SafetyReminderJob> logger)
{
    private static readonly TimeZoneInfo VnTz = GetVnTz();

    private static TimeZoneInfo GetVnTz()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var records = await repo.GetPendingNeedingReminderAsync(now);

            foreach (var record in records)
            {
                try
                {
                    var remaining = (int)(record.DeadlineAtUtc - now.UtcDateTime).TotalMinutes;
                    await fcm.SendToUserAsync(record.UserId,
                        "Nhắc nhở checkin an toàn",
                        $"Bạn còn {remaining} phút để checkin hôm nay.");
                    record.RecordReminderSent();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Reminder failed for UserId={UserId}", record.UserId);
                }
            }

            await repo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyReminderJob failed");
        }
    }
}
```

**Acceptance criteria:**
- [ ] Unit tests GREEN — 3 scenarios
- [ ] Job trigger thủ công: records Pending → FCM + `ReminderSentAtUtc` được ghi
- [ ] 1 record FCM lỗi không làm hỏng các record còn lại

**Dependencies**: Slice 0.1, 0.2, 0.3

---

### Slice 2.2 — SafetyMissedCheckerJob

**Type**: Background Job (system use case)

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Safety/SafetyMissedCheckerJobTests.cs
```
```csharp
ExecuteAsync_Phase1_WhenPendingPastDeadline_ShouldMarkMissed()
ExecuteAsync_Phase2_WhenMissedWithDelay_ShouldCreateAlertIncidentAndSendFcm()
ExecuteAsync_Phase2_WhenFcmFails_ShouldLogError_ContinueNextRecord_AndNotThrow()
ExecuteAsync_Idempotent_WhenAlertAlreadyExists_ShouldNotCreateDuplicate()
```

---

**Bước 2 — Thêm 2 method vào `IDailySafetyRecordRepository`:**
```csharp
Task<IReadOnlyList<DailySafetyRecord>> GetPendingPastDeadlineAsync(
    DateTimeOffset now, CancellationToken ct = default);

Task<IReadOnlyList<DailySafetyRecord>> GetMissedNeedingAlertAsync(
    DateTimeOffset now, CancellationToken ct = default);
```

**Bước 3 — Implement trong `DailySafetyRecordRepository`:**

`GetPendingPastDeadlineAsync`:
```csharp
=> await db.DailySafetyRecords
    .Where(r => r.Status == SafetyStatus.Pending
             && r.DeadlineAtUtc < now.UtcDateTime)
    .ToListAsync(ct);
```

`GetMissedNeedingAlertAsync`:
```csharp
=> await db.DailySafetyRecords
    .Include(r => r.AlertIncident)
    .Join(db.SafetySettings, r => r.UserId, s => s.UserId, (r, s) => new { r, s })
    .Where(x => (x.r.Status == SafetyStatus.Missed || x.r.Status == SafetyStatus.Alerted)
             && x.r.AlertIncident == null
             && x.s.IsAutoAlertEnabled
             && x.r.MarkedMissedAtUtc != null
             && x.r.MarkedMissedAtUtc.Value.AddMinutes(x.s.AutoAlertDelayMinutes) <= now.UtcDateTime)
    .Select(x => x.r)
    .ToListAsync(ct);
```

**Bước 4 — Tạo `SafetyMissedCheckerJob`:**

```
src/Beacon.Api/Backgroundjobs/SafetyMissedCheckerJob.cs
```

```csharp
public class SafetyMissedCheckerJob(
    IDailySafetyRecordRepository recordRepo,
    IAlertIncidentRepository alertRepo,
    IFcmService fcm,
    ILogger<SafetyMissedCheckerJob> logger)
{
    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        // Phase 1 — Mark Missed (try/catch riêng để Phase 2 vẫn chạy nếu Phase 1 lỗi)
        try
        {
            var pendingPastDeadline = await recordRepo.GetPendingPastDeadlineAsync(now);
            foreach (var record in pendingPastDeadline)
                record.MarkMissed();
            await recordRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 1 (MarkMissed) failed");
        }

        // Phase 2 — Create AlertIncident + FCM
        try
        {
            var missedNeedingAlert = await recordRepo.GetMissedNeedingAlertAsync(now);
            foreach (var record in missedNeedingAlert)
            {
                try
                {
                    var incident = AlertIncident.Create(
                        record.UserId, record.Id, AlertIncidentType.MissedCheckin);
                    await alertRepo.AddAsync(incident);

                    await fcm.SendToUserAsync(record.UserId,
                        "Cảnh báo: Bạn chưa checkin!",
                        "Hệ thống đã ghi nhận bạn chưa checkin hôm nay. Vui lòng checkin ngay.");

                    incident.MarkSent();
                    record.MarkAlerted();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Alert failed for UserId={UserId}", record.UserId);
                }
            }
            await alertRepo.SaveChangesAsync();
            await recordRepo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SafetyMissedCheckerJob Phase 2 (AlertIncident) failed");
        }
    }
}
```

**Acceptance criteria:**
- [ ] Unit tests GREEN — 4 scenarios
- [ ] Phase 1 và Phase 2 có try/catch riêng — Phase 1 lỗi không block Phase 2
- [ ] 1 record trong Phase 2 lỗi FCM: log lỗi, tiếp tục record tiếp theo, không throw
- [ ] Unique index `IX_AlertIncidents_DailySafetyRecordId` là guard cuối chống duplicate

**Dependencies**: Slice 0.1, 0.2, 0.3, Slice 2.1 (interface chung)

---

## ✅ Checkpoint: Phase 2 Complete

- [ ] `dotnet build` — 0 error
- [ ] Unit tests `Safety/*` — GREEN
- [ ] Hangfire Dashboard `/hangfire` hiện 2 recurring jobs
- [ ] Trigger job thủ công → verify DB state đúng

---

## Phase 3: Checkin History

### Slice 3.1 — GET /api/v1/checkins/history

**Type**: Query (read-only)

> Trả rows từ bảng `Checkins` — không phải DailySafetyRecord timeline.

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Checkins/GetCheckinHistoryQueryHandlerTests.cs
```
```csharp
Handle_WhenNoCursor_ShouldReturnFirstPage()
Handle_WhenCursorProvided_ShouldReturnItemsOlderThanCursor()
Handle_WhenNoCheckins_ShouldReturnEmptyItems()
Handle_WhenLimitExceeds100_ShouldReturnValidationError()
```

---

**Bước 2 — Thêm method vào `ICheckinRepository`:**
```csharp
Task<CursorPagedResult<Checkin>> GetPagedByUserIdAsync(
    Guid userId, DateTimeOffset? cursor, int limit, CancellationToken ct = default);
```

**Bước 3 — Implement trong `CheckinRepository`:**
```csharp
public async Task<CursorPagedResult<Checkin>> GetPagedByUserIdAsync(
    Guid userId, DateTimeOffset? cursor, int limit, CancellationToken ct)
{
    var query = db.Checkins
        .Include(c => c.MediaItems)
            .ThenInclude(m => m.MediaObject)
        .Where(c => c.UserId == userId);

    if (cursor.HasValue)
        query = query.Where(c => c.CheckedInAtUtc < cursor.Value.UtcDateTime);

    var items = await query
        .OrderByDescending(c => c.CheckedInAtUtc)
        .Take(limit + 1)
        .ToListAsync(ct);

    var hasMore = items.Count > limit;
    if (hasMore) items.RemoveAt(limit);

    var nextCursor = hasMore ? (DateTimeOffset?)items.Last().CheckedInAtUtc : null;

    return new CursorPagedResult<Checkin>(items, nextCursor, hasMore);
}
```

**Bước 4 — Query:**
```
src/Beacon.Application/Features/Checkins/Queries/GetCheckinHistory/GetCheckinHistoryQuery.cs
```
```csharp
public record GetCheckinHistoryQuery(Guid UserId, DateTimeOffset? Cursor, int Limit)
    : IRequest<Result<CursorPagedResult<CheckinHistoryItemDto>>>;
```

**Bước 5 — Handler:**
```
src/Beacon.Application/Features/Checkins/Queries/GetCheckinHistory/GetCheckinHistoryQueryHandler.cs
```

**Bước 6 — Validator:**
```
src/Beacon.Application/Features/Checkins/Validators/Checkins/GetCheckinHistoryQueryValidator.cs
```
```csharp
RuleFor(x => x.Limit)
    .InclusiveBetween(1, 100).WithMessage("Limit phải trong khoảng 1–100.");
```

**Bước 7 — DTOs:**
```
src/Beacon.Application/Features/Checkins/Dtos/CheckinHistoryItemDto.cs
src/Beacon.Application/Features/Checkins/Dtos/CheckinMediaItemDto.cs
```

**Bước 8 — Mapper:**
```
src/Beacon.Application/Mappings/Checkins/CheckinHistoryMapper.cs
```
Đăng ký Singleton trong `ApplicationServiceExtensions.cs`.

**Bước 9 — Controller action** (thêm vào `CheckinsController`):
```csharp
#region
/// <summary>Lấy lịch sử các lần checkin của user hiện tại</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Trả danh sách lần user đã checkin (bảng Checkins), mới nhất trước.
/// Không bao gồm ngày chưa checkin hay Missed.
///
/// Cursor là <c>CheckedInAtUtc</c> (ISO-8601 UTC) của item cuối trang trước.
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: limit ngoài khoảng 1–100.
///
/// Cấu trúc <c>data</c>:
/// <code>{ "items": [...], "nextCursor": "ISO-UTC|null", "hasMore": bool }</code>
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
[HttpGet("history")]
public async Task<IActionResult> GetHistory(
    [FromQuery] DateTimeOffset? cursor,
    [FromQuery] int limit = 20,
    CancellationToken ct = default)
    => HandleResult(await mediator.Send(
        new GetCheckinHistoryQuery(currentUser.UserId, cursor, limit), ct));
```

**Bước 10 — Integration test:**
```
tests/Beacon.IntergrationTests/Checkins/CheckinsControllerTests.cs (bổ sung)
```
- `GET /checkins/history` — 200, `items` + `nextCursor` + `hasMore`
- `GET /checkins/history?limit=0` — 400
- `GET /checkins/history` không có token — 401

**Acceptance criteria:**
- [ ] Chỉ trả rows từ bảng `Checkins`
- [ ] `CheckinType.Recovery` hiện đúng trong field `type`
- [ ] cursor = null → trang đầu; cursor != null → trang tiếp
- [ ] Unit + Integration tests GREEN

**Dependencies**: Slice 0.2

---

## ✅ Checkpoint: Phase 3 Complete

- [ ] `dotnet build` — 0 error
- [ ] `GET /api/v1/checkins/history` cursor pagination hoạt động đúng
- [ ] Checkin Recovery sau Missed hiện trong history với `type = "Recovery"`

---

## Phase 4: EmergencyContact CRUD

### Slice 4.1 — List + Create EmergencyContact

**Type**: Query + Command

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Safety/GetEmergencyContactsQueryHandlerTests.cs
tests/Beacon.UnitTests/Safety/CreateEmergencyContactCommandHandlerTests.cs
```
```csharp
// GetEmergencyContacts
Handle_ShouldReturnOnlyUserContacts()
Handle_WhenNoContacts_ShouldReturnEmptyList()

// CreateEmergencyContact
Handle_WhenValidRequest_ShouldCreateContact()
Handle_WhenLimitExceeded_ShouldReturnLimitExceededError()
```

---

**Bước 2 — DTOs:**
```
src/Beacon.Application/Features/Safety/Dtos/EmergencyContactDto.cs
src/Beacon.Application/Features/Safety/Dtos/CreateEmergencyContactRequest.cs
```
```csharp
public record EmergencyContactDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = default!;
    public string ContactValue { get; init; } = default!;
    public string? Relationship { get; init; }
    public string ChannelType { get; init; } = default!;
    public int PriorityOrder { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; }
}
```

**Bước 3 — Mapper:**
```
src/Beacon.Application/Mappings/Safety/EmergencyContactMapper.cs
```
Đăng ký Singleton trong `ApplicationServiceExtensions.cs`.

**Bước 4 — Query + Handler:**
```
src/Beacon.Application/Features/Safety/Queries/GetEmergencyContacts/
  GetEmergencyContactsQuery.cs
  GetEmergencyContactsQueryHandler.cs
```

**Bước 5 — Command + Handler:**
```
src/Beacon.Application/Features/Safety/Commands/CreateEmergencyContact/
  CreateEmergencyContactCommand.cs
  CreateEmergencyContactCommandHandler.cs
```

Handler logic:
```csharp
var count = await _repo.CountActiveByUserIdAsync(cmd.UserId, ct);
if (count >= 5)
    return Result<EmergencyContactDto>.Failure(
        Error.Conflict(ErrorCodes.EMERGENCY_CONTACT_LIMIT_EXCEEDED,
            "Bạn chỉ có thể thêm tối đa 5 liên hệ khẩn cấp."));

var contact = EmergencyContact.Create(
    cmd.UserId, cmd.Request.FullName, cmd.Request.ContactValue,
    cmd.Request.ChannelType, cmd.Request.Relationship, cmd.Request.PriorityOrder);

await _repo.AddAsync(contact, ct);
await _repo.SaveChangesAsync(ct);
return Result<EmergencyContactDto>.Success(_mapper.ToDto(contact));
```

**Bước 6 — Validator:**
```
src/Beacon.Application/Features/Safety/Validators/Safety/CreateEmergencyContactCommandValidator.cs
```
```csharp
RuleFor(x => x.Request.FullName).NotEmpty().WithMessage("Họ tên không được để trống.")
    .MaximumLength(200).WithMessage("Họ tên không được vượt quá 200 ký tự.");
RuleFor(x => x.Request.ContactValue).NotEmpty().WithMessage("Thông tin liên hệ không được để trống.")
    .MaximumLength(255).WithMessage("Thông tin liên hệ không được vượt quá 255 ký tự.");
RuleFor(x => x.Request.ChannelType)
    .IsInEnum().WithMessage("Loại kênh liên hệ không hợp lệ.");
RuleFor(x => x.Request.Relationship)
    .MaximumLength(100).WithMessage("Quan hệ không được vượt quá 100 ký tự.")
    .When(x => x.Request.Relationship is not null);
RuleFor(x => x.Request.PriorityOrder)
    .InclusiveBetween(1, 99).WithMessage("Thứ tự ưu tiên phải trong khoảng 1–99.");
```

**Bước 7 — Controller (GET + POST):**
```
src/Beacon.Api/Controllers/Safety/EmergencyContactsController.cs
```
```csharp
[Route("api/v1/emergency-contacts")]
[Authorize]
public class EmergencyContactsController(IMediator mediator, ICurrentUserService currentUser)
    : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => HandleResult(await mediator.Send(
            new GetEmergencyContactsQuery(currentUser.UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmergencyContactRequest req, CancellationToken ct)
        => CreatedResult("api/v1/emergency-contacts",
            await mediator.Send(new CreateEmergencyContactCommand(currentUser.UserId, req), ct));
}
```

**Bước 8 — Integration test:**
```
tests/Beacon.IntergrationTests/Safety/EmergencyContactsControllerTests.cs
```
- `GET /emergency-contacts` — 200, trả list
- `POST /emergency-contacts` — 201, tạo thành công
- `POST /emergency-contacts` lần 6 — 409 `EMERGENCY_CONTACT_LIMIT_EXCEEDED`
- Không có token — 401

**Acceptance criteria:**
- [ ] Tối đa 5 contacts — kiểm tra trong Handler (không phải Validator)
- [ ] `GET` chỉ trả contacts của user hiện tại (`IsDeleted = false` tự lọc bởi EF filter)
- [ ] Unit + Integration tests GREEN

**Dependencies**: Slice 0.1, 0.2

---

### Slice 4.2 — Update + Delete EmergencyContact

**Type**: Command x2

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Safety/UpdateEmergencyContactCommandHandlerTests.cs
tests/Beacon.UnitTests/Safety/DeleteEmergencyContactCommandHandlerTests.cs
```
```csharp
// Update
Handle_WhenValidOwner_ShouldUpdateContact()
Handle_WhenContactNotFound_ShouldReturnNotFoundError()
Handle_WhenNotOwner_ShouldReturnForbiddenError()

// Delete
Handle_WhenValidOwner_ShouldSoftDeleteContact()
Handle_WhenContactNotFound_ShouldReturnNotFoundError()
Handle_WhenNotOwner_ShouldReturnForbiddenError()
```

---

**Bước 2 — Command + Handler (Update):**
```
src/Beacon.Application/Features/Safety/Commands/UpdateEmergencyContact/
  UpdateEmergencyContactCommand.cs
  UpdateEmergencyContactCommandHandler.cs
```

Handler — owner check pattern:
```csharp
var contact = await _repo.GetByIdAsync(cmd.ContactId, ct);
if (contact is null)
    return Result<EmergencyContactDto>.Failure(
        Error.NotFound(ErrorCodes.EMERGENCY_CONTACT_NOT_FOUND, "Không tìm thấy liên hệ khẩn cấp."));
if (contact.UserId != cmd.UserId)
    return Result<EmergencyContactDto>.Failure(
        Error.Forbidden(ErrorCodes.EMERGENCY_CONTACT_FORBIDDEN, "Bạn không có quyền chỉnh sửa liên hệ này."));
// update fields...
```

**Bước 3 — Command + Handler (Delete — soft delete):**
```
src/Beacon.Application/Features/Safety/Commands/DeleteEmergencyContact/
  DeleteEmergencyContactCommand.cs
  DeleteEmergencyContactCommandHandler.cs
```

```csharp
contact.Deactivate(); // IsActive = false
// SoftDeletableEntity: set IsDeleted = true
// Cần method SoftDelete() trong entity hoặc set trực tiếp qua SoftDeletableEntity base
```

> `EmergencyContact : SoftDeletableEntity` — dùng `Deactivate()` để set `IsActive = false`.  
> EF soft-delete filter (`IsDeleted`) cần được set — kiểm tra `SoftDeletableEntity` base class có method `Delete()` hay không; nếu không thì thêm.

**Bước 4 — Validator (Update):**
```
src/Beacon.Application/Features/Safety/Validators/Safety/UpdateEmergencyContactCommandValidator.cs
```
Tương tự `CreateEmergencyContactCommandValidator`.

**Bước 5 — Controller (PUT + DELETE):**
```csharp
[HttpPut("{id:guid}")]
public async Task<IActionResult> Update(Guid id,
    [FromBody] UpdateEmergencyContactRequest req, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new UpdateEmergencyContactCommand(currentUser.UserId, id, req), ct));

[HttpDelete("{id:guid}")]
public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new DeleteEmergencyContactCommand(currentUser.UserId, id), ct));
```

**Bước 6 — Integration test (bổ sung vào `EmergencyContactsControllerTests`):**
- `PUT /emergency-contacts/{id}` — 200 success
- `PUT /emergency-contacts/{id}` sai owner — 403
- `PUT /emergency-contacts/{id-not-found}` — 404
- `DELETE /emergency-contacts/{id}` — 200, contact không còn trong `GET`
- `DELETE /emergency-contacts/{id}` sai owner — 403

**Acceptance criteria:**
- [ ] Owner check trong Handler (không phải Controller)
- [ ] Soft delete: contact không còn xuất hiện trong `GET /emergency-contacts` sau khi xóa
- [ ] Unit + Integration tests GREEN

**Dependencies**: Slice 0.1, 0.2, Slice 4.1

---

### Slice 4.3 — SetPrimary EmergencyContact

**Type**: Command

---

**Bước 1 — Viết Test (RED):**

```
tests/Beacon.UnitTests/Safety/SetPrimaryEmergencyContactCommandHandlerTests.cs
```
```csharp
Handle_WhenNoPreviousPrimary_ShouldSetContactAsPrimary()
Handle_WhenPreviousPrimaryExists_ShouldClearOldAndSetNew()
Handle_WhenContactNotFound_ShouldReturnNotFoundError()
Handle_WhenNotOwner_ShouldReturnForbiddenError()
Handle_WhenContactAlreadyPrimary_ShouldReturnSuccess_NoSideEffect()
```

---

**Bước 2 — Command + Handler:**
```
src/Beacon.Application/Features/Safety/Commands/SetPrimaryEmergencyContact/
  SetPrimaryEmergencyContactCommand.cs
  SetPrimaryEmergencyContactCommandHandler.cs
```

Handler logic:
```csharp
var contact = await _repo.GetByIdAsync(cmd.ContactId, ct);
if (contact is null)
    return Result<EmergencyContactDto>.Failure(
        Error.NotFound(ErrorCodes.EMERGENCY_CONTACT_NOT_FOUND, "Không tìm thấy liên hệ khẩn cấp."));
if (contact.UserId != cmd.UserId)
    return Result<EmergencyContactDto>.Failure(
        Error.Forbidden(ErrorCodes.EMERGENCY_CONTACT_FORBIDDEN, "Bạn không có quyền thao tác liên hệ này."));

// Unset primary cũ (nếu khác contact hiện tại)
var currentPrimary = await _repo.GetPrimaryByUserIdAsync(cmd.UserId, ct);
if (currentPrimary is not null && currentPrimary.Id != cmd.ContactId)
    currentPrimary.ClearPrimary();

contact.SetAsPrimary();
await _repo.SaveChangesAsync(ct);
return Result<EmergencyContactDto>.Success(_mapper.ToDto(contact));
```

**Bước 3 — Controller (PATCH):**
```csharp
[HttpPatch("{id:guid}/set-primary")]
public async Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
    => HandleResult(await mediator.Send(
        new SetPrimaryEmergencyContactCommand(currentUser.UserId, id), ct));
```

**Bước 4 — Integration test (bổ sung):**
- `PATCH /emergency-contacts/{id}/set-primary` — 200, `isPrimary = true`
- Sau set-primary, `GET` → chỉ 1 contact có `isPrimary = true`
- `PATCH` sai owner — 403
- `PATCH` id không tồn tại — 404

**Acceptance criteria:**
- [ ] Sau set-primary: đúng 1 contact có `IsPrimary = true` trong user's list
- [ ] Contact trước đó là primary bị clear (`IsPrimary = false`)
- [ ] Idempotent: set-primary contact đã là primary → không có side effect
- [ ] Unit + Integration tests GREEN

**Dependencies**: Slice 0.1, 0.2, Slice 4.1, 4.2

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — tất cả GREEN
- [ ] `dotnet test tests/Beacon.IntergrationTests` — tất cả GREEN
- [ ] Swagger: tất cả endpoint mới hiện đúng route, auth requirement, XML doc
- [ ] Hangfire Dashboard: 2 recurring jobs hiện và có thể trigger thủ công
- [ ] `dotnet ef migrations list` — migration `Enable_AlertIncident_And_EmergencyContact` applied

---

## Tóm tắt thứ tự triển khai

| Slice | Mô tả | Phụ thuộc |
|---|---|---|
| **0.1** | Domain patch + Enable entities + Repos + Error codes | — |
| **0.2** | EF Migration | 0.1 |
| **0.3** | Hangfire NuGet + Extensions | 0.1 |
| **1.1** | Auto-resolve + CheckinType.Recovery | 0.1, 0.2 |
| **2.1** | SafetyReminderJob + GetPendingNeedingReminderAsync | 0.1, 0.2, 0.3 |
| **2.2** | SafetyMissedCheckerJob + 2 repo methods | 0.1, 0.2, 0.3, 2.1 |
| **3.1** | GET /checkins/history | 0.2 |
| **4.1** | EmergencyContact List + Create | 0.1, 0.2 |
| **4.2** | EmergencyContact Update + Delete | 0.1, 0.2, 4.1 |
| **4.3** | EmergencyContact SetPrimary | 0.1, 0.2, 4.1, 4.2 |

**Có thể chạy song song sau Checkpoint Foundation:**
- `1.1` + `3.1` + `4.1` (độc lập nhau)
- `2.1` sau khi `1.1` done
- `2.2` sau khi `2.1` done
- `4.2` sau `4.1`; `4.3` sau `4.2`
