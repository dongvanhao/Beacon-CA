# Feature: Safety/Checkin — Completion Sprint

## Objective

Hoàn thiện module Safety/Checkin bằng cách bật AlertIncident + EmergencyContact (đã scaffold nhưng bị disable), thêm background jobs Hangfire để tự động nhắc nhở và phát cảnh báo, bổ sung checkin history API, và CRUD EmergencyContact — tất cả không phá vỡ API hiện tại.

## Target Users

- **User thường** (`[Authorize]`): tạo checkin, xem history, quản lý emergency contact của chính mình
- **System (background jobs)**: chạy reminder và missed-checker tự động, không expose API

---

## Current State (đã có, không đụng)

| Thành phần | Trạng thái |
|---|---|
| `DailySafetyRecord` entity + methods | ✅ Enabled |
| `Checkin` + `CheckinMedia` + EF config | ✅ Enabled |
| `SafetySetting` entity + config | ✅ Enabled |
| `CreateCheckinCommandHandler` | ✅ Hoạt động |
| `GetTodayCheckinStatusQueryHandler` | ✅ Hoạt động |
| `IFcmService.SendToUserAsync` | ✅ Sẵn sàng |
| `AlertIncident` entity + methods | 🔴 Disabled (`#if false` trong EF config, comment trong AppDbContext) |
| `EmergencyContact` entity + methods | 🔴 Disabled (comment trong AppDbContext) |
| `IAlertIncidentRepository` | ❌ Chưa có |
| `IEmergencyContactRepository` | ❌ Chưa có |
| Hangfire NuGet | ❌ Chưa có |

---

## Scope — 6 Task Groups

### Task 1 — Repository Methods (IDailySafetyRecordRepository + ICheckinRepository)

**3 method mới cho `IDailySafetyRecordRepository`:**

```csharp
// Records Pending, ReminderSentAtUtc IS NULL, DeadlineAtUtc - now <= setting.ReminderBeforeMinutes
// JOIN SafetySetting theo UserId; IsMonitoringEnabled = true
Task<IReadOnlyList<DailySafetyRecord>> GetPendingNeedingReminderAsync(
    DateTimeOffset now, CancellationToken ct = default);

// Records Pending, DeadlineAtUtc < now (đã quá deadline, chưa mark Missed)
Task<IReadOnlyList<DailySafetyRecord>> GetPendingPastDeadlineAsync(
    DateTimeOffset now, CancellationToken ct = default);

// Records Status = Missed hoặc Alerted, chưa có AlertIncident (navigation null),
// IsAutoAlertEnabled = true trong SafetySetting,
// MarkedMissedAtUtc + AutoAlertDelayMinutes <= now
Task<IReadOnlyList<DailySafetyRecord>> GetMissedNeedingAlertAsync(
    DateTimeOffset now, CancellationToken ct = default);
```

**Acceptance criteria:**
- `GetPendingNeedingReminderAsync`: `Status = Pending`, `ReminderSentAtUtc IS NULL`, `DeadlineAtUtc <= now + TimeSpan.FromMinutes(setting.ReminderBeforeMinutes)`, `IsMonitoringEnabled = true`
- `GetPendingPastDeadlineAsync`: `Status = Pending`, `DeadlineAtUtc < now` — không cần JOIN SafetySetting
- `GetMissedNeedingAlertAsync`: `Status IN (Missed, Alerted)`, LEFT JOIN AlertIncident → IS NULL, `IsAutoAlertEnabled = true`, `MarkedMissedAtUtc + AutoAlertDelayMinutes <= now`

**1 method mới cho `ICheckinRepository`:**

```csharp
// Cursor pagination (cursor = CheckedInAtUtc của item cuối), sắp xếp mới nhất trước
// Include: CheckinMedia (với MediaObject)
// Chỉ trả checkin của userId chỉ định
Task<CursorPagedResult<Checkin>> GetPagedByUserIdAsync(
    Guid userId, DateTimeOffset? cursor, int limit, CancellationToken ct = default);
```

**Acceptance criteria:**
- cursor = null → lấy trang đầu (không có WHERE trên CheckedInAtUtc)
- cursor != null → `CheckedInAtUtc < cursor`
- Limit max 100; sắp xếp DESC theo `CheckedInAtUtc`

