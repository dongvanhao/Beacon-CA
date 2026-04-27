using Beacon.Application.Features.Settings.Dtos;
using Beacon.Application.Features.Settings.Queries.GetSafetySetting;
using Beacon.Application.Mappings.Settings;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.IRepository.Settings;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Settings;

public class GetSafetySettingHandlerTests
{
    private readonly Mock<ISafetySettingRepository> _repo = new();
    private readonly SafetySettingMapper _mapper = new();
    private readonly GetSafetySettingQueryHandler _handler;

    public GetSafetySettingHandlerTests()
    {
        _handler = new GetSafetySettingQueryHandler(_repo.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenSettingExists_ReturnsActualData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var setting = SafetySetting.CreateDefault(userId, new TimeOnly(20, 0));
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync(setting);

        // Act
        var result = await _handler.Handle(new GetSafetySettingQuery(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DailyDeadlineLocalTime.Should().Be("20:00");
    }

    [Fact]
    public async Task Handle_WhenSettingNotFound_ReturnsDefaultValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repo.Setup(r => r.GetByUserIdAsync(userId, default)).ReturnsAsync((SafetySetting?)null);

        // Act
        var result = await _handler.Handle(new GetSafetySettingQuery(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DailyDeadlineLocalTime.Should().Be("23:59");
        result.Value.GracePeriodMinutes.Should().Be(15);
        result.Value.ReminderBeforeMinutes.Should().Be(30);
        result.Value.AutoAlertDelayMinutes.Should().Be(15);
        result.Value.IsMonitoringEnabled.Should().BeTrue();
        result.Value.IsAutoAlertEnabled.Should().BeTrue();
    }
}
