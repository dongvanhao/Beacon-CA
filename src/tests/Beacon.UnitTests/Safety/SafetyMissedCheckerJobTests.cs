using Beacon.Api.Backgroundjobs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Safety;
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
    private readonly Mock<ILogger<SafetyMissedCheckerJob>> _loggerMock = new();
    private readonly SafetyMissedCheckerJob _sut;

    public SafetyMissedCheckerJobTests()
    {
        _sut = new SafetyMissedCheckerJob(
            _recordRepoMock.Object,
            _alertRepoMock.Object,
            _fcmMock.Object,
            _loggerMock.Object);

        _recordRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _alertRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _alertRepoMock.Setup(r => r.AddAsync(It.IsAny<AlertIncident>(), default)).Returns(Task.CompletedTask);

        // Default FCM available — individual tests override when testing unavailable path
        _fcmMock.Setup(f => f.IsAvailable).Returns(true);

        // Default: Phase 2 trả empty để không ảnh hưởng Phase 1 test
        _recordRepoMock.Setup(r => r.GetMissedNeedingAlertAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());
    }

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
            .Returns(Task.CompletedTask);

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
            .Returns(Task.CompletedTask);

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
        // Nếu unique index đã block ở DB, query sẽ không trả ra record đó nữa
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
}