---

### Task 2 — Bật AlertIncident + EmergencyContact (EF + DbContext)

**Bước 1 — Bỏ `#if false` trong:**
- `Infrastructure/Presistence/Configuration/Safety/AlertIncidentConfiguration.cs`
- `Infrastructure/Presistence/Configuration/Safety/EmergencyContactConfiguration.cs`

> **Lưu ý:** `AlertIncidentConfiguration` đã định nghĩa unique index trên `DailySafetyRecordId` trong block `#if false` — sau khi bỏ flag, index này tự động được tạo trong migration, đảm bảo mỗi `DailySafetyRecord` chỉ có tối đa 1 `AlertIncident`. Đây là guard DB-level chống duplicate song song với guard ở tầng query (`AlertIncident IS NULL`).

**Bước 2 — Uncomment trong `AppDbContext.cs`:**
- `DbSet<EmergencyContact> EmergencyContacts`
- `DbSet<AlertIncident> AlertIncidents`

**Bước 3 — Tạo `IAlertIncidentRepository`:**
```csharp
// Domain/IRepository/Safety/IAlertIncidentRepository.cs
Task AddAsync(AlertIncident incident, CancellationToken ct = default);
Task SaveChangesAsync(CancellationToken ct = default);
```

**Bước 4 — Implement `AlertIncidentRepository`** trong Infrastructure, đăng ký DI.

**Bước 5 — Tạo `IEmergencyContactRepository`:**
```csharp
// Domain/IRepository/Safety/IEmergencyContactRepository.cs
Task<IReadOnlyList<EmergencyContact>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
Task<EmergencyContact?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task AddAsync(EmergencyContact contact, CancellationToken ct = default);
Task<int> CountByUserIdAsync(Guid userId, CancellationToken ct = default);
// Trả về primary contact hiện tại (nếu có) để unset trước khi set primary mới
Task<EmergencyContact?> GetPrimaryByUserIdAsync(Guid userId, CancellationToken ct = default);
Task SaveChangesAsync(CancellationToken ct = default);
```

**Bước 6 — Implement `EmergencyContactRepository`** trong Infrastructure, đăng ký DI.

**Bước 7 — Migration:** 1 migration `Enable_AlertIncident_And_EmergencyContact` tạo bảng `AlertIncidents` (có unique index `IX_AlertIncidents_DailySafetyRecordId`) và `EmergencyContacts`.

---

### Task 3 — Hangfire Background Jobs

**NuGet cần thêm vào `Beacon.Api`:**
- `Hangfire.AspNetCore`
- `Hangfire.SqlServer`

**Schema Hangfire:** tự tạo khi `UseSqlServerStorage` với `SchemaName = "Hangfire"` — không cần EF migration.

**Job 1: `SafetyReminderJob`** — `Api/Backgroundjobs/SafetyReminderJob.cs`

```
Schedule: every 5 minutes (Cron.MinuteInterval(5))
Logic:
  1. Gọi IDailySafetyRecordRepository.GetPendingNeedingReminderAsync(now)
  2. Với mỗi record:
     a. Gọi IFcmService.SendToUserAsync(record.UserId, title, body)
     b. Gọi record.RecordReminderSent()
  3. Gọi SaveChangesAsync một lần (batch)
```

FCM message: `title = "Nhắc nhở checkin an toàn"`, `body = "Bạn còn {X} phút để checkin hôm nay."`

> **V1 scope:** Chỉ gửi FCM đến device của user (qua `IFcmService`). Chưa gửi qua kênh EmergencyContact (SMS/Email/Telegram) — những kênh này chưa có service implementation. Sẽ thêm ở V2.

**Job 2: `SafetyMissedCheckerJob`** — `Api/Backgroundjobs/SafetyMissedCheckerJob.cs`

```
Schedule: every 5 minutes (Cron.MinuteInterval(5))

Phase 1 — Mark Missed:
  1. Gọi GetPendingPastDeadlineAsync(now)
  2. Với mỗi record: record.MarkMissed()
  3. SaveChangesAsync (DailySafetyRecordRepo)

Phase 2 — Create AlertIncident:
  1. Gọi GetMissedNeedingAlertAsync(now)
  2. Với mỗi record:
     a. var incident = AlertIncident.Create(record.UserId, record.Id, AlertIncidentType.MissedCheckin)
     b. IAlertIncidentRepository.AddAsync(incident)
     c. IFcmService.SendToUserAsync(record.UserId, title, body)
     d. incident.MarkSent()
     e. record.MarkAlerted()
  3. SaveChangesAsync (cả AlertIncidentRepo lẫn DailySafetyRecordRepo)
```

