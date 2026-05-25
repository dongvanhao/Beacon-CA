# Feature: Phase 3 SafetyMissedCheckerJob — FCM Alert to Emergency Contacts

## Objective

Sau khi Phase 2 tạo `AlertIncident` và gửi FCM cho chính user bị miss checkin, Phase 3 gửi tiếp FCM push notification tới thiết bị của các người liên hệ khẩn cấp (`EmergencyContact`) có tài khoản Beacon, đồng thời ghi `NotificationDelivery` để audit toàn bộ quá trình.

## Target Users

- **System job** (không phải user-facing): `SafetyMissedCheckerJob` chạy bởi Hangfire scheduler.
- Người nhận FCM: các `EmergencyContact` của user bị miss checkin — nếu họ có tài khoản Beacon.

## Core Features & Use Cases

### UC1 — Gửi FCM tới emergency contact có tài khoản Beacon
**AC:**
- Lấy tất cả `EmergencyContact` của `record.UserId` có `IsActive = true` và `IsDeleted = false`.
- Với mỗi contact, lookup Beacon account theo `ChannelType`:
  - `Email` → `IUserRepository.GetByEmailAsync(contact.ContactValue)`
  - `Phone | Sms` → `IUserRepository.GetByPhoneAsync(contact.ContactValue)`
  - Các `ChannelType` khác (không hỗ trợ lookup Beacon account) → skip ngầm, không tạo `NotificationDelivery`
- Nếu lookup ra `userId == record.UserId` → bỏ qua (không tự gửi cho chính user bị miss).
- Nếu không tìm thấy Beacon account → tạo `NotificationDelivery` với `Status = Failed`, `FailureReason = "No Beacon account found"`.
- Nếu `IFcmService.SendToUserAsync` trả `false` (không có device token) → `Status = Failed`, `FailureReason = "No active device tokens"`.
- Nếu gửi thành công (`SendToUserAsync` trả `true`) → `NotificationDelivery.MarkSent()`.

### UC2 — FCM payload chuẩn tới emergency contact
**AC:**
```
Title: "Cảnh báo khẩn: {victimName} chưa checkin!"
Body:  "{victimName} chưa thực hiện checkin an toàn hôm nay. Vui lòng liên hệ xác nhận."
Data:  {
  "type": "emergency_alert",
  "alertIncidentId": "<guid>",
  "victimUserId": "<guid>",
  "victimName": "<string>"
}
```
`victimName` lấy từ `User.FullName` (hoặc `User.Username` nếu `FullName` null).

### UC3 — Resilience & isolation
**AC:**
- 1 contact lỗi (exception) không dừng các contact còn lại — try/catch per-contact.
- Phase 3 lỗi toàn bộ → log error, **không throw** (Phase 1 & 2 đã committed).
- `IFcmService.IsAvailable == false` → bỏ qua toàn bộ Phase 3, log warning, không tạo bất kỳ `NotificationDelivery` nào.

### UC4 — Audit trail
**AC:**
- Mỗi contact được xử lý (kể cả failed) đều tạo `NotificationDelivery` với:
  - `UserId` = contact Beacon account id
  - `AlertIncidentId` = incident.Id
  - `EmergencyContactId` = contact.Id
  - `Kind = NotificationKind.EmergencyAlert`
  - `Channel = NotificationChannel.Push`
  - `Recipient` = contact.ContactValue
  - `Title`, `Body` = FCM payload
- Tất cả `NotificationDelivery` trong Phase 3 lưu qua `INotificationDeliveryRepository.SaveChangesAsync` sau vòng lặp per-incident.

## Out of Scope

- Gửi SMS/Email thật sự — chỉ FCM Push notification tới Beacon app.
- Retry logic cho contact failed — lần này chỉ ghi Failed, không tự retry.
- Contact có `ChannelType` không hỗ trợ lookup (vd `Telegram`) — skip ngầm, không trong scope.
- Tạo migration mới — bảng `NotificationDelivery` đã tồn tại trong DB.
- Thay đổi Phase 1 hoặc Phase 2.
- UI/API endpoint mới.

---

## Technical Approach (Clean Architecture)

### Infrastructure Layer — Thay đổi cần thiết (theo thứ tự)

#### 1. `AppDbContext` — bỏ comment `DbSet<NotificationDelivery>`
**File:** `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs:59`

```csharp
// Từ:
// public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
// Thành:
public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
```

