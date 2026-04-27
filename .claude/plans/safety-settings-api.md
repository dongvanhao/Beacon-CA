# Plan: SafetySettings API

**Module**: Settings (sub-module của Safety)
**Branch**: `feature/safety-settings`
**Phạm vi**: 3 phases, 5 slices
**Routes**: `GET /api/v1/safety/settings` · `PATCH /api/v1/safety/settings`

---

## Quan sát hiện trạng

| File | Trạng thái |
|---|---|
| `Domain/Entities/Settings/SafetySetting.cs` | ✅ Có — thiếu `UpdateSettings()` method |
| `Domain/Entities/Identity/User.cs` | ⚠️ Thiếu navigation property `SafetySetting?` |
| `Infrashtructure/Presistence/Configuration/Settings/SafetySettingConfiguration.cs` | ⚠️ Wrapped `#if false` |
| `Infrashtructure/Presistence/AppDbContext.cs` | ⚠️ `DbSet<SafetySetting>` bị comment |
| `Infrashtructure/Presistence/Configuration/Identity/UserConfiguration.cs` | ⚠️ Relationship `HasOne(SafetySetting)` bị comment |
| `Shared/Constants/ErrorCodes.cs` | ⚠️ Thiếu `Settings` class |
| `Domain/IRepository/Settings/ISafetySettingRepository.cs` | ❌ Chưa có |
| `Application/Features/Settings/` | ❌ Chưa có |
| `Api/Controllers/Settings/SafetySettingsController.cs` | ❌ Chưa có |
| Migration `AddSafetySettings` | ❌ Chưa có |

## Business Rules (đã chốt)

- SafetySetting là quan hệ **1-1 với User** (cascade delete khi user bị xóa)
- **Lazy upsert**: không tạo sẵn khi register — GET trả default nếu chưa có record, PATCH tạo mới nếu chưa có
- `DailyDeadlineLocalTime` lưu dạng `TimeOnly`, truyền qua API dạng string `"HH:mm"`
- Tất cả settings đều có giá trị mặc định hợp lý (20:00 / 15p / 30p / 15p / true / true)

---

## Phase 1: Foundation

### Slice 1.1 — Domain + Error Codes

> Không cần test — pure domain changes và constants.

**Việc cần làm:**

**1. Thêm navigation property vào User entity**

File: `src/Beacon.Domain/Entities/Identity/User.cs`

```csharp
// Thêm vào cuối Relations section
public SafetySetting? SafetySetting { get; private set; }
```

**2. Thêm `UpdateSettings()` vào SafetySetting entity**

File: `src/Beacon.Domain/Entities/Settings/SafetySetting.cs`

```csharp
public void UpdateSettings(
    TimeOnly dailyDeadlineLocalTime,
    int gracePeriodMinutes,
    int reminderBeforeMinutes,
    int autoAlertDelayMinutes,
    bool isMonitoringEnabled,
    bool isAutoAlertEnabled)
{
    DailyDeadlineLocalTime = dailyDeadlineLocalTime;
    GracePeriodMinutes     = gracePeriodMinutes;
    ReminderBeforeMinutes  = reminderBeforeMinutes;
    AutoAlertDelayMinutes  = autoAlertDelayMinutes;
    IsMonitoringEnabled    = isMonitoringEnabled;
    IsAutoAlertEnabled     = isAutoAlertEnabled;
}
```

**3. Thêm error codes**

File: `src/Beacon.Shared/Constants/ErrorCodes.cs`

```csharp
public static class Settings
{
    public const string SAFETY_SETTING_NOT_FOUND = "SAFETY_SETTING_NOT_FOUND";
}
```

**Dependencies**: Không có.

---

### Slice 1.2 — Infrastructure Activation + Migration

> Không cần test — schema change.

**Việc cần làm:**

**1. Bỏ `#if false` trong SafetySettingConfiguration**

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Settings/SafetySettingConfiguration.cs`

Xóa dòng `#if false` và `#endif`, giữ nguyên nội dung bên trong.