FCM message: `title = "Cảnh báo: Bạn chưa checkin!"`, `body = "Hệ thống đã ghi nhận bạn chưa checkin hôm nay. Vui lòng checkin ngay."`

> **V1 scope:** Chỉ gửi FCM đến device của user. Không gửi cho emergency contacts qua SMS/Email/Telegram/Phone — chưa có service. Sẽ thêm ở V2 khi bật `NotificationDelivery`.

**Timezone trong jobs:**
```csharp
TimeZoneInfo tz;
try { tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
catch { tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
```

**DI Registration:** `Api/Extensions/HangfireExtensions.cs` — gọi từ `Program.cs` qua extension method.

**Acceptance criteria:**
- Job không throw exception ra ngoài — try/catch toàn bộ body, log lỗi, tiếp tục vòng lặp
- Idempotent: `ReminderSentAtUtc IS NULL` guard reminder; unique index + `AlertIncident IS NULL` guard alert
- Hangfire Dashboard: expose tại `/hangfire` chỉ khi `Development` environment

---

### Task 4 — Auto-Resolve khi Checkin (CreateCheckinCommandHandler)

**File cần sửa:** `Application/Features/Checkins/Commands/CreateCheckin/CreateCheckinCommandHandler.cs`

#### Hai nhánh xử lý tách biệt

**Nhánh A — Status = Pending (checkin bình thường):**
```csharp
var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Manual, today, ...);
record.MarkCheckedIn(checkin.CheckedInAtUtc);
// Kết quả: DailySafetyRecord.Status = CheckedIn
```

**Nhánh B — Status = Missed hoặc Alerted (checkin muộn/recovery):**
```csharp
var checkin = Checkin.Create(cmd.UserId, record.Id, CheckinType.Recovery, today, ...);
// Resolve AlertIncident nếu có
if (record.AlertIncident is not null
    && record.AlertIncident.Status != AlertIncidentStatus.Resolved)
{
    record.AlertIncident.Resolve();
}
record.MarkResolved();
// Kết quả: DailySafetyRecord.Status = Resolved
```

> **Tại sao tách hai nhánh:**
> - `MarkCheckedIn` đặt Status = `CheckedIn` (checkin đúng hạn)
> - `MarkResolved` đặt Status = `Resolved` (checkin muộn, sau khi đã bị đánh Missed)
> - Gọi cả hai liên tiếp sẽ ghi đè lên nhau — trạng thái cuối không xác định
> - `CheckinType.Recovery` giúp client phân biệt checkin đúng hạn vs checkin phục hồi

**Cần thêm `.Include(r => r.AlertIncident)` vào `IDailySafetyRecordRepository.GetByUserIdAndDateAsync`** để navigation không null khi Missed/Alerted có incident.

**Acceptance criteria:**
- Status = `CheckedIn` → trả lỗi `ALREADY_CHECKED_IN` (đã có, không đổi)
- Status = `Pending` → MarkCheckedIn, CheckinType = Manual, Status cuối = CheckedIn
- Status = `Missed` hoặc `Alerted` → MarkResolved, CheckinType = Recovery, AlertIncident.Resolve() nếu có, Status cuối = Resolved
- Status = `Resolved` → cho phép tạo checkin bình thường không? → **Không** (Resolved là trạng thái cuối ngày, không cần checkin lại) — trả `ALREADY_CHECKED_IN`

---

### Task 5 — GET /api/v1/checkins/history

**Mục đích:** Trả danh sách **các lần user đã thực sự checkin** (rows trong bảng `Checkins`), sắp xếp mới nhất trước. **Không phải** toàn bộ timeline `DailySafetyRecord` (Pending/Missed/Resolved).

**Endpoint:** `GET /api/v1/checkins/history?cursor={ISO-UTC}&limit={int}`