#### 2. `NotificationDeliveryConfiguration` — bật lại và sửa
**File:** `src/Beacon.Infrashtructure/Presistence/Configuration/Notification/NotificationDeliveryConfiguration.cs`

- Xóa `#if false` / `#endif`
- Sửa namespace: `Beacon.Infrastructure.Persistence.Configurations.Notifications` → `Beacon.Infrashtructure.Presistence.Configuration.Notification`
- Sửa `ToTable("NotificationDeliveries")` → `ToTable("NotificationDelivery")` (tên bảng thực tế trong DB)

#### 3. `IUserRepository` — thêm method mới
**File:** `src/Beacon.Domain/IRepository/IUserRepository.cs`

```csharp
Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
```

Implement trong `UserRepository` (tương tự `GetByPhoneAsync`):
```csharp
public Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
```

#### 4. `INotificationDeliveryRepository` — tạo mới
**File:** `src/Beacon.Domain/IRepository/Notification/INotificationDeliveryRepository.cs`

```csharp
namespace Beacon.Domain.IRepository.Notification;

public interface INotificationDeliveryRepository
{
    Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

#### 5. `NotificationDeliveryRepository` — tạo mới + đăng ký
**File:** `src/Beacon.Infrashtructure/Repository/Notification/NotificationDeliveryRepository.cs`

```csharp
public class NotificationDeliveryRepository(AppDbContext db) : INotificationDeliveryRepository
{
    public Task AddAsync(NotificationDelivery delivery, CancellationToken ct)
    {
        db.NotificationDeliveries.Add(delivery);
        return Task.CompletedTask;
    }
    public Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct)
    {
        db.NotificationDeliveries.AddRange(deliveries);
        return Task.CompletedTask;
    }
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
```

Đăng ký `Scoped` trong `InfrastructureServiceExtensions.cs`:
```csharp
services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
```

### API Layer — `SafetyMissedCheckerJob`

**File:** `src/Beacon.Api/Backgroundjobs/SafetyMissedCheckerJob.cs`

Inject thêm:
- `IEmergencyContactRepository` — lấy danh sách contact của user
- `INotificationDeliveryRepository` — lưu audit
- `IUserRepository` — lookup Beacon account từ email/phone
- `ILogger<SafetyMissedCheckerJob>` — đã có

Phase 3 được gọi **sau Phase 2** (cùng loop per-incident hoặc loop riêng sau `alertRepo.SaveChangesAsync()`). Recommended: thêm inner method `ExecutePhase3Async(IReadOnlyList<(AlertIncident incident, DailySafetyRecord record)> pairs, ...)` để giữ `ExecuteAsync` readable.

**Lookup logic:**
```
ContactChannelType.Email → userRepo.GetByEmailAsync(contact.ContactValue)
ContactChannelType.Phone → userRepo.GetByPhoneAsync(contact.ContactValue)
ContactChannelType.Sms   → userRepo.GetByPhoneAsync(contact.ContactValue)
// các ChannelType khác → skip ngầm (continue, không tạo delivery)
```

**Per-contact flow:**
```
1. if !fcm.IsAvailable → skip all Phase 3, log warning
2. contacts = await emergencyContactRepo.GetByUserIdAsync(record.UserId) — filter IsActive
3. foreach contact:
   a. resolve channelType → contactUser (nullable)
   b. if contactUser == null → delivery.MarkFailed("No Beacon account found")
   c. if contactUser.Id == record.UserId → skip
   d. else → fcm.SendToUserAsync(contactUser.Id, title, body, data)
      - sent == true  → delivery.MarkSent()
      - sent == false → delivery.MarkFailed("No active device tokens")
