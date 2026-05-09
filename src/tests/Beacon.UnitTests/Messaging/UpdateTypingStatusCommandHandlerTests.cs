using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.UpdateTypingStatus;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class UpdateTypingStatusCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly UpdateTypingStatusCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();

    public UpdateTypingStatusCommandHandlerTests()
    {
        _sut = new UpdateTypingStatusCommandHandler(
            _groupRepoMock.Object,
            _notifierMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenGroupDoesNotExist()
    {
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);

        var result = await _sut.Handle(
            new UpdateTypingStatusCommand(Guid.NewGuid(), _userId, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenUserIsNotGroupMember()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        // no members — user is not in group

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(
            new UpdateTypingStatusCommand(groupId, _userId, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldCallNotifyTypingAsync_WhenUserIsTyping()
    {
        var groupId = Guid.NewGuid();
        var otherMember = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _userId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = otherMember, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(
            new UpdateTypingStatusCommand(groupId, _userId, true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(
            n => n.NotifyTypingAsync(
                groupId,
                _userId,
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCallNotifyTypingAsync_WhenUserStopsTyping()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _userId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _sut.Handle(
            new UpdateTypingStatusCommand(groupId, _userId, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(
            n => n.NotifyTypingAsync(
                groupId,
                _userId,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotCallSaveChangesAsync()
    {
        var groupId = Guid.NewGuid();
        var group = new MessageGroup { Type = MessageGroupType.Group, CreatedAtUtc = DateTime.UtcNow };
        group.Members.Add(new MessageGroupMember { GroupId = groupId, UserId = _userId, Role = GroupMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });

        _groupRepoMock
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _sut.Handle(new UpdateTypingStatusCommand(groupId, _userId, true), CancellationToken.None);

        _groupRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