**Query + Handler:**
```
Application/Features/Checkins/Queries/GetCheckinHistory/
  GetCheckinHistoryQuery.cs        — UserId (từ token), Cursor (DateTimeOffset?), Limit (int, default 20)
  GetCheckinHistoryQueryHandler.cs
```

**DTO Response:**
```csharp
public record CheckinHistoryItemDto
{
    public Guid Id { get; init; }
    public string Type { get; init; }          // "Manual" | "Recovery" | "Emergency"
    public DateOnly CheckinDate { get; init; }
    public DateTimeOffset CheckedInAtUtc { get; init; }
    public string? Note { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public IReadOnlyList<CheckinMediaItemDto> Media { get; init; }
}

public record CheckinMediaItemDto
{
    public Guid MediaObjectId { get; init; }
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
    public string? Caption { get; init; }
}
```

**Validator:**
- `Limit`: 1–100, default 20
- `Cursor`: optional

**Controller action** (thêm vào `CheckinsController`):
```csharp
#region
/// <summary>Lấy lịch sử các lần checkin của user hiện tại</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Trả về danh sách các lần user đã checkin thành công (bảng Checkins),
/// sắp xếp mới nhất trước. Không bao gồm các ngày chưa checkin hay Missed.
///
/// Cursor là <c>CheckedInAtUtc</c> (ISO-8601 UTC) của item cuối trang trước.
/// Bỏ trống cursor để lấy trang đầu.
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: limit ngoài khoảng 1–100.
///
/// Cấu trúc <c>data</c> khi thành công:
/// <code>{ "items": [...], "nextCursor": "ISO-UTC | null", "hasMore": true|false }</code>
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

---

### Task 6 — EmergencyContact CRUD + Set Primary

**Endpoints:**
```
GET    /api/v1/emergency-contacts                    — List contacts của user (chỉ IsDeleted = false)
POST   /api/v1/emergency-contacts                    — Tạo contact mới (max 5 active)
PUT    /api/v1/emergency-contacts/{id:guid}          — Cập nhật contact
DELETE /api/v1/emergency-contacts/{id:guid}          — Soft delete (IsDeleted = true)
PATCH  /api/v1/emergency-contacts/{id:guid}/set-primary — Đặt làm primary contact
```

> **Lưu ý:** `EmergencyContact` kế thừa `SoftDeletableEntity` — phải dùng **soft delete** (`IsDeleted = true`), không phải hard delete. EF query filter tự động loại trừ `IsDeleted = true` khỏi kết quả bình thường.

**Commands/Queries:**
```
Application/Features/Safety/
  Commands/
    CreateEmergencyContact/
      CreateEmergencyContactCommand.cs
      CreateEmergencyContactCommandHandler.cs
    UpdateEmergencyContact/
      UpdateEmergencyContactCommand.cs
      UpdateEmergencyContactCommandHandler.cs
    DeleteEmergencyContact/
      DeleteEmergencyContactCommand.cs
      DeleteEmergencyContactCommandHandler.cs
    SetPrimaryEmergencyContact/
      SetPrimaryEmergencyContactCommand.cs
      SetPrimaryEmergencyContactCommandHandler.cs
  Queries/
    GetEmergencyContacts/
      GetEmergencyContactsQuery.cs
      GetEmergencyContactsQueryHandler.cs
  Validators/Safety/
    CreateEmergencyContactCommandValidator.cs
    UpdateEmergencyContactCommandValidator.cs
  Dtos/
    EmergencyContactDto.cs
    CreateEmergencyContactRequest.cs
    UpdateEmergencyContactRequest.cs