4. await notifDeliveryRepo.AddRangeAsync(deliveries)
5. await notifDeliveryRepo.SaveChangesAsync()
```

### Domain Layer — Enums cần kiểm tra

Xác nhận các enum values tồn tại:
- `NotificationKind.EmergencyAlert` trong `Beacon.Domain.Enums.Notification.NotificationKind`
- `NotificationChannel.Push` trong `Beacon.Domain.Enums.Notification.NotificationChannel`
- `NotificationStatus.Sent`, `NotificationStatus.Failed` trong `NotificationStatus`
- `ContactChannelType.Email`, `Phone`, `Sms` trong `Beacon.Domain.Enums.ContactChannelType` (chỉ 3 loại này có đường lookup Beacon account)

Nếu thiếu → thêm vào enum tương ứng (không cần migration vì enum stored as int).

---

## Testing Strategy

### Unit Tests — thêm vào `SafetyMissedCheckerJobTests.cs`

File: `src/tests/Beacon.UnitTests/Safety/SafetyMissedCheckerJobTests.cs`

Constructor của test class cần inject thêm 3 mock:
- `Mock<IEmergencyContactRepository>`
- `Mock<INotificationDeliveryRepository>`
- `Mock<IUserRepository>`

| Test | Setup | Assert |
|---|---|---|
| `Phase3_ContactWithEmail_HasBeaconAccount_ShouldSendFcm_AndMarkDeliveryAsSent` | contact ChannelType=Email, userRepo trả User, fcm.SendToUserAsync → true | `NotificationDelivery.Status == Sent`, AddRangeAsync called |
| `Phase3_ContactWithPhone_HasBeaconAccount_ShouldSendFcm_AndMarkDeliveryAsSent` | contact ChannelType=Phone, userRepo.GetByPhoneAsync trả User, fcm → true | Sent |
| `Phase3_ContactHasNoBeaconAccount_ShouldMarkDeliveryAsFailed` | userRepo trả null | `Status == Failed`, `FailureReason = "No Beacon account found"` |
| `Phase3_ContactHasNoDeviceToken_ShouldMarkDeliveryAsFailed` | fcm.SendToUserAsync → false | `Status == Failed`, `FailureReason = "No active device tokens"` |
| `Phase3_FcmNotAvailable_ShouldSkipPhase3Entirely` | `fcm.IsAvailable = false` | `AddRangeAsync` never called, `AddAsync` never called |
| `Phase3_OneContactThrows_OtherContactsStillProcessed` | 2 contacts, fcm throws on first, returns true on second | second contact Sent, không throw |
| `Phase3_ContactUserIsSameAsVictim_ShouldSkip` | lookup trả User với Id == record.UserId | không tạo NotificationDelivery cho contact đó |
| `Phase3_UnsupportedChannelType_ShouldSkipSilently` | contact ChannelType=Telegram | không tạo NotificationDelivery, không throw |

---

## Architectural Constraints

### Always Do
- `SafetyMissedCheckerJob` ở `Api/Backgroundjobs/` — được phép inject bất kỳ service nào qua DI.
- Repository được inject vào job trực tiếp (không qua MediatR Command) — đây là background job pattern của Beacon.
- `NotificationDelivery` tạo qua factory method `NotificationDelivery.Create(...)` — không new trực tiếp.
- `MarkSent()` / `MarkFailed()` thay vì set property trực tiếp.
- Error code string nằm trong `FailureReason` (plain text, không phải ErrorCode constant) — đây là internal audit, không phải API error.

### Never Do
- Không throw exception từ Phase 3 — phase này là best-effort, Phase 1 & 2 đã committed.
- Không dùng `GetByEmailAsync` từ `ExistsByEmailAsync` (trả bool) — phải thêm method mới trả `User?`.
- Không đặt business logic trong `NotificationDeliveryRepository`.
- Không tạo migration mới — bảng `NotificationDelivery` (số ít) đã tồn tại.

---

## File Checklist

| File | Action |
|---|---|
| `Beacon.Infrashtructure/Presistence/AppDbContext.cs:59` | Uncomment `DbSet<NotificationDelivery>` |
| `Beacon.Infrashtructure/Presistence/Configuration/Notification/NotificationDeliveryConfiguration.cs` | Remove `#if false`, fix namespace, fix table name |
| `Beacon.Domain/IRepository/IUserRepository.cs` | Add `GetByEmailAsync` |
| `Beacon.Infrashtructure/Repository/Identity/UserRepository.cs` | Implement `GetByEmailAsync` |
| `Beacon.Domain/IRepository/Notification/INotificationDeliveryRepository.cs` | Create new |
| `Beacon.Infrashtructure/Repository/Notification/NotificationDeliveryRepository.cs` | Create new |
| `Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs` | Register `INotificationDeliveryRepository` Scoped |
| `Beacon.Api/Backgroundjobs/SafetyMissedCheckerJob.cs` | Inject 3 new deps, add Phase 3 |
| `Beacon.Domain/Enums/Notification/NotificationKind.cs` (if exists) | Verify/add `EmergencyAlert` |
| `Beacon.Domain/Enums/Notification/NotificationChannel.cs` (if exists) | Verify/add `Push` |
| `tests/Beacon.UnitTests/Safety/SafetyMissedCheckerJobTests.cs` | Add 7 Phase 3 tests |
