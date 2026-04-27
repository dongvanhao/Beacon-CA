using Beacon.Application.Features.Settings.Commands.UpdateSafetySetting;
using Beacon.Application.Features.Settings.Dtos;
using Beacon.Application.Mappings.Settings;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.IRepository.Settings;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Settings;

public class UpdateSafetySettingHandlerTests
{
    private readonly Mock<ISafetySettingRepository> _repo = new();
    private readonly SafetySettingMapper _mapper = new();
    private readonly UpdateSafetySettingCommandHandler _handler;

    private static readonly UpdateSafetySettingRequest ValidRequest = new()
    {
        DailyDeadlineLocalTime = "21:00",
        GracePeriodMinutes     = 20,
        ReminderBeforeMinutes  = 45,
        AutoAlertDelayMinutes  = 10,
        IsMonitoringEnabled    = true,
        IsAutoAlertEnabled     = false
    };

    public UpdateSafetySettingHandlerTests()
    {
        _handler = new UpdateSafetySettingCommandHandler(_repo.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenNoExistingRecord_CreatesNewAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((SafetySetting?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<SafetySetting>(), default)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new UpdateSafetySettingCommand(userId, ValidRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DailyDeadlineLocalTime.Should().Be("21:00");
        result.Value.GracePeriodMinutes.Should().Be(20);
        _repo.Verify(r => r.AddAsync(It.IsAny<SafetySetting>(), default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRecordExists_UpdatesAndReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existing = SafetySetting.CreateDefault(userId, new TimeOnly(20, 0));
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(existing);
        _repo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new UpdateSafetySettingCommand(userId, ValidRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DailyDeadlineLocalTime.Should().Be("21:00");
        result.Value.GracePeriodMinutes.Should().Be(20);
        result.Value.IsAutoAlertEnabled.Should().BeFalse();
        _repo.Verify(r => r.AddAsync(It.IsAny<SafetySetting>(), default), Times.Never);
        _repo.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