**2. Uncomment DbSet + apply configuration trong AppDbContext**

File: `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs`

```csharp
// Bỏ comment:
public DbSet<SafetySetting> SafetySettings => Set<SafetySetting>();

// Thêm vào OnModelCreating:
modelBuilder.ApplyConfiguration(new SafetySettingConfiguration());
```

**3. Uncomment relationship trong UserConfiguration**

File: `src/Beacon.Infrashtructure/Presistence/Configuration/Identity/UserConfiguration.cs`

```csharp
// Bỏ comment block:
builder.HasOne(x => x.SafetySetting)
    .WithOne(x => x.User)
    .HasForeignKey<SafetySetting>(x => x.UserId)
    .OnDelete(DeleteBehavior.Cascade);
```

**4. Tạo migration**

```bash
dotnet ef migrations add AddSafetySettings \
  --project src/Beacon.Infrashtructure \
  --startup-project src/Beacon.Api
```

> ⚠️ Review migration file sau khi tạo: xác nhận chỉ có `CREATE TABLE SafetySettings` + FK, không có DROP bất ngờ.

**Dependencies**: Slice 1.1.

---

## ✅ Checkpoint: Foundation Complete

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] Migration file đã review — không có DROP ngoài ý muốn
- [ ] `dotnet ef database update` — apply thành công, bảng `SafetySettings` xuất hiện trong DB

---

## Phase 2: Core Use Cases (TDD)

### Slice 2.1 — Repository Interface + Implementation

> Không cần test riêng — sẽ được cover bởi integration test của Slice 2.2 và 2.3.

**Việc cần làm:**

**1. Repository interface**

File: `src/Beacon.Domain/IRepository/Settings/ISafetySettingRepository.cs`

