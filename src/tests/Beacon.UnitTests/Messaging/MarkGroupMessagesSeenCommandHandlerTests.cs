using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.MarkGroupMessagesSeen;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class MarkGroupMessagesSeenCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IMessageRepository> _messageRepoMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly MarkGroupMessagesSeenCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public MarkGroupMessagesSeenCommandHandlerTests()
    {
        _sut = new MarkGroupMessagesSeenCommandHandler(
            _groupRepoMock.Object,
            _messageRepoMock.Object,
            _notifierMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenGroupDoesNotExist()
    {
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);

        var result = await _sut.Handle(
            new MarkGroupMessagesSeenCommand(Guid.NewGuid(), _userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserIsNotGroupMember()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { IsPrivate = false, CreatedAtUtc = DateTime.UtcNow };
        // no members

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(
            new MarkGroupMessagesSeenCommand(groupId, _userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenMessageDoesNotExistInGroup()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { IsPrivate = false, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _userId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _messageRepoMock
            .Setup(r => r.ExistsInGroupAsync(groupId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Handle(
            new MarkGroupMessagesSeenCommand(groupId, _userId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldUpdateLastSeenMessageId_AndNotifyOtherMembers()
    {
        var groupId = Guid.NewGuid();
        var lastSeenMessageId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();

        var myMember = new MessageGroupMember { GroupId = groupId, UserId = _userId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow };
        var group = new MessageGroup { IsPrivate = false, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(myMember);
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = otherMember, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _messageRepoMock
            .Setup(r => r.ExistsInGroupAsync(groupId, lastSeenMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groupRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new MarkGroupMessagesSeenCommand(groupId, _userId, lastSeenMessageId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        myMember.LastSeenMessageId.Should().Be(lastSeenMessageId);
        _groupRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifierMock.Verify(
            n => n.NotifyMessageSeenAsync(
                groupId,
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(otherMember) && !ids.Contains(_userId)),
                _userId,
                lastSeenMessageId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
