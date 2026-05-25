using Beacon.Api.Backgroundjobs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Notification;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.Enums.Notification;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Notification;
using Beacon.Domain.IRepository.Safety;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Safety;

public class SafetyMissedCheckerJobTests
{
    private readonly Mock<IDailySafetyRecordRepository> _recordRepoMock = new();
    private readonly Mock<IAlertIncidentRepository> _alertRepoMock = new();
    private readonly Mock<IFcmService> _fcmMock = new();
    private readonly Mock<IEmergencyContactRepository> _emergencyContactRepoMock = new();
    private readonly Mock<INotificationDeliveryRepository> _notifDeliveryRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<INotificationService> _notifServiceMock = new();
    private readonly Mock<ILogger<SafetyMissedCheckerJob>> _loggerMock = new();
    private readonly SafetyMissedCheckerJob _sut;

    public SafetyMissedCheckerJobTests()
    {
        _sut = new SafetyMissedCheckerJob(
            _recordRepoMock.Object,
            _alertRepoMock.Object,
            _fcmMock.Object,
            _emergencyContactRepoMock.Object,
            _notifDeliveryRepoMock.Object,
            _userRepoMock.Object,
            _notifServiceMock.Object,
            _loggerMock.Object);

        _recordRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _alertRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _alertRepoMock.Setup(r => r.AddAsync(It.IsAny<AlertIncident>(), default)).Returns(Task.CompletedTask);

        // Default FCM available — individual tests override when testing unavailable path
        _fcmMock.Setup(f => f.IsAvailable).Returns(true);

        // Default: Phase 2 returns empty so Phase 1 tests are not affected
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());