```csharp
public interface ISafetySettingRepository
{
    Task<SafetySetting?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(SafetySetting setting, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

**2. Repository implementation**

File: `src/Beacon.Infrashtructure/Repository/Settings/SafetySettingRepository.cs`

```csharp
public class SafetySettingRepository(AppDbContext db) : ISafetySettingRepository
{
    public Task<SafetySetting?> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => db.SafetySettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task AddAsync(SafetySetting setting, CancellationToken ct)
        => await db.SafetySettings.AddAsync(setting, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => db.SaveChangesAsync(ct);
}
```

**3. Đăng ký DI**

File: `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

```csharp
services.AddScoped<ISafetySettingRepository, SafetySettingRepository>();
```

**Dependencies**: Slice 1.2.

---

### Slice 2.2 — GET Safety Settings (Query)

**Route**: `GET /api/v1/safety/settings`
**Auth**: `[Authorize]`
**Logic**: Có record → trả actual data. Chưa có → trả default values (`IsDefault: true`), **không** persist.

---

**Bước 1 — Viết Unit Test (RED) trước**

File: `tests/Beacon.UnitTests/Settings/GetSafetySettingHandlerTests.cs`

Cases cần cover:
- `Handle_WhenSettingExists_ReturnsActualData` — verify `IsDefault = false`, đúng values
- `Handle_WhenSettingNotFound_ReturnsDefaultValues` — verify `IsDefault = true`, `Result.IsSuccess = true` (không phải NotFound error)

---

**Bước 2 — DTO**

File: `src/Beacon.Application/Features/Settings/Dtos/SafetySettingDto.cs`

```csharp
public record SafetySettingDto(
    string DailyDeadlineLocalTime,
    int GracePeriodMinutes,
    int ReminderBeforeMinutes,
    int AutoAlertDelayMinutes,
    bool IsMonitoringEnabled,
    bool IsAutoAlertEnabled,
    bool IsDefault
);
```

---

**Bước 3 — Mapper**

File: `src/Beacon.Application/Mappings/Settings/SafetySettingMapper.cs`

```csharp
public sealed class SafetySettingMapper
{
    public SafetySettingDto ToDto(SafetySetting s) => new(
        DailyDeadlineLocalTime : s.DailyDeadlineLocalTime.ToString("HH:mm"),
        GracePeriodMinutes     : s.GracePeriodMinutes,
        ReminderBeforeMinutes  : s.ReminderBeforeMinutes,
        AutoAlertDelayMinutes  : s.AutoAlertDelayMinutes,
        IsMonitoringEnabled    : s.IsMonitoringEnabled,
        IsAutoAlertEnabled     : s.IsAutoAlertEnabled,
        IsDefault              : false);

    public SafetySettingDto ToDefaultDto() => new(
        DailyDeadlineLocalTime : "20:00",
        GracePeriodMinutes     : 15,
        ReminderBeforeMinutes  : 30,
        AutoAlertDelayMinutes  : 15,
        IsMonitoringEnabled    : true,
        IsAutoAlertEnabled     : true,
        IsDefault              : true);
}
```

Đăng ký Singleton trong `src/Beacon.Application/DependencyInjection/ApplicationServiceExtensions.cs`:

```csharp
services.AddSingleton<SafetySettingMapper>();
```

---

**Bước 4 — Query + Handler**

File: `src/Beacon.Application/Features/Settings/Queries/GetSafetySetting/GetSafetySettingQuery.cs`

```csharp
public record GetSafetySettingQuery(Guid UserId) : IRequest<Result<SafetySettingDto>>;
```

File: `src/Beacon.Application/Features/Settings/Queries/GetSafetySetting/GetSafetySettingQueryHandler.cs`

```csharp
public async Task<Result<SafetySettingDto>> Handle(GetSafetySettingQuery q, CancellationToken ct)
{
    var setting = await _repo.GetByUserIdAsync(q.UserId, ct);
    return Result.Success(setting is null
        ? _mapper.ToDefaultDto()
        : _mapper.ToDto(setting));
}
```

---

**Bước 5 — Controller**

File: `src/Beacon.Api/Controllers/Settings/SafetySettingsController.cs`

```csharp
[Route("api/v1/safety/settings")]
[Authorize]
public class SafetySettingsController(IMediator mediator, ICurrentUserService currentUser)
    : BaseController
{
    #region
    /// <summary>Lấy cài đặt an toàn của người dùng hiện tại.</summary>
    /// <remarks>
    /// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
    ///
    /// Các giá trị <c>code</c>:
    /// - <c>null</c>: Thành công.
    /// - <c>VALIDATION_ERROR</c>: Token không hợp lệ.
    ///
    /// Cấu trúc <c>data</c> khi thành công:
    /// <code>
    /// {
    ///   "dailyDeadlineLocalTime": "HH:mm",
    ///   "gracePeriodMinutes": 15,
    ///   "reminderBeforeMinutes": 30,
    ///   "autoAlertDelayMinutes": 15,
    ///   "isMonitoringEnabled": true,
    ///   "isAutoAlertEnabled": true,
    ///   "isDefault": true|false
    /// }
    /// </code>
    ///
    /// Format: <c>{ success, message, code, data, errors }</c>
    /// </remarks>
    #endregion
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => HandleResult(await mediator.Send(new GetSafetySettingQuery(currentUser.UserId), ct));
}
```

---

**Bước 6 — Unit Test GREEN + Integration Test**

File: `tests/Beacon.IntergrationTests/Settings/SafetySettingsControllerTests.cs`

Cases cần cover:
- `GET_ReturnsDefaultValues_WhenNoSettingExists` — `200 OK`, `IsDefault = true`
- `GET_ReturnsActualData_WhenSettingExists` — `200 OK`, `IsDefault = false`
- `GET_Returns401_WhenNoToken` — `401 Unauthorized`

**Dependencies**: Slice 2.1.

---

### Slice 2.3 — PATCH Safety Settings (Command — Upsert)

**Route**: `PATCH /api/v1/safety/settings`
**Auth**: `[Authorize]`
**Logic**: Chưa có record → `CreateDefault()` + `UpdateSettings()` + `AddAsync`. Đã có → `UpdateSettings()` + `SaveChangesAsync`.

---

**Bước 1 — Viết Unit Test (RED) trước**

File: `tests/Beacon.UnitTests/Settings/UpdateSafetySettingHandlerTests.cs`

Cases cần cover:
- `Handle_WhenNoExistingRecord_CreatesNewAndReturnsSuccess` — verify `AddAsync` được gọi, `Result.IsSuccess = true`
- `Handle_WhenRecordExists_UpdatesAndReturnsSuccess` — verify `SaveChangesAsync` được gọi, values đã thay đổi
- `Handle_WhenDeadlineFormatInvalid_ValidatorRejects` — verify validator throw trước khi handler chạy

---

**Bước 2 — Request DTO + Command**

File: `src/Beacon.Application/Features/Settings/Dtos/UpdateSafetySettingRequest.cs`

```csharp
public record UpdateSafetySettingRequest(
    string DailyDeadlineLocalTime,
    int GracePeriodMinutes,
    int ReminderBeforeMinutes,
    int AutoAlertDelayMinutes,
    bool IsMonitoringEnabled,
    bool IsAutoAlertEnabled
);
```

File: `src/Beacon.Application/Features/Settings/Commands/UpdateSafetySetting/UpdateSafetySettingCommand.cs`

```csharp
public record UpdateSafetySettingCommand(Guid UserId, UpdateSafetySettingRequest Request)
    : IRequest<Result<SafetySettingDto>>;
```

---

**Bước 3 — Handler (upsert)**

File: `src/Beacon.Application/Features/Settings/Commands/UpdateSafetySetting/UpdateSafetySettingCommandHandler.cs`

```csharp
public async Task<Result<SafetySettingDto>> Handle(UpdateSafetySettingCommand cmd, CancellationToken ct)
{
    var req      = cmd.Request;
    var deadline = TimeOnly.Parse(req.DailyDeadlineLocalTime); // validator đã đảm bảo format

    var setting = await _repo.GetByUserIdAsync(cmd.UserId, ct);

    if (setting is null)
    {
        setting = SafetySetting.CreateDefault(cmd.UserId, deadline);
        setting.UpdateSettings(deadline, req.GracePeriodMinutes, req.ReminderBeforeMinutes,
            req.AutoAlertDelayMinutes, req.IsMonitoringEnabled, req.IsAutoAlertEnabled);
        await _repo.AddAsync(setting, ct);
    }
    else
    {
        setting.UpdateSettings(deadline, req.GracePeriodMinutes, req.ReminderBeforeMinutes,
            req.AutoAlertDelayMinutes, req.IsMonitoringEnabled, req.IsAutoAlertEnabled);
    }

    await _repo.SaveChangesAsync(ct);
    return Result.Success(_mapper.ToDto(setting));
}
```

---

**Bước 4 — Validator**

File: `src/Beacon.Application/Features/Settings/Validators/UpdateSafetySettingCommandValidator.cs`

```csharp
public class UpdateSafetySettingCommandValidator : AbstractValidator<UpdateSafetySettingCommand>
{
    public UpdateSafetySettingCommandValidator()
    {
        RuleFor(x => x.Request.DailyDeadlineLocalTime)
            .NotEmpty().WithMessage("Giờ deadline không được để trống.")
            .Matches(@"^\d{2}:\d{2}$").WithMessage("Giờ deadline phải theo định dạng HH:mm.")
            .Must(t => TimeOnly.TryParse(t, out _)).WithMessage("Giờ deadline không hợp lệ.");

        RuleFor(x => x.Request.GracePeriodMinutes)
            .InclusiveBetween(0, 120)
            .WithMessage("Thời gian ân hạn phải từ 0 đến 120 phút.");

        RuleFor(x => x.Request.ReminderBeforeMinutes)
            .InclusiveBetween(0, 120)
            .WithMessage("Thời gian nhắc nhở phải từ 0 đến 120 phút.");

        RuleFor(x => x.Request.AutoAlertDelayMinutes)
            .InclusiveBetween(0, 60)
            .WithMessage("Thời gian chờ cảnh báo phải từ 0 đến 60 phút.");
    }
}
```

---

**Bước 5 — Thêm endpoint PATCH vào Controller**

```csharp
#region
/// <summary>Cập nhật cài đặt an toàn của người dùng hiện tại.</summary>
/// <remarks>
/// Yêu cầu: <c>Authorization: Bearer &lt;token&gt;</c>
///
/// Tạo mới nếu chưa có record, cập nhật nếu đã tồn tại (upsert).
///
/// Các giá trị <c>code</c>:
/// - <c>null</c>: Thành công.
/// - <c>VALIDATION_ERROR</c>: Dữ liệu không hợp lệ (format HH:mm, giá trị ngoài range).
///
/// Cấu trúc <c>data</c> khi thành công: xem GET /api/v1/safety/settings.
///
/// Format: <c>{ success, message, code, data, errors }</c>
/// </remarks>
#endregion
[HttpPatch]
public async Task<IActionResult> Update(
    [FromBody] UpdateSafetySettingRequest request,
    CancellationToken ct)
    => HandleResult(await mediator.Send(
        new UpdateSafetySettingCommand(currentUser.UserId, request), ct));
```

---

**Bước 6 — Unit Test GREEN + Integration Test**

Cases cần cover:
- `PATCH_CreatesNewSetting_WhenNoneExists` — `200 OK`, `IsDefault = false`
- `PATCH_UpdatesExistingSetting` — `200 OK`, values khớp request
- `PATCH_Returns400_WhenDeadlineFormatInvalid` — `400`, `errors` có message tiếng Việt
- `PATCH_Returns400_WhenGracePeriodOutOfRange` — `400`
- `PATCH_Returns401_WhenNoToken` — `401`

**Dependencies**: Slice 2.2 (dùng chung mapper, DTO, repository).

---

## ✅ Final Checkpoint

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] `dotnet test tests/Beacon.UnitTests` — GREEN (tất cả cases)
- [ ] `dotnet test tests/Beacon.IntergrationTests` — GREEN
- [ ] Swagger: `GET` + `PATCH /api/v1/safety/settings` hiển thị đúng, auth required
- [ ] XML doc đầy đủ trên cả 2 endpoints

