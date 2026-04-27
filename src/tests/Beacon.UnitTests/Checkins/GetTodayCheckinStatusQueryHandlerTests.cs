using Beacon.Application.Features.Checkins.Queries.GetTodayCheckinStatus;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Checkins;

public class GetTodayCheckinStatusQueryHandlerTests
{
    private readonly Mock<IDailySafetyRecordRepository> _dailySafetyRecordRepo = new();
    private readonly Mock<ISafetySettingRepository> _safetySettingRepo = new();
    private readonly CheckinStatusMapper _mapper = new();
    private readonly GetTodayCheckinStatusQueryHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();

    public GetTodayCheckinStatusQueryHandlerTests()
    {
        _handler = new GetTodayCheckinStatusQueryHandler(
            _dailySafetyRecordRepo.Object,
            _safetySettingRepo.Object,
            _mapper);
    }

    // ─── Pending: có record, còn thời gian ──────────────────────────────────

    [Fact]
    public async Task Handle_WhenRecordExistsAndBeforeDeadline_ReturnsPending()
    {
        var deadline = DateTime.UtcNow.AddHours(2);
        var record = MakePendingRecord(deadline);
        SetupRecord(record);

        var result = await _handler.Handle(new GetTodayCheckinStatusQuery(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCheckedIn.Should().BeFalse();
        result.Value.Status.Should().Be("Pending");
        result.Value.RemainingSeconds.Should().BeGreaterThanOrEqualTo(0);
        result.Value.CheckedInAtUtc.Should().BeNull();
    }

    // ─── Pending: không có record, còn thời gian ────────────────────────────

    [Fact]
    public async Task Handle_WhenNoRecordAndBeforeDeadline_ReturnsPending()
    {
        SetupNoRecord();
        var setting = SafetySetting.CreateDefault(UserId, new TimeOnly(23, 59));
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(UserId, default)).ReturnsAsync(setting);

        var result = await _handler.Handle(new GetTodayCheckinStatusQuery(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCheckedIn.Should().BeFalse();
        result.Value.Status.Should().BeOneOf("Pending", "Overdue");
        result.Value.RemainingSeconds.Should().NotBeNull();
    }

    // ─── CheckedIn ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAlreadyCheckedIn_ReturnsCheckedIn()
    {
        var checkedInAt = DateTime.UtcNow.AddHours(-1);
        var record = MakeCheckedInRecord(checkedInAt);
        SetupRecord(record);

        var result = await _handler.Handle(new GetTodayCheckinStatusQuery(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCheckedIn.Should().BeTrue();
        result.Value.Status.Should().Be("CheckedIn");
        result.Value.RemainingSeconds.Should().BeNull();
        result.Value.CheckedInAtUtc.Should().Be(checkedInAt);
    }

    // ─── Overdue: quá deadline, chưa checkin ────────────────────────────────

    [Fact]
    public async Task Handle_WhenPastDeadlineAndNotCheckedIn_ReturnsOverdue()
    {
        var deadline = DateTime.UtcNow.AddHours(-1);
        var record = MakePendingRecord(deadline);
        SetupRecord(record);

        var result = await _handler.Handle(new GetTodayCheckinStatusQuery(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasCheckedIn.Should().BeFalse();
        result.Value.Status.Should().Be("Overdue");
        result.Value.RemainingSeconds.Should().BeLessThan(0);
    }

    // ─── Default deadline khi không có SafetySetting ─────────────────────────

    [Fact]
    public async Task Handle_WhenNoSafetySetting_UsesDefaultDeadline2359()
    {
        SetupNoRecord();
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(UserId, default)).ReturnsAsync((SafetySetting?)null);

        var result = await _handler.Handle(new GetTodayCheckinStatusQuery(UserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DeadlineAtUtc.TimeOfDay.Should().Be(new TimeSpan(23, 59, 0));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static DailySafetyRecord MakePendingRecord(DateTime deadline)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return DailySafetyRecord.Create(UserId, today, deadline);
    }

    private static DailySafetyRecord MakeCheckedInRecord(DateTime checkedInAt)
    {
        var record = MakePendingRecord(checkedInAt.AddHours(1));
        record.MarkCheckedIn(checkedInAt);
        return record;
    }

    private void SetupRecord(DailySafetyRecord record)
        => _dailySafetyRecordRepo
            .Setup(r => r.GetByUserIdAndDateAsync(UserId, DateOnly.FromDateTime(DateTime.UtcNow), default))
            .ReturnsAsync(record);

    private void SetupNoRecord()
        => _dailySafetyRecordRepo
            .Setup(r => r.GetByUserIdAndDateAsync(UserId, DateOnly.FromDateTime(DateTime.UtcNow), default))
            .ReturnsAsync((DailySafetyRecord?)null);
}
