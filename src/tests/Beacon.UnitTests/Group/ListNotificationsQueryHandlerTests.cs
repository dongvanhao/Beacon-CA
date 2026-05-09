using Beacon.Application.Features.Group.Queries.ListNotifications;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class ListNotificationsQueryHandlerTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();
    private readonly ListNotificationsQueryHandler _sut;

    public ListNotificationsQueryHandlerTests()
    {
        _sut = new ListNotificationsQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenUserHasNoNotifications()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.ListByReceiverAsync(userId, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.Handle(new ListNotificationsQuery(userId, null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.HasNextPage.Should().BeFalse();
        result.Value.UnreadCount.Should().Be(0);
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReturnCursorPagedResult_WithCorrectUnreadCount()
    {
        var userId = Guid.NewGuid();
        var notifications = CreateNotifications(userId, 3, readCount: 1);

        _repoMock.Setup(r => r.ListByReceiverAsync(userId, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _sut.Handle(new ListNotificationsQuery(userId, null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.UnreadCount.Should().Be(2);
        result.Value.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnHasNextPage_WhenMoreItemsExist()
    {
        var userId = Guid.NewGuid();
        // limit=2 → request limit+1=3 items; if repo returns 3, HasNextPage=true
        var notifications = CreateNotifications(userId, 3);

        _repoMock.Setup(r => r.ListByReceiverAsync(userId, null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _sut.Handle(new ListNotificationsQuery(userId, null, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.HasNextPage.Should().BeTrue();
        result.Value.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldCapLimitAt50()
    {
        var userId = Guid.NewGuid();

        _repoMock.Setup(r => r.ListByReceiverAsync(userId, null, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _sut.Handle(new ListNotificationsQuery(userId, null, 200), CancellationToken.None);

        // Verify that limit was capped to 50 → repo called with 51 (50+1)
        _repoMock.Verify(r => r.ListByReceiverAsync(userId, null, 51, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static List<Notification> CreateNotifications(Guid userId, int count, int readCount = 0)
    {
        var list = new List<Notification>();
        for (int i = 0; i < count; i++)
        {
            var n = Notification.Create(userId, NotificationType.FriendRequest, $"Title {i}", $"Body {i}");
            if (i < readCount) n.MarkRead();
            list.Add(n);
        }
        return list;
    }
}