---

## Thứ tự implement

```
Slice 1.1          Slice 1.2        Slice 2.1       Slice 2.2        Slice 2.3
Domain + EC    →   Migration    →   Repository  →   GET (TDD)    →   PATCH (TDD)
(no test)          (no test)        (no test)        Unit + Intg      Unit + Intg
```

## Files sẽ tạo mới

| File | Layer |
|---|---|
| `Domain/IRepository/Settings/ISafetySettingRepository.cs` | Domain |
| `Application/Features/Settings/Dtos/SafetySettingDto.cs` | Application |
| `Application/Features/Settings/Dtos/UpdateSafetySettingRequest.cs` | Application |
| `Application/Features/Settings/Queries/GetSafetySetting/GetSafetySettingQuery.cs` | Application |
| `Application/Features/Settings/Queries/GetSafetySetting/GetSafetySettingQueryHandler.cs` | Application |
| `Application/Features/Settings/Commands/UpdateSafetySetting/UpdateSafetySettingCommand.cs` | Application |
| `Application/Features/Settings/Commands/UpdateSafetySetting/UpdateSafetySettingCommandHandler.cs` | Application |
| `Application/Features/Settings/Validators/UpdateSafetySettingCommandValidator.cs` | Application |
| `Application/Mappings/Settings/SafetySettingMapper.cs` | Application |
| `Infrashtructure/Repository/Settings/SafetySettingRepository.cs` | Infrastructure |
| `Api/Controllers/Settings/SafetySettingsController.cs` | API |
| `tests/Beacon.UnitTests/Settings/GetSafetySettingHandlerTests.cs` | Tests |
| `tests/Beacon.UnitTests/Settings/UpdateSafetySettingHandlerTests.cs` | Tests |
| `tests/Beacon.IntergrationTests/Settings/SafetySettingsControllerTests.cs` | Tests |
