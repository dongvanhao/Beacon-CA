using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Microsoft.Extensions.Logging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class SendMessageCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IMessageRepository> _messageRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly Mock<IFcmService> _fcmServiceMock = new();
    private readonly Mock<IMessageGroupPresenceTracker> _presenceTrackerMock = new();
    private readonly Mock<ILogger<SendMessageCommandHandler>> _loggerMock = new();
    private readonly MessageMapper _mapper = new();
    private readonly SendMessageCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SendMessageCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.Username).Returns("testuser");
        _currentUserMock.Setup(s => s.FamilyName).Returns("Le");
        _currentUserMock.Setup(s => s.GivenName).Returns("Sender");

        _presenceTrackerMock
            .Setup(p => p.IsUserInGroup(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns(false);

        _fcmServiceMock
            .Setup(f => f.SendToUserAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SendMessageCommandHandler(
            _groupRepoMock.Object,
            _messageRepoMock.Object,
            _currentUserMock.Object,
            _notifierMock.Object,
            _fcmServiceMock.Object,
            _presenceTrackerMock.Object,
            _loggerMock.Object,
            _mapper);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenGroupDoesNotExist()
    {
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotMember()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };
        // no members — current user is not in the group

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello", null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenMember()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember
        {
            GroupId = groupId,
            UserId = _currentUserId,
            Role = GroupMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _messageRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello!", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().Be("Hello!");
        result.Value.GroupId.Should().Be(groupId);
        result.Value.SenderId.Should().Be(_currentUserId);
        _messageRepoMock.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnExistingMessage_WhenClientMessageIdAlreadyExists()
    {
        var groupId = Guid.NewGuid();
        var clientId = "client-uuid-123";
        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember
        {
            GroupId = groupId,
            UserId = _currentUserId,
            Role = GroupMemberRole.Member,
            JoinedAtUtc = DateTime.UtcNow
        });

        var existingMessage = Message.Create(groupId, _currentUserId, "Hello!", clientId);

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _messageRepoMock
            .Setup(r => r.GetByClientMessageIdAsync(groupId, clientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMessage);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello!", clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Content.Should().Be("Hello!");
        // should not add a new message
        _messageRepoMock.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Never);
        // idempotency path — no notification push
        _notifierMock.Verify(
            n => n.NotifyNewMessageAsync(It.IsAny<Guid>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notifierMock.Verify(
            n => n.NotifyNewMessageAsync(
                It.IsAny<Guid>(),
                It.IsAny<object>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCallNotifyNewMessageAsync_WhenMessageSent()
    {
        var groupId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _currentUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = otherMember, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _messageRepoMock.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hi!", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        group.Members.Single(m => m.UserId == _currentUserId).LastSeenMessageId.Should().Be(result.Value!.Id);
        _notifierMock.Verify(
            n => n.NotifyNewMessageAsync(
                groupId,
                It.IsAny<object>(),
                It.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 2 &&
                    ids.Contains(_currentUserId) &&
                    ids.Contains(otherMember)),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notifierMock.Verify(
            n => n.NotifyUnreadMessageCountAsync(_currentUserId, groupId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _notifierMock.Verify(
            n => n.NotifyUnreadMessageCountAsync(otherMember, groupId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldBroadcastToGroupRoom_WhenMessageSentToMultiMemberGroup()
    {
        var groupId = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _currentUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = member2, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = member3, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _messageRepoMock.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello group!", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(
            n => n.NotifyNewMessageAsync(
                groupId,
                It.IsAny<object>(),
                It.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.Count == 3 &&
                    ids.Contains(_currentUserId) &&
                    ids.Contains(member2) &&
                    ids.Contains(member3)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSendFcm_ToOfflineMembers_Only()
    {
        var groupId = Guid.NewGuid();
        var inRoomUserId = Guid.NewGuid();
        var offlineUserId = Guid.NewGuid();

        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _currentUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = inRoomUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = offlineUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _messageRepoMock.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _presenceTrackerMock
            .Setup(p => p.IsUserInGroup(inRoomUserId, groupId))
            .Returns(true);

        var fcmSignal = new TaskCompletionSource<bool>();
        _fcmServiceMock
            .Setup(f => f.SendToUserAsync(
                offlineUserId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => fcmSignal.TrySetResult(true))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hi!", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await fcmSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        _fcmServiceMock.Verify(
            f => f.SendToUserAsync(
                offlineUserId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _fcmServiceMock.Verify(
            f => f.SendToUserAsync(
                inRoomUserId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _fcmServiceMock.Verify(
            f => f.SendToUserAsync(
                _currentUserId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
