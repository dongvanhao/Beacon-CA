using Beacon.Api.Backgroundjobs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository.Safety;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Safety;

public class SafetyReminderJobTests
{
    private readonly Mock<IDailySafetyRecordRepository> _repoMock = new();
    private readonly Mock<IFcmService> _fcmMock = new();
    private readonly Mock<ILogger<SafetyReminderJob>> _loggerMock = new();
    private readonly SafetyReminderJob _sut;

    public SafetyReminderJobTests()
    {
        _sut = new SafetyReminderJob(_repoMock.Object, _fcmMock.Object, _loggerMock.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRecordsPendingNeedReminder_ShouldSendFcmAndRecordSent()
    {
        // Arrange
        var record = MakePendingRecord();
        _repoMock.Setup(r => r.GetPendingNeedingReminderAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record });
        _fcmMock.Setup(f => f.SendToUserAsync(record.UserId, It.IsAny<string>(), It.IsAny<string>(), null, default))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _fcmMock.Verify(f => f.SendToUserAsync(record.UserId, It.IsAny<string>(), It.IsAny<string>(), null, default), Times.Once);
        record.ReminderSentAtUtc.Should().NotBeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRecordsPending_ShouldNotCallFcm()
    {
        // Arrange
        _repoMock.Setup(r => r.GetPendingNeedingReminderAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord>());

        // Act
        await _sut.ExecuteAsync();

        // Assert
        _fcmMock.Verify(
            f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default),
            Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFcmThrows_ShouldContinueNextRecord_AndNotThrow()
    {
        // Arrange
        var record1 = MakePendingRecord();
        var record2 = MakePendingRecord();

        _repoMock.Setup(r => r.GetPendingNeedingReminderAsync(It.IsAny<DateTimeOffset>(), default))
            .ReturnsAsync(new List<DailySafetyRecord> { record1, record2 });

        _fcmMock.SetupSequence(f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default))
            .ThrowsAsync(new Exception("FCM error"))
            .Returns(Task.CompletedTask);

        // Act
        var act = () => _sut.ExecuteAsync();

        // Assert — không throw ra ngoài
        await act.Should().NotThrowAsync();

        // record2 vẫn xử lý bình thường
        record2.ReminderSentAtUtc.Should().NotBeNull();
        _fcmMock.Verify(
            f => f.SendToUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), null, default),
            Times.Exactly(2));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DailySafetyRecord MakePendingRecord()
    {
        var userId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deadline = DateTime.UtcNow.AddMinutes(20);
        return DailySafetyRecord.Create(userId, today, deadline);
    }
}
