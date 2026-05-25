# Plan: Phase 3 SafetyMissedCheckerJob — FCM to Emergency Contacts

**Module**: Safety + Notification (cross-cutting)
**Spec**: [`docs/specs/safety-missed-checker-phase3.md`](../specs/safety-missed-checker-phase3.md)
**Phạm vi**: 3 slices — không có API endpoint mới, không có migration mới

---

## Context nhanh

| Đã có sẵn | Cần làm |
|---|---|
| Entity `NotificationDelivery`, `EmergencyContact`, `AlertIncident` | Uncomment `DbSet<NotificationDelivery>` |
| Bảng `NotificationDelivery` (số ít) trong DB | Fix `NotificationDeliveryConfiguration` (namespace + table name) |
| `IFcmService.SendToUserAsync`, `IEmergencyContactRepository` | Thêm `GetByEmailAsync` vào `IUserRepository` |
| `NotificationChannel.Push`, `NotificationStatus.Sent/Failed` | Thêm `NotificationKind.EmergencyAlert` (=4) |
| Phase 1 & 2 trong `SafetyMissedCheckerJob` | Tạo `INotificationDeliveryRepository` + impl + DI |
| | Implement Phase 3 trong job |

**Không có** MediatR Command/Query/Handler, Validator, Mapper, Controller — đây là background job.

---

## Phase 1: Foundation — EF Unlock + Enum

### Slice 1.1: Bật lại NotificationDelivery trong EF pipeline

**Mục tiêu**: Sau slice này `dotnet build` phải pass — EF biết về `NotificationDelivery`.

**Không có unit test** — acceptance criteria là build 0 error.

---

#### Bước 1 — Thêm `EmergencyAlert` vào `NotificationKind`

**File**: `src/Beacon.Domain/Enums/Notification/NotificationKind.cs`

Hiện tại: `Reminder=1, Alert=2, FollowUp=3`

```csharp
public enum NotificationKind
{
    Reminder = 1,
    Alert = 2,
    FollowUp = 3,
    EmergencyAlert = 4
}
```

> Không cần migration — enum stored as `int`, giá trị `4` chưa dùng.

---

#### Bước 2 — Uncomment `DbSet<NotificationDelivery>`

**File**: `src/Beacon.Infrashtructure/Presistence/AppDbContext.cs` — dòng 59

```csharp
// Từ:
// public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

// Thành:
public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
```

---

#### Bước 3 — Fix `NotificationDeliveryConfiguration`

**File**: `src/Beacon.Infrashtructure/Presistence/Configuration/Notification/NotificationDeliveryConfiguration.cs`

3 thay đổi đồng thời:

1. **Xóa** `#if false` ở đầu file và `#endif` ở cuối file
2. **Sửa namespace** — dòng `namespace`:
   ```csharp
   // Từ:
   namespace Beacon.Infrastructure.Persistence.Configurations.Notifications;
   // Thành:
   namespace Beacon.Infrashtructure.Presistence.Configuration.Notification;
   ```
3. **Sửa table name** — trong `Configure()`:
   ```csharp
   // Từ:
   builder.ToTable("NotificationDeliveries");
   // Thành:
   builder.ToTable("NotificationDelivery");  // tên bảng thực tế trong DB
   ```

---

#### ✅ Checkpoint Slice 1.1

```bash
dotnet build
```

- [ ] 0 error, 0 warning
- [ ] EF model snapshot không báo lỗi (không cần migrate vì bảng đã tồn tại)

**Dependencies**: Không có — đây là slice đầu tiên.

---

## Phase 2: Domain Contracts — Interfaces + Implementations

### Slice 2.1: `IUserRepository.GetByEmailAsync` + `INotificationDeliveryRepository`

**Mục tiêu**: Cung cấp đủ repository contracts cho Phase 3 cần dùng.

**Không có unit test** — acceptance criteria là build 0 error + DI resolve không throw.

---

#### Bước 1 — Thêm `GetByEmailAsync` vào `IUserRepository`

**File**: `src/Beacon.Domain/IRepository/IUserRepository.cs`

Thêm sau `GetByPhoneAsync`:

```csharp
Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
```

---

#### Bước 2 — Implement `GetByEmailAsync` trong `UserRepository`

**File**: `src/Beacon.Infrashtructure/Repository/Identity/UserRepository.cs`

Pattern giống `GetByPhoneAsync` — normalize email về lowercase:

```csharp
public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    => context.Users.FirstOrDefaultAsync(
        u => u.Email == email.ToLowerInvariant(), ct);
```