        // Default Phase 3 mocks — return safe empty/null values
        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<EmergencyContact>());
        _notifDeliveryRepoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Returns(Task.CompletedTask);
        _notifDeliveryRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByPhoneAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((User?)null);

        // Default: notification service succeeds
        _notifServiceMock
            .Setup(s => s.CreateAndDeliverAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ─── Phase 1 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Phase1_WhenPendingPastDeadline_ShouldMarkMissed()
    {
        // Arrange
        var record = MakePendingRecord();
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });

        // Act
        await _sut.ExecuteAsync();

        // Assert
        record.Status.Should().Be(SafetyStatus.Missed);
        record.MarkedMissedAtUtc.Should().NotBeNull();
        _recordRepoMock.Verify(r => r.SaveChangesAsync(default), Times.AtLeastOnce);
    }

    // ─── Phase 2 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Phase2_WhenMissedWithDelay_ShouldCreateAlertIncidentAndSendFcm()
    {
        // Arrange
        var record = MakeMissedRecord();
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });
        _fcmMock.Setup(f => f.SendToUserAsync(record.UserId, It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ReturnsAsync(true);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _alertRepoMock.Verify(r => r.AddAsync(It.IsAny<AlertIncident>(), default), Times.Once);
        _fcmMock.Verify(f => f.SendToUserAsync(record.UserId, It.IsAny<string>(), It.IsAny<string>(), null, default), Times.Once);
        record.Status.Should().Be(SafetyStatus.Alerted);
    }

    [Fact]
    public async Task ExecuteAsync_Phase2_WhenFcmFails_ShouldLogError_ContinueNextRecord_AndNotThrow()
    {
        // Arrange
        var record1 = MakeMissedRecord();
        var record2 = MakeMissedRecord();

        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record1, record2 });

        _fcmMock.SetupSequence(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ThrowsAsync(new Exception("FCM error"))
            .ReturnsAsync(true);

        // Act
        var act = () => _sut.ExecuteAsync();

        // Assert
        await act.Should().NotThrowAsync();

        // record2 vẫn được xử lý
        record2.Status.Should().Be(SafetyStatus.Alerted);
    }

    [Fact]
    public async Task ExecuteAsync_Phase2_WhenFcmUnavailable_ShouldMarkIncidentFailed_AndStillMarkAlerted()
    {
        // Arrange
        var record = MakeMissedRecord();
        _fcmMock.Setup(f => f.IsAvailable).Returns(false);
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _fcmMock.Verify(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default), Times.Never);
        _alertRepoMock.Verify(r => r.AddAsync(It.Is<AlertIncident>(i => i.Status == AlertIncidentStatus.Failed), default), Times.Once);
        record.Status.Should().Be(SafetyStatus.Alerted);
    }

    [Fact]
    public async Task ExecuteAsync_Idempotent_WhenAlertAlreadyExists_ShouldNotCreateDuplicate()
    {
        // Arrange — GetMissedNeedingAlertAsync đã lọc những record đã có AlertIncident
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());

        // Act
        await _sut.ExecuteAsync();

        // Assert — không tạo thêm AlertIncident nào
        _alertRepoMock.Verify(r => r.AddAsync(It.IsAny<AlertIncident>(), default), Times.Never);
        _fcmMock.Verify(
            f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default),
            Times.Never);
    }

    // ─── Phase 3 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Phase3_PhoneContactHasBeaconAccount_ShouldCreateInAppNotification_AndMarkDeliveryAsSent()
    {
        // Arrange
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contactUser = User.Create("contactuser", "contact@example.com", "hash", "Contact", "User");
        var contact = EmergencyContact.Create(record.UserId, "Contact Name", "+84912345678", ContactChannelType.Phone);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84912345678", default))
            .ReturnsAsync(contactUser);

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            contactUser.Id, NotificationType.EmergencyAlert,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Once);
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Sent);
        _notifDeliveryRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_SmsContactHasBeaconAccount_ShouldCreateInAppNotification_AndMarkDeliveryAsSent()
    {
        // Arrange
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contactUser = User.Create("smscontact", "sms@example.com", "hash", "Sms", "Contact");
        var contact = EmergencyContact.Create(record.UserId, "Sms Contact", "+84987654321", ContactChannelType.Sms);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84987654321", default))
            .ReturnsAsync(contactUser);

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            contactUser.Id, NotificationType.EmergencyAlert,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Once);
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_EmailContactHasBeaconAccount_ShouldLookupByEmail_CreateNotification_AndMarkDeliveryAsSent()
    {
        // Arrange — Email channel dùng GetByEmailAsync để tìm Beacon account
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contactUser = User.Create("emailcontact", "someone@example.com", "hash", "Email", "Contact");
        var contact = EmergencyContact.Create(record.UserId, "Email Contact", "someone@example.com", ContactChannelType.Email);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        _userRepoMock.Setup(r => r.GetByEmailAsync("someone@example.com", default))
            .ReturnsAsync(contactUser);

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _userRepoMock.Verify(r => r.GetByEmailAsync("someone@example.com", default), Times.Once);
        _userRepoMock.Verify(r => r.GetByPhoneAsync(It.IsAny<string>(), default), Times.Never);
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            contactUser.Id, NotificationType.EmergencyAlert,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Once);
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_EmailContactHasNoBeaconAccount_ShouldMarkDeliveryAsFailed()
    {
        // Arrange — Email contact không có Beacon account → delivery Failed
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contact = EmergencyContact.Create(record.UserId, "Email Contact", "nobody@example.com", ContactChannelType.Email);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        // GetByEmailAsync returns null (default) — no Beacon account

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Failed);
        captured![0].FailureReason.Should().Be("No Beacon account found");
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_ContactHasNoBeaconAccount_ShouldMarkDeliveryAsFailed_WithReason()
    {
        // Arrange
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contact = EmergencyContact.Create(record.UserId, "Unknown", "+84900000000", ContactChannelType.Phone);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        // GetByPhoneAsync returns null (default) — no Beacon account

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Failed);
        captured![0].FailureReason.Should().Be("No Beacon account found");
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_NotificationServiceThrows_ShouldMarkDeliveryAsFailed_AndNotThrow()
    {
        // Arrange — notification service ném exception → delivery Failed, job không crash
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contactUser = User.Create("erruser", "err@example.com", "hash", "Err", "User");
        var contact = EmergencyContact.Create(record.UserId, "Err Contact", "+84911111111", ContactChannelType.Phone);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84911111111", default))
            .ReturnsAsync(contactUser);
        _notifServiceMock
            .Setup(s => s.CreateAndDeliverAsync(
                contactUser.Id, It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), default))
            .ThrowsAsync(new Exception("Service error"));

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        var act = () => _sut.ExecuteAsync();

        // Assert
        await act.Should().NotThrowAsync();
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Failed);
        captured![0].FailureReason.Should().Be("Notification service error");
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_FcmNotAvailable_ShouldSkipPhase3Entirely()
    {
        // Arrange
        var record = MakeMissedRecord();
        _fcmMock.Setup(f => f.IsAvailable).Returns(false);
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });

        var contact = EmergencyContact.Create(record.UserId, "Contact", "+84922222222", ContactChannelType.Phone);
        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });

        // Act
        await _sut.ExecuteAsync();

        // Assert — Phase 3 returns early; no deliveries, no notifications created
        _notifDeliveryRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default), Times.Never);
        _notifDeliveryRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_OneContactThrows_OtherContactsStillProcessed()
    {
        // Arrange
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contactUser2 = User.Create("second", "second@example.com", "hash", "Second", "Contact");
        var contact1 = EmergencyContact.Create(record.UserId, "First", "+84933333333", ContactChannelType.Phone);
        var contact2 = EmergencyContact.Create(record.UserId, "Second", "+84944444444", ContactChannelType.Phone);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact1, contact2 });

        // first contact lookup throws
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84933333333", default))
            .ThrowsAsync(new Exception("DB error"));
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84944444444", default))
            .ReturnsAsync(contactUser2);

        List<NotificationDelivery>? captured = null;
        _notifDeliveryRepoMock
            .Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default))
            .Callback<IEnumerable<NotificationDelivery>, CancellationToken>((d, _) => captured = d.ToList())
            .Returns(Task.CompletedTask);

        // Act
        var act = () => _sut.ExecuteAsync();

        // Assert — no throw, second contact was processed and notification created
        await act.Should().NotThrowAsync();
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            contactUser2.Id, NotificationType.EmergencyAlert,
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Once);
        captured.Should().NotBeNull().And.HaveCount(1);
        captured![0].Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_ContactIsSameAsVictim_ShouldSkipDelivery()
    {
        // Arrange
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        // Force contactUser.Id == record.UserId so the skip-self check triggers
        var sameUser = User.Create("victim", "victim@example.com", "hash", "Victim", "User");
        var idSetter = typeof(Beacon.Domain.Common.BaseEntity).GetProperty("Id")!.GetSetMethod(nonPublic: true)!;
        idSetter.Invoke(sameUser, new object[] { record.UserId });

        var contact = EmergencyContact.Create(record.UserId, "Self", "+84955555555", ContactChannelType.Phone);
        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });
        _userRepoMock.Setup(r => r.GetByPhoneAsync("+84955555555", default))
            .ReturnsAsync(sameUser);

        // Act
        await _sut.ExecuteAsync();

        // Assert — contactUser.Id == victimUserId → skip silently; no delivery, no notification
        _notifDeliveryRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default), Times.Never);
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Phase3_UnsupportedChannelType_ShouldSkipSilently_NoDelivery()
    {
        // Arrange — Telegram không được hỗ trợ lookup Beacon account
        var record = MakeMissedRecord();
        SetupPhase2WithRecord(record);

        var contact = EmergencyContact.Create(record.UserId, "Telegram", "@someuser", ContactChannelType.Telegram);

        _emergencyContactRepoMock.Setup(r => r.GetActiveByUserIdAsync(record.UserId, default))
            .ReturnsAsync(new List<EmergencyContact> { contact });

        // Act
        await _sut.ExecuteAsync();

        // Assert — skip silently, no delivery created
        _notifDeliveryRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<NotificationDelivery>>(), default), Times.Never);
        _userRepoMock.Verify(r => r.GetByPhoneAsync(It.IsAny<string>(), default), Times.Never);
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), default), Times.Never);
        _notifServiceMock.Verify(s => s.CreateAndDeliverAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), default), Times.Never);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DailySafetyRecord MakePendingRecord()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deadline = DateTime.UtcNow.AddMinutes(-10);
        return DailySafetyRecord.Create(userId, today, deadline);
    }

    private static DailySafetyRecord MakeMissedRecord()
    {
        var record = MakePendingRecord();
        record.MarkMissed();
        return record;
    }

    private void SetupPhase2WithRecord(DailySafetyRecord record)
    {
        _recordRepoMock.Setup(r => r.GetPendingPastDeadlineAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });
        // FCM for Phase 2 victim — returns default (false), record still gets MarkAlerted
    }
}
