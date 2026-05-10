using Beacon.Application.Common.Interfaces.IService;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Identity;
using Beacon.Infrashtructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Notifications;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _notifRepoMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly Mock<IFcmService> _fcmServiceMock = new();
    private readonly Mock<IUserDeviceTokenRepository> _tokenRepoMock = new();
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _notifRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _notifierMock
            .Setup(n => n.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fcmServiceMock
            .Setup(f => f.SendToUserAndGetInvalidTokensAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        _sut = new NotificationService(
            _notifRepoMock.Object,
            _notifierMock.Object,
            _fcmServiceMock.Object,
            _tokenRepoMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAndDeliverAsync_ShouldSaveNotificationToDb_Always()
    {
        var receiverId = Guid.NewGuid();

        await _sut.CreateAndDeliverAsync(
            receiverId, NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        _notifRepoMock.Verify(
            r => r.AddAsync(
                It.Is<Notification>(n =>
                    n.ReceiverUserId == receiverId &&
                    n.Type == NotificationType.FriendAccepted &&
                    n.Title == "Title" &&
                    n.Body == "Body"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notifRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAndDeliverAsync_ShouldEmitSignalR_AfterDbCommit()
    {
        var receiverId = Guid.NewGuid();
        var callOrder = new List<string>();

        _notifRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SaveChanges"))
            .Returns(Task.CompletedTask);
        _notifierMock
            .Setup(n => n.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("SignalR"))
            .Returns(Task.CompletedTask);

        await _sut.CreateAndDeliverAsync(
            receiverId, NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        callOrder.Should().ContainInOrder("SaveChanges", "SignalR");
        _notifierMock.Verify(
            n => n.NotifyUserAsync(receiverId, It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAndDeliverAsync_ShouldNotThrow_WhenSignalRFails()
    {
        _notifierMock
            .Setup(n => n.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR connection lost"));

        var act = async () => await _sut.CreateAndDeliverAsync(
            Guid.NewGuid(), NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAndDeliverAsync_ShouldNotRollbackNotification_WhenSignalRFails()
    {
        _notifierMock
            .Setup(n => n.NotifyUserAsync(It.IsAny<Guid>(), It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SignalR connection lost"));

        await _sut.CreateAndDeliverAsync(
            Guid.NewGuid(), NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        // DB save must have happened — notification is persisted regardless of SignalR failure
        _notifRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