---

#### Bước 3 — Tạo `INotificationDeliveryRepository`

**File mới**: `src/Beacon.Domain/IRepository/Notification/INotificationDeliveryRepository.cs`

```csharp
using Beacon.Domain.Entities.Notification;

namespace Beacon.Domain.IRepository.Notification;

public interface INotificationDeliveryRepository
{
    Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

> `SaveChangesAsync` được expose vì job cần kiểm soát timing flush — không rải trong từng Add call.

---

#### Bước 4 — Tạo `NotificationDeliveryRepository`

**File mới**: `src/Beacon.Infrashtructure/Repository/Notification/NotificationDeliveryRepository.cs`

```csharp
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.IRepository.Notification;
using Beacon.Infrashtructure.Presistence;

namespace Beacon.Infrashtructure.Repository.Notification;

public class NotificationDeliveryRepository(AppDbContext db) : INotificationDeliveryRepository
{
    public Task AddAsync(NotificationDelivery delivery, CancellationToken ct = default)
    {
        db.NotificationDeliveries.Add(delivery);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<NotificationDelivery> deliveries, CancellationToken ct = default)
    {
        db.NotificationDeliveries.AddRange(deliveries);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
```

---

#### Bước 5 — Đăng ký DI

**File**: `src/Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs`

Thêm cùng nhóm với các Safety repositories:

```csharp
services.AddScoped<INotificationDeliveryRepository, NotificationDeliveryRepository>();
```

Thêm `using` tương ứng:
```csharp
using Beacon.Domain.IRepository.Notification;
using Beacon.Infrashtructure.Repository.Notification;
```

---

#### ✅ Checkpoint Slice 2.1

```bash
dotnet build
```

- [ ] 0 error, 0 warning
- [ ] `IUserRepository` có đủ `GetByEmailAsync`
- [ ] `INotificationDeliveryRepository` đã đăng ký Scoped

**Dependencies**: Slice 1.1 phải complete trước (DbSet cần trước khi Repository compile).

---

## Phase 3: Core Business Logic — Phase 3 Job + Unit Tests (TDD)

### Slice 3.1: SafetyMissedCheckerJob Phase 3 + 7 Unit Tests

**Mục tiêu**: Implement Phase 3 end-to-end theo TDD — test RED trước, rồi implement GREEN.

---

#### Bước 1 — Viết 7 Unit Tests (RED)

**File**: `src/tests/Beacon.UnitTests/Safety/SafetyMissedCheckerJobTests.cs`

**Constructor** — thêm 3 mock mới và re-wire `_sut`:

```csharp
private readonly Mock<IEmergencyContactRepository> _emergencyContactRepoMock = new();
private readonly Mock<INotificationDeliveryRepository> _notifDeliveryRepoMock = new();
private readonly Mock<IUserRepository> _userRepoMock = new();

// Trong constructor — cập nhật _sut:
_sut = new SafetyMissedCheckerJob(
    _recordRepoMock.Object,
    _alertRepoMock.Object,
    _fcmMock.Object,
    _emergencyContactRepoMock.Object,
    _notifDeliveryRepoMock.Object,
    _userRepoMock.Object,
    _loggerMock.Object);

// Default setups cho Phase 3:
_notifDeliveryRepoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
    .Returns(Task.CompletedTask);
_notifDeliveryRepoMock.Setup(r => r.SaveChangesAsync(default))
    .Returns(Task.CompletedTask);
_emergencyContactRepoMock.Setup(r => r.GetByUserIdAsync(It.IsAny<Guid>(), default))
    .ReturnsAsync(new List<EmergencyContact>());
```

**7 test cases**:

```csharp
// Test 1: Email contact có Beacon account + device token → Sent
[Fact]
public async Task ExecuteAsync_Phase3_EmailContactHasBeaconAccount_ShouldSendFcm_AndMarkDeliveryAsSent()

// Test 2: Phone contact có Beacon account + device token → Sent
[Fact]
public async Task ExecuteAsync_Phase3_PhoneContactHasBeaconAccount_ShouldSendFcm_AndMarkDeliveryAsSent()

// Test 3: Contact không có Beacon account → Failed
[Fact]
public async Task ExecuteAsync_Phase3_ContactHasNoBeaconAccount_ShouldMarkDeliveryAsFailed_WithReason()

// Test 4: Contact có Beacon account nhưng không có device token → Failed
[Fact]
public async Task ExecuteAsync_Phase3_ContactHasNoDeviceToken_ShouldMarkDeliveryAsFailed_WithReason()

// Test 5: FCM not available → skip toàn bộ Phase 3, không tạo delivery nào
[Fact]
public async Task ExecuteAsync_Phase3_FcmNotAvailable_ShouldSkipPhase3Entirely()

// Test 6: 1 contact throw exception → catch + tiếp tục contact kế
[Fact]
public async Task ExecuteAsync_Phase3_OneContactThrows_OtherContactsStillProcessed()

// Test 7: Contact lookup ra userId == victim → skip, không tạo delivery
[Fact]
public async Task ExecuteAsync_Phase3_ContactIsSameAsVictim_ShouldSkipDelivery()
```

> Ở bước này các test đều **FAIL** (compile error hoặc runtime fail) — đây là trạng thái RED bình thường.

---

#### Bước 2 — Cập nhật `SafetyMissedCheckerJob`

**File**: `src/Beacon.Api/Backgroundjobs/SafetyMissedCheckerJob.cs`

**Constructor** — inject 3 deps mới:

```csharp
public class SafetyMissedCheckerJob(
    IDailySafetyRecordRepository recordRepo,
    IAlertIncidentRepository alertRepo,
    IFcmService fcm,
    IEmergencyContactRepository emergencyContactRepo,   // mới
    INotificationDeliveryRepository notifDeliveryRepo,  // mới
    IUserRepository userRepo,                           // mới
    ILogger<SafetyMissedCheckerJob> logger)
```

**Phase 3** — thêm sau `await alertRepo.SaveChangesAsync()` và `await recordRepo.SaveChangesAsync()` của Phase 2:

```csharp
// Phase 3 — Notify emergency contacts
await ExecutePhase3Async(alertedPairs, ct);
```

> `alertedPairs` là `List<(AlertIncident incident, DailySafetyRecord record)>` được tích lũy trong loop Phase 2.

**Private method `ExecutePhase3Async`**:

```csharp
private async Task ExecutePhase3Async(
    List<(AlertIncident incident, DailySafetyRecord record)> pairs,
    CancellationToken ct)
{
    if (!fcm.IsAvailable)
    {
        logger.LogWarning("FCM unavailable — Phase 3 skipped entirely");
        return;
    }

    try
    {
        foreach (var (incident, record) in pairs)
        {
            var contacts = await emergencyContactRepo.GetByUserIdAsync(record.UserId, ct);
            var activeContacts = contacts.Where(c => c.IsActive).ToList();
            var deliveries = new List<NotificationDelivery>();

            // Lấy tên nạn nhân để dùng trong FCM payload
            var victim = await userRepo.GetByIdAsync(record.UserId, ct);
            var victimName = victim?.FullName ?? victim?.Username ?? record.UserId.ToString();

            var title = $"Cảnh báo khẩn: {victimName} chưa checkin!";
            var body = $"{victimName} chưa thực hiện checkin an toàn hôm nay. Vui lòng liên hệ xác nhận.";
            var data = new Dictionary<string, string>
            {
                ["type"] = "emergency_alert",
                ["alertIncidentId"] = incident.Id.ToString(),
                ["victimUserId"] = record.UserId.ToString(),
                ["victimName"] = victimName
            };

            foreach (var contact in activeContacts)
            {
                try
                {
                    await ProcessContactAsync(
                        contact, incident, record.UserId,
                        title, body, data, deliveries, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Phase 3 failed for ContactId={ContactId} UserId={UserId}",
                        contact.Id, record.UserId);
                }
            }

            if (deliveries.Count > 0)
            {
                await notifDeliveryRepo.AddRangeAsync(deliveries, ct);
                await notifDeliveryRepo.SaveChangesAsync(ct);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SafetyMissedCheckerJob Phase 3 failed entirely — Phase 1 & 2 already committed");
        // KHÔNG throw — Phase 1 & 2 đã committed
    }
}

private async Task ProcessContactAsync(
    EmergencyContact contact,
    AlertIncident incident,
    Guid victimUserId,
    string title, string body,
    Dictionary<string, string> data,
    List<NotificationDelivery> deliveries,
    CancellationToken ct)
{
    // Lookup Beacon account theo ChannelType
    User? contactUser = contact.ChannelType switch
    {
        ContactChannelType.Email => await userRepo.GetByEmailAsync(contact.ContactValue, ct),
        ContactChannelType.Phone => await userRepo.GetByPhoneAsync(contact.ContactValue, ct),
        ContactChannelType.Sms   => await userRepo.GetByPhoneAsync(contact.ContactValue, ct),
        _                        => null  // Telegram và các loại khác → skip ngầm
    };

    // ChannelType không hỗ trợ lookup → skip ngầm (không tạo delivery)
    if (contact.ChannelType is not (ContactChannelType.Email
                                 or ContactChannelType.Phone
                                 or ContactChannelType.Sms))
        return;

    var delivery = NotificationDelivery.Create(
        userId: contactUser?.Id ?? victimUserId,  // fallback khi không tìm thấy user
        kind: NotificationKind.EmergencyAlert,
        channel: NotificationChannel.Push,
        recipient: contact.ContactValue,
        title: title,
        body: body,
        alertIncidentId: incident.Id,
        emergencyContactId: contact.Id);

    if (contactUser is null)
    {
        delivery.MarkFailed("No Beacon account found");
        deliveries.Add(delivery);
        return;
    }

    if (contactUser.Id == victimUserId)
        return;  // contact trùng chính victim → skip, không tạo delivery

    // Tạo lại delivery với đúng userId của contact
    var contactDelivery = NotificationDelivery.Create(
        userId: contactUser.Id,
        kind: NotificationKind.EmergencyAlert,
        channel: NotificationChannel.Push,
        recipient: contact.ContactValue,
        title: title,
        body: body,
        alertIncidentId: incident.Id,
        emergencyContactId: contact.Id);

    var sent = await fcm.SendToUserAsync(contactUser.Id, title, body, data, ct);
    if (sent)
        contactDelivery.MarkSent();
    else
        contactDelivery.MarkFailed("No active device tokens");

    deliveries.Add(contactDelivery);
}
```

> **Lưu ý**: `alertedPairs` cần được tích lũy trong Phase 2 loop — sửa Phase 2 để collect pairs trước khi gọi Phase 3.

**Sửa nhỏ trong Phase 2** — collect pairs:

```csharp
// Thêm ở đầu Phase 2:
var alertedPairs = new List<(AlertIncident incident, DailySafetyRecord record)>();

// Trong foreach loop, sau record.MarkAlerted():
alertedPairs.Add((incident, record));

// Sau alertRepo.SaveChangesAsync() + recordRepo.SaveChangesAsync():
await ExecutePhase3Async(alertedPairs, default);
```

---

#### Bước 3 — Chạy Tests (GREEN)

```bash
dotnet test src/tests/Beacon.UnitTests --filter "SafetyMissedCheckerJobTests"
```

Tất cả 7 test mới + 5 test cũ phải **GREEN**.

---

#### ✅ Checkpoint Final

```bash
dotnet build
dotnet test src/tests/Beacon.UnitTests
```

- [ ] `dotnet build` — 0 error, 0 warning
- [ ] Tất cả 12 tests trong `SafetyMissedCheckerJobTests` GREEN (5 cũ + 7 mới)
- [ ] Không có exception khi inject job qua DI

**Dependencies**: Slice 1.1 + Slice 2.1 phải complete trước.

---

## Tổng kết — File Changes

| File | Loại thay đổi | Slice |
|---|---|---|
| `Beacon.Domain/Enums/Notification/NotificationKind.cs` | Thêm `EmergencyAlert = 4` | 1.1 |
| `Beacon.Infrashtructure/Presistence/AppDbContext.cs` | Uncomment `DbSet<NotificationDelivery>` | 1.1 |
| `Beacon.Infrashtructure/Presistence/Configuration/Notification/NotificationDeliveryConfiguration.cs` | Remove `#if false`, fix namespace, fix table name | 1.1 |
| `Beacon.Domain/IRepository/IUserRepository.cs` | Add `GetByEmailAsync` | 2.1 |
| `Beacon.Infrashtructure/Repository/Identity/UserRepository.cs` | Implement `GetByEmailAsync` | 2.1 |
| `Beacon.Domain/IRepository/Notification/INotificationDeliveryRepository.cs` | **Tạo mới** | 2.1 |
| `Beacon.Infrashtructure/Repository/Notification/NotificationDeliveryRepository.cs` | **Tạo mới** | 2.1 |
| `Beacon.Infrashtructure/Dependencyinjection/InfrastructureServiceExtensions.cs` | Register Scoped | 2.1 |
| `Beacon.Api/Backgroundjobs/SafetyMissedCheckerJob.cs` | Inject 3 deps + Phase 3 | 3.1 |
| `tests/Beacon.UnitTests/Safety/SafetyMissedCheckerJobTests.cs` | Add 7 tests + update constructor | 3.1 |

**Tổng**: 10 files, 0 migration mới, 0 API endpoint mới.
