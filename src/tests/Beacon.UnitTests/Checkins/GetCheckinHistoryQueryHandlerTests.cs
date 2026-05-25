using Beacon.Application.Features.Checkins.Queries.GetCheckinHistory;
using Beacon.Application.Mappings.Checkins;
using Beacon.Domain.Entities.Checkins;
using Beacon.Domain.Enums.Checkins;
using Beacon.Domain.IRepository.Checkins;
using Beacon.Shared.Common.Pagination;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Checkins;

public class GetCheckinHistoryQueryHandlerTests
{
    private readonly Mock<ICheckinRepository> _repoMock = new();
    private readonly CheckinHistoryMapper _mapper = new();
    private readonly GetCheckinHistoryQueryHandler _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public GetCheckinHistoryQueryHandlerTests()
    {
        _sut = new GetCheckinHistoryQueryHandler(_repoMock.Object, _mapper);
    }

    [Fact]
    public async Task Handle_WhenNoCursor_ShouldReturnFirstPage()
    {
        // Arrange
        var checkins = new List<Checkin>
        {
            MakeCheckin(DateTime.UtcNow.AddHours(-1)),
            MakeCheckin(DateTime.UtcNow.AddHours(-2))
        };

        _repoMock.Setup(r => r.GetPagedByUserIdAsync(UserId, null, 20, default))
            .ReturnsAsync(new CursorPagedResult<Checkin>
            {
                Data = checkins,
                Meta = new CursorMeta { NextCursor = null, Limit = 20, HasMore = false }
            });

        // Act
        var result = await _sut.Handle(new GetCheckinHistoryQuery(UserId, null, 20), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(2);
        result.Value.Meta.HasMore.Should().BeFalse();
        result.Value.Meta.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCursorProvided_ShouldReturnItemsOlderThanCursor()
    {
        // Arrange
        var cursor = DateTimeOffset.UtcNow.AddHours(-3);
        var checkins = new List<Checkin>
        {
            MakeCheckin(DateTime.UtcNow.AddHours(-4)),
            MakeCheckin(DateTime.UtcNow.AddHours(-5))
        };

        _repoMock.Setup(r => r.GetPagedByUserIdAsync(UserId, cursor, 20, default))
            .ReturnsAsync(new CursorPagedResult<Checkin>
            {
                Data = checkins,
                Meta = new CursorMeta
                {
                    NextCursor = checkins.Last().CheckedInAtUtc,
                    Limit = 20,
                    HasMore = true
                }
            });

        // Act
        var result = await _sut.Handle(new GetCheckinHistoryQuery(UserId, cursor, 20), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(2);
        result.Value.Meta.HasMore.Should().BeTrue();
        result.Value.Meta.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenNoCheckins_ShouldReturnEmptyItems()
    {
        // Arrange
        _repoMock.Setup(r => r.GetPagedByUserIdAsync(UserId, null, 20, default))
            .ReturnsAsync(new CursorPagedResult<Checkin>
            {
                Data = new List<Checkin>(),
                Meta = new CursorMeta { NextCursor = null, Limit = 20, HasMore = false }
            });

        // Act
        var result = await _sut.Handle(new GetCheckinHistoryQuery(UserId, null, 20), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().BeEmpty();
        result.Value.Meta.HasMore.Should().BeFalse();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static Checkin MakeCheckin(DateTime checkedInAtUtc)
    {
        var checkin = Checkin.Create(
            userId: UserId,
            dailySafetyRecordId: Guid.NewGuid(),
            type: CheckinType.Manual,
            checkinDate: DateOnly.FromDateTime(checkedInAtUtc));

        // Ghi đè CheckedInAtUtc để test cursor (thời điểm checkin)
        typeof(Checkin)
            .GetProperty(nameof(Checkin.CheckedInAtUtc))!
            .SetValue(checkin, checkedInAtUtc);

        return checkin;
    }
}
