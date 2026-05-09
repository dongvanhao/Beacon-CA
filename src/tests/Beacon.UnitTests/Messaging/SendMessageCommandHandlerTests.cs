using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
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
    private readonly MessageMapper _mapper = new();
    private readonly SendMessageCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SendMessageCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.Username).Returns("testuser");
        _currentUserMock.Setup(s => s.FamilyName).Returns("Le");
        _currentUserMock.Setup(s => s.GivenName).Returns("Sender");

        _sut = new SendMessageCommandHandler(
            _groupRepoMock.Object,
            _messageRepoMock.Object,
            _currentUserMock.Object,
            _notifierMock.Object,
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
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
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
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
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
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
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
            n => n.NotifyNewMessageAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCallNotifyNewMessageAsync_WhenMessageSent()
    {
        var groupId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        var group = new MessageGroup { IsPrivate = true, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _currentUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = otherMember, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _messageRepoMock.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hi!", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(
            n => n.NotifyNewMessageAsync(
                groupId,
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(otherMember) && !ids.Contains(_currentUserId)),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPushToSender_OnlyToOtherMembers()
    {
        var groupId = Guid.NewGuid();
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();
        var group = new MessageGroup { IsPrivate = false, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _currentUserId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = member2, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = member3, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock.Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        _messageRepoMock.Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _messageRepoMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        IEnumerable<Guid>? capturedIds = null;
        _notifierMock
            .Setup(n => n.NotifyNewMessageAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IEnumerable<Guid>, object, CancellationToken>((_, ids, _, _) => capturedIds = ids.ToList())
            .Returns(Task.CompletedTask);

        await _sut.Handle(new SendMessageCommand(groupId, "Hello group!", null), CancellationToken.None);

        capturedIds.Should().NotBeNull();
        capturedIds.Should().Contain(member2);
        capturedIds.Should().Contain(member3);
        capturedIds.Should().NotContain(_currentUserId);
    }
}
