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
    private readonly Mock<IBeaconHub> _callerMock = new();
    private readonly SignalRRealtimeNotifier _sut;

    public SignalRRealtimeNotifierTests()
    {
        _hubContextMock.Setup(h => h.Clients).Returns(_clientsMock.Object);
        _clientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(_callerMock.Object);
        _callerMock.Setup(c => c.ReceiveNotification(It.IsAny<NotificationPayload>())).Returns(Task.CompletedTask);
        _callerMock.Setup(c => c.ReceiveNewMessage(It.IsAny<object>())).Returns(Task.CompletedTask);
        _callerMock.Setup(c => c.ReceiveTypingStatus(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>())).Returns(Task.CompletedTask);
        _callerMock.Setup(c => c.ReceiveMessageSeen(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _sut = new SignalRRealtimeNotifier(_hubContextMock.Object);
    }

    [Fact]
    public async Task NotifyUserAsync_ShouldCallClientsUserWithCorrectPayload()
    {
        var userId = Guid.NewGuid();
        var payload = new NotificationPayload(Guid.NewGuid(), "FriendRequest", "Title", "Body", null);

        await _sut.NotifyUserAsync(userId, payload);

        _clientsMock.Verify(c => c.User(userId.ToString()), Times.Once);
        _callerMock.Verify(c => c.ReceiveNotification(payload), Times.Once);
    }

    [Fact]
    public async Task NotifyNewMessageAsync_ShouldPushToAllMembersExceptSender()
    {
        var groupId = Guid.NewGuid();
        var member1 = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var messageDto = new { Content = "hello" };

        await _sut.NotifyNewMessageAsync(groupId, [member1, member2], messageDto);

        _clientsMock.Verify(c => c.User(member1.ToString()), Times.Once);
        _clientsMock.Verify(c => c.User(member2.ToString()), Times.Once);
        _callerMock.Verify(c => c.ReceiveNewMessage(messageDto), Times.Exactly(2));
    }

    [Fact]
    public async Task NotifyTypingAsync_ShouldExcludeTypingUser()
    {
        var groupId = Guid.NewGuid();
        var typingUserId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        // otherMemberIds already excludes the typing user per interface contract
        // but we also apply the filter inside notifier as a safety net
        var otherMemberIds = new[] { otherMember, typingUserId };

        await _sut.NotifyTypingAsync(groupId, otherMemberIds, typingUserId, true);

        _clientsMock.Verify(c => c.User(otherMember.ToString()), Times.Once);
        _clientsMock.Verify(c => c.User(typingUserId.ToString()), Times.Never);
    }

    [Fact]
    public async Task NotifyMessageSeenAsync_ShouldExcludeSeenByUser()
    {
        var groupId = Guid.NewGuid();
        var seenByUserId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        var lastSeenMessageId = Guid.NewGuid();
        var otherMemberIds = new[] { otherMember, seenByUserId };

        await _sut.NotifyMessageSeenAsync(groupId, otherMemberIds, seenByUserId, lastSeenMessageId);

        _clientsMock.Verify(c => c.User(otherMember.ToString()), Times.Once);
        _clientsMock.Verify(c => c.User(seenByUserId.ToString()), Times.Never);
    }
}
