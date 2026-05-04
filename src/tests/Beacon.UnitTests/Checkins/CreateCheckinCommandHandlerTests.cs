using Beacon.Application.Features.Checkins.Commands.CreateCheckin;
using Beacon.Application.Features.Checkins.Dtos;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Entities.Safety;
using Beacon.Domain.Entities.Setting;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.Enums.Safety;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Domain.IRepository.Safety;
using Beacon.Domain.IRepository.Settings;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Checkins;

public class CreateCheckinCommandHandlerTests
{
    private readonly Mock<IDailySafetyRecordRepository> _dailySafetyRecordRepo = new();
    private readonly Mock<ISafetySettingRepository> _safetySettingRepo = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepo = new();
    private readonly Mock<ICheckinRepository> _checkinRepo = new();
    private readonly CheckinMapper _mapper = new();
    private readonly CreateCheckinCommandHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();

    private static readonly CreateCheckinRequest BasicRequest = new(
        Note: "Tôi ổn",
        Latitude: 10.762622m,
        Longitude: 106.660172m,
        MediaId: null);

    public CreateCheckinCommandHandlerTests()
    {
        _handler = new CreateCheckinCommandHandler(
            _dailySafetyRecordRepo.Object,
            _safetySettingRepo.Object,
            _mediaRepo.Object,
            _checkinRepo.Object,
            _mapper);

        _checkinRepo.Setup(r => r.AddAsync(It.IsAny<Checkin>(), default)).Returns(Task.CompletedTask);
        _checkinRepo.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        _dailySafetyRecordRepo.Setup(r => r.AddAsync(It.IsAny<DailySafetyRecord>(), default)).Returns(Task.CompletedTask);
    }

    // ─── Happy path: no media ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidRequest_NoMedia_CreatesCheckinAndReturnsSuccess()
    {
        // Arrange
        var record = MakePendingRecord();
        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync(record);

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, BasicRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MediaObjectId.Should().BeNull();
        result.Value.Type.Should().Be(nameof(CheckinType.Manual));
        _checkinRepo.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    // ─── Happy path: with media ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithMediaId_AttachesMediaAndReturnsDto()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var record = MakePendingRecord();
        var media = CreateFakeMedia();

        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync(record);
        _mediaRepo.Setup(r => r.GetByIdAsync(mediaId, default)).ReturnsAsync(media);

        var request = BasicRequest with { MediaId = mediaId };

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MediaObjectId.Should().Be(mediaId);
    }

    // ─── No DailySafetyRecord yet — creates with SafetySetting deadline ─────

    [Fact]
    public async Task Handle_WhenNoRecord_AndHasSafetySetting_CreatesRecordWithDeadline()
    {
        // Arrange
        var setting = SafetySetting.CreateDefault(UserId, new TimeOnly(21, 0));
        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync((DailySafetyRecord?)null);
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(UserId, default)).ReturnsAsync(setting);

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, BasicRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dailySafetyRecordRepo.Verify(r => r.AddAsync(It.IsAny<DailySafetyRecord>(), default), Times.Once);
    }

    // ─── No DailySafetyRecord, no SafetySetting — uses fallback deadline ────

    [Fact]
    public async Task Handle_WhenNoRecord_AndNoSafetySetting_UsesDefaultDeadline()
    {
        // Arrange
        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync((DailySafetyRecord?)null);
        _safetySettingRepo.Setup(r => r.GetByUserIdAsync(UserId, default)).ReturnsAsync((SafetySetting?)null);

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, BasicRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dailySafetyRecordRepo.Verify(r => r.AddAsync(It.IsAny<DailySafetyRecord>(), default), Times.Once);
    }

    // ─── Already checked in today ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAlreadyCheckedIn_ReturnsConflict()
    {
        // Arrange
        var record = MakeCheckedInRecord();
        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync(record);

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, BasicRequest), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Safety.ALREADY_CHECKED_IN);
        _checkinRepo.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    // ─── Media not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMediaNotFound_ReturnsNotFound()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var record = MakePendingRecord();
        _dailySafetyRecordRepo.Setup(r => r.GetByUserIdAndDateAsync(UserId, TodayVn, default))
            .ReturnsAsync(record);
        _mediaRepo.Setup(r => r.GetByIdAsync(mediaId, default)).ReturnsAsync((MediaObject?)null);

        var request = BasicRequest with { MediaId = mediaId };

        // Act
        var result = await _handler.Handle(new CreateCheckinCommand(UserId, request), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Storage.MEDIA_NOT_FOUND);
        _checkinRepo.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static readonly TimeZoneInfo VnTz =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    private static DateOnly TodayVn =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTz));

    private static DailySafetyRecord MakePendingRecord()
    {
        var deadline = DateTime.UtcNow.Date.AddHours(23).AddMinutes(59);
        return DailySafetyRecord.Create(UserId, TodayVn, deadline);
    }

    private static DailySafetyRecord MakeCheckedInRecord()
    {
        var record = MakePendingRecord();
        record.MarkCheckedIn(DateTime.UtcNow);
        return record;
    }

    private static MediaObject CreateFakeMedia()
        => MediaObject.Create(
            bucketName: "test",
            objectKey: "test/file.jpg",
            originalFileName: "file.jpg",
            contentType: "image/jpeg",
            fileSizeBytes: 1024,
            mediaType: Beacon.Domain.Enums.MediaType.Image);
}
