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

public class NotificationServiceFcmTests
{
    private readonly Mock<INotificationRepository> _notifRepoMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly Mock<IFcmService> _fcmServiceMock = new();
    private readonly Mock<IUserDeviceTokenRepository> _tokenRepoMock = new();
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();
    private readonly NotificationService _sut;

    public NotificationServiceFcmTests()
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
        _tokenRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scopeFactory = NotificationServiceTests.BuildScopeFactory(
            _fcmServiceMock.Object, _tokenRepoMock.Object);

        _sut = new NotificationService(
            _notifRepoMock.Object,
            _notifierMock.Object,
            scopeFactory,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAndDeliver_ShouldCallFcmSendToUser_AfterDbCommit()
    {
        var receiverId = Guid.NewGuid();
        var fcmSignal = new TaskCompletionSource<bool>();

        _fcmServiceMock
            .Setup(f => f.SendToUserAndGetInvalidTokensAsync(
                receiverId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => fcmSignal.TrySetResult(true))
            .ReturnsAsync(new List<string>());

        await _sut.CreateAndDeliverAsync(
            receiverId, NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        await fcmSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _fcmServiceMock.Verify(
            f => f.SendToUserAndGetInvalidTokensAsync(
                receiverId, "Title", "Body",
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAndDeliver_ShouldMarkInvalidTokens_WhenFcmReportsInvalid()
    {
        var receiverId = Guid.NewGuid();
        var invalidToken = "invalid-fcm-token-123";
        var fcmSignal = new TaskCompletionSource<bool>();

        _fcmServiceMock
            .Setup(f => f.SendToUserAndGetInvalidTokensAsync(
                receiverId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { invalidToken });

        _tokenRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => fcmSignal.TrySetResult(true))
            .Returns(Task.CompletedTask);

        await _sut.CreateAndDeliverAsync(
            receiverId, NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        await fcmSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _tokenRepoMock.Verify(
            r => r.GetByTokenAsync(invalidToken, It.IsAny<CancellationToken>()),
            Times.Once);
        _tokenRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAndDeliver_ShouldNotThrow_WhenFcmFails()
    {
        var fcmSignal = new TaskCompletionSource<bool>();

        _fcmServiceMock
            .Setup(f => f.SendToUserAndGetInvalidTokensAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => fcmSignal.TrySetResult(true))
            .ThrowsAsync(new Exception("Firebase unavailable"));

        var act = async () => await _sut.CreateAndDeliverAsync(
            Guid.NewGuid(), NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        await act.Should().NotThrowAsync();

        await fcmSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAndDeliver_ShouldNotRollbackNotification_WhenFcmFails()
    {
        var fcmSignal = new TaskCompletionSource<bool>();

        _fcmServiceMock
            .Setup(f => f.SendToUserAndGetInvalidTokensAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => fcmSignal.TrySetResult(true))
            .ThrowsAsync(new Exception("Firebase unavailable"));

        await _sut.CreateAndDeliverAsync(
            Guid.NewGuid(), NotificationType.FriendAccepted, "Title", "Body", null, CancellationToken.None);

        await fcmSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Notification must be persisted regardless of FCM failure
        _notifRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
