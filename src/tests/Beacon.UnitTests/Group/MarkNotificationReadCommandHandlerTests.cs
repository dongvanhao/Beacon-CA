using Beacon.Application.Features.Group.Commands.MarkNotificationRead;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class MarkNotificationReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();
    private readonly MarkNotificationReadCommandHandler _sut;

    public MarkNotificationReadCommandHandlerTests()
    {
        _sut = new MarkNotificationReadCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndReturnNewUnreadCount()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(userId, NotificationType.FriendRequest, "Title", "Body");
        var notificationId = notification.Id;

        _repoMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _sut.Handle(new MarkNotificationReadCommand(notificationId, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadCount.Should().Be(3);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNotificationDoesNotExist()
    {
        var notificationId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(notificationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification?)null);

        var result = await _sut.Handle(
            new MarkNotificationReadCommand(notificationId, Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Notification.NOTIFICATION_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotificationBelongsToAnotherUser()
    {
        var ownerId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var notification = Notification.Create(ownerId, NotificationType.FriendRequest, "Title", "Body");

        _repoMock.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);

        var result = await _sut.Handle(
            new MarkNotificationReadCommand(notification.Id, requesterId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Notification.NOTIFICATION_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent_WhenAlreadyRead()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(userId, NotificationType.FriendRequest, "Title", "Body");
        notification.MarkRead();

        _repoMock.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _repoMock.Setup(r => r.CountUnreadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.Handle(
            new MarkNotificationReadCommand(notification.Id, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadCount.Should().Be(0);
    }
}