```

**DTO:**
```csharp
public record EmergencyContactDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; }
    public string ContactValue { get; init; }
    public string? Relationship { get; init; }
    public string ChannelType { get; init; }   // "Email" | "Telegram" | "Sms" | "Phone"
    public int PriorityOrder { get; init; }
    public bool IsPrimary { get; init; }
    public bool IsActive { get; init; }
}
```

**Business rules:**

| Rule | Handler xử lý |
|---|---|
| Tối đa 5 active contact / user | `CreateEmergencyContact` — `CountByUserIdAsync` trước khi tạo |
| Owner check trước update/delete/set-primary | Tất cả mutation handlers — `contact.UserId == currentUser.UserId` |
| Đặt primary mới → unset primary cũ | `SetPrimaryEmergencyContact` — lấy `GetPrimaryByUserIdAsync`, gọi `oldPrimary.IsPrimary = false`, sau đó `contact.SetAsPrimary()` |
| V1: verify flow bỏ qua | `IsVerified` mặc định `false`, không gửi xác thực |
| Soft delete, không hard delete | `Deactivate()` + EF filter tự loại trừ |

**SetPrimary handler logic:**
```csharp
// Unset primary hiện tại (nếu có và khác contact đang set)
var currentPrimary = await _repo.GetPrimaryByUserIdAsync(cmd.UserId, ct);
if (currentPrimary is not null && currentPrimary.Id != cmd.ContactId)
    currentPrimary.IsPrimary = false;   // cần method ClearPrimary() hoặc set trực tiếp qua internal setter

// Set primary mới
contact.SetAsPrimary();
await _repo.SaveChangesAsync(ct);
```

> Nếu `EmergencyContact` không có `ClearPrimary()` method, cần thêm vào Domain entity: `public void ClearPrimary() => IsPrimary = false;`

**Validators:**
- `FullName`: required, max 200
- `ContactValue`: required, max 255
- `ChannelType`: phải parse được thành `ContactChannelType` enum
- `Relationship`: optional, max 100
- `PriorityOrder`: 1–99

**Error codes mới — thêm vào `Beacon.Shared/Constants/ErrorCodes.cs` trước khi viết handler:**
```csharp
public const string EMERGENCY_CONTACT_NOT_FOUND = "EMERGENCY_CONTACT_NOT_FOUND";
public const string EMERGENCY_CONTACT_LIMIT_EXCEEDED = "EMERGENCY_CONTACT_LIMIT_EXCEEDED";
public const string EMERGENCY_CONTACT_FORBIDDEN = "EMERGENCY_CONTACT_FORBIDDEN";
```

**Controller:** `Api/Controllers/Safety/EmergencyContactsController.cs`

```csharp
[Route("api/v1/emergency-contacts")]
[Authorize]
public class EmergencyContactsController(IMediator mediator, ICurrentUserService currentUser) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetEmergencyContactsQuery(currentUser.UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmergencyContactRequest req, CancellationToken ct)
        => CreatedResult("api/v1/emergency-contacts",
            await mediator.Send(new CreateEmergencyContactCommand(currentUser.UserId, req), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmergencyContactRequest req, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new UpdateEmergencyContactCommand(currentUser.UserId, id, req), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new DeleteEmergencyContactCommand(currentUser.UserId, id), ct));

    [HttpPatch("{id:guid}/set-primary")]
    public async Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
        => HandleResult(await mediator.Send(
            new SetPrimaryEmergencyContactCommand(currentUser.UserId, id), ct));
}
```

---

## Technical Approach Summary

### Domain Layer
- Không thay đổi entity (đã đủ methods) — ngoại trừ có thể cần `EmergencyContact.ClearPrimary()`
- Thêm `IAlertIncidentRepository`, `IEmergencyContactRepository` vào `Domain/IRepository/Safety/`
- Thêm 3 method vào `IDailySafetyRecordRepository`
- Thêm 1 method vào `ICheckinRepository`

### Application Layer
- Thêm `GetCheckinHistoryQuery/Handler`
- Thêm 4 Commands + 1 Query cho EmergencyContact (bao gồm `SetPrimary`)
- Sửa `CreateCheckinCommandHandler` — tách 2 nhánh Pending vs Missed/Alerted
- Thêm Mappers, DTOs, Validators tương ứng
- Thêm 3 error codes vào `ErrorCodes.cs`

### Infrastructure Layer
- Bỏ `#if false` trong 2 EF Config files
- Uncomment 2 DbSet trong AppDbContext
- Implement `AlertIncidentRepository`, `EmergencyContactRepository`
- Sửa `DailySafetyRecordRepository` (thêm 3 method; thêm `.Include(r => r.AlertIncident)` vào `GetByUserIdAndDateAsync`)
- Sửa `CheckinRepository` (thêm `GetPagedByUserIdAsync`)
- Đăng ký DI cho 2 repo mới
- 1 EF migration: `Enable_AlertIncident_And_EmergencyContact`

