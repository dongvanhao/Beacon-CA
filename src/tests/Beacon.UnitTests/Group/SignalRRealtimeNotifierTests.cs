using Beacon.Api.Hubs;
using Beacon.Api.Services;
using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Common.Interfaces.IService;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Beacon.UnitTests.Group;

public class SignalRRealtimeNotifierTests
{
    private readonly Mock<IHubContext<BeaconHub, IBeaconHub>> _hubContextMock = new();
    private readonly Mock<IHubClients<IBeaconHub>> _clientsMock = new();
    private readonly Mock<IBeaconHub> _groupClientMock = new();
    private readonly SignalRRealtimeNotifier _sut;

    public SignalRRealtimeNotifierTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupClientMock.Object);
        _groupClientMock.Setup(c => c.ReceiveNotification(It.IsAny<NotificationPayload>())).Returns(Task.CompletedTask);
        _groupClientMock.Setup(c => c.ReceiveNewMessage(It.IsAny<object>())).Returns(Task.CompletedTask);
        _groupClientMock.Setup(c => c.ReceiveTypingStatus(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
        _groupClientMock.Setup(c => c.ReceiveMessageSeen(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _sut = new SignalRRealtimeNotifier(_hubContextMock.Object);
    }

    [Fact]
    public async Task NotifyUserAsync_ShouldCallGroupRoomWithCorrectPayload()
    {
        var userId = Guid.NewGuid();
        var payload = new NotificationPayload(Guid.NewGuid(), "FriendRequest", "Title", "Body", null);

        await _sut.NotifyUserAsync(userId, payload);

        _clientsMock.Verify(c => c.Group($"user:{userId}"), Times.Once);
        _groupClientMock.Verify(c => c.ReceiveNotification(payload), Times.Once);
    }

    [Fact]
    public async Task NotifyNewMessageAsync_ShouldBroadcastToMessageGroupRoom()
    {
        var groupId = Guid.NewGuid();
        var messageDto = new { Content = "hello" };

        await _sut.NotifyNewMessageAsync(groupId, messageDto);

        _clientsMock.Verify(c => c.Group($"message_group:{groupId}"), Times.Once);
        _groupClientMock.Verify(c => c.ReceiveNewMessage(messageDto), Times.Once);
    }

    [Fact]
    public async Task NotifyTypingAsync_ShouldBroadcastToMessageGroupRoom()
    {
        var groupId = Guid.NewGuid();
        var typingUserId = Guid.NewGuid();

        await _sut.NotifyTypingAsync(groupId, typingUserId, true);

        _clientsMock.Verify(c => c.Group($"message_group:{groupId}"), Times.Once);
        _groupClientMock.Verify(c => c.ReceiveTypingStatus(groupId, typingUserId, true), Times.Once);
    }

    [Fact]
    public async Task NotifyMessageSeenAsync_ShouldBroadcastToMessageGroupRoom()
    {
        var groupId = Guid.NewGuid();
        var seenByUserId = Guid.NewGuid();
        var lastSeenMessageId = Guid.NewGuid();

        await _sut.NotifyMessageSeenAsync(groupId, seenByUserId, lastSeenMessageId);

        _clientsMock.Verify(c => c.Group($"message_group:{groupId}"), Times.Once);
        _groupClientMock.Verify(c => c.ReceiveMessageSeen(groupId, seenByUserId, lastSeenMessageId), Times.Once);
    }
}
