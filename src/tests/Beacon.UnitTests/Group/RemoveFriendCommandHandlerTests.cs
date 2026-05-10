using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Commands.RemoveFriend;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class RemoveFriendCommandHandlerTests
{
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly RemoveFriendCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public RemoveFriendCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);

        _sut = new RemoveFriendCommandHandler(
            _friendRepoMock.Object,
            _groupRepoMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenNotFriends()
    {
        _friendRepoMock
            .Setup(r => r.GetByUsersAsync(_currentUserId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Friend?)null);

        var result = await _sut.Handle(new RemoveFriendCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndSoftDeleteGroup()
    {
        var targetId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var (u1, u2) = _currentUserId < targetId ? (_currentUserId, targetId) : (targetId, _currentUserId);
        var friend = new Friend { UserId1 = u1, UserId2 = u2, Type = FriendType.Normal };

        var group = new MessageGroup { Type = MessageGroupType.Direct, CreatedAtUtc = DateTime.UtcNow };

        _friendRepoMock
            .Setup(r => r.GetByUsersAsync(_currentUserId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friend);
        _groupRepoMock
            .Setup(r => r.GetPrivateGroupBetweenAsync(u1, u2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _groupRepoMock
            .Setup(r => r.RemoveMemberAsync(group.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _friendRepoMock
            .Setup(r => r.DeleteAsync(friend, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _friendRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new RemoveFriendCommand(targetId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        group.IsDeleted.Should().BeTrue();
        _groupRepoMock.Verify(r => r.RemoveMemberAsync(group.Id, u1, It.IsAny<CancellationToken>()), Times.Once);
        _groupRepoMock.Verify(r => r.RemoveMemberAsync(group.Id, u2, It.IsAny<CancellationToken>()), Times.Once);
        _friendRepoMock.Verify(r => r.DeleteAsync(friend, It.IsAny<CancellationToken>()), Times.Once);
        _friendRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_EvenWhenGroupNotFound()
    {
        var targetId = Guid.NewGuid();
        var (u1, u2) = _currentUserId < targetId ? (_currentUserId, targetId) : (targetId, _currentUserId);
        var friend = new Friend { UserId1 = u1, UserId2 = u2, Type = FriendType.Normal };

        _friendRepoMock
            .Setup(r => r.GetByUsersAsync(_currentUserId, targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friend);
        _groupRepoMock
            .Setup(r => r.GetPrivateGroupBetweenAsync(u1, u2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);
        _friendRepoMock
            .Setup(r => r.DeleteAsync(friend, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _friendRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new RemoveFriendCommand(targetId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _groupRepoMock.Verify(r => r.RemoveMemberAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