### API Layer
- Thêm Hangfire NuGet + `HangfireExtensions.cs`
- Thêm `SafetyReminderJob.cs`, `SafetyMissedCheckerJob.cs`
- Thêm `GetHistory` action vào `CheckinsController`
- Tạo `EmergencyContactsController` (5 endpoints)

---

## Out of Scope (V1)

- EmergencyContact verify flow (SMS/Email xác thực)
- Gửi thông báo qua emergency contacts khi Missed/Alerted (SMS/Email/Telegram/Phone) — V2
- `NotificationDelivery` tracking (liên kết AlertIncident với FCM delivery)
- AlertIncident Acknowledge API (mobile không cần ở V1)
- Manual Emergency alert (`AlertIncidentType.ManualEmergency`)
- Admin view danh sách AlertIncidents
- Rate limiting cho background jobs

---

## Migration Plan

| Thứ tự | Migration | Nội dung |
|---|---|---|
| 1 | `Enable_AlertIncident_And_EmergencyContact` | Bảng `AlertIncidents` (unique index `DailySafetyRecordId`), bảng `EmergencyContacts` |
| 2 | (Hangfire tự tạo) | Schema `Hangfire.*` — không qua EF |

---

## Testing Strategy

### Unit Tests (`Beacon.UnitTests`)

| Test class | Scenarios |
|---|---|
| `SafetyReminderJobTests` | Records cần reminder → gửi FCM + RecordReminderSent; không có records → skip |
| `SafetyMissedCheckerJobTests` | Phase 1: MarkMissed đúng records; Phase 2: tạo AlertIncident + MarkSent + MarkAlerted |
| `CreateCheckinCommandHandlerTests` (bổ sung) | Pending → Manual + CheckedIn; Missed → Recovery + Resolved + AlertIncident.Resolve |
| `CreateEmergencyContactCommandHandlerTests` | Tạo thành công; vượt limit 5 → LIMIT_EXCEEDED |
| `UpdateEmergencyContactCommandHandlerTests` | Owner check → FORBIDDEN; not found → NOT_FOUND; thành công |
| `DeleteEmergencyContactCommandHandlerTests` | Owner check → FORBIDDEN; not found → NOT_FOUND; soft delete thành công |
| `SetPrimaryEmergencyContactCommandHandlerTests` | Unset primary cũ + set primary mới; không có primary cũ → chỉ set mới |
| `GetCheckinHistoryQueryHandlerTests` | Trang đầu không cursor; trang tiếp với cursor; empty result |

### Integration Tests (`Beacon.IntergrationTests`)

| Test class | Scenarios |
|---|---|
| `EmergencyContactsControllerTests` | CRUD + set-primary đầy đủ; 401 khi không có token; 403 khi sai owner; limit exceeded |
| `CheckinsControllerTests` (bổ sung) | `GET /checkins/history` pagination; checkin recovery khi Missed |

---

## Risks & Notes

1. **AlertIncident navigation phải được Include:** `GetByUserIdAndDateAsync` cần `.Include(r => r.AlertIncident)` — thiếu sẽ navigation null dù có incident, tự động resolve sẽ bị bỏ sót.
2. **CheckinType.Recovery vs Manual:** Client dùng Type để phân biệt checkin đúng hạn vs checkin phục hồi, ảnh hưởng UI badge.
3. **EmergencyContact là SoftDeletable:** Query filter tự động loại `IsDeleted = true` — không cần `Where(!x.IsDeleted)` trong repo, nhưng cần verify filter được đăng ký trong `OnModelCreating`.
4. **SetPrimary cần ClearPrimary:** Nếu Domain entity thiếu method, cần thêm trước khi viết handler.
5. **Hangfire + scoped services:** Hangfire tự tạo scope khi resolve job class — đăng ký job class như `Scoped` hoặc `Transient`, không `Singleton`.
6. **Job idempotency:** Cả 2 job đều dùng DB condition làm guard; unique index `AlertIncidents.DailySafetyRecordId` là guard cuối cùng ở tầng DB.
7. **V2 alert cho EmergencyContacts:** Khi bật notification cho emergency contacts, cần bật `NotificationDeliveries` DbSet và review FK constraints với `EmergencyContact`.
