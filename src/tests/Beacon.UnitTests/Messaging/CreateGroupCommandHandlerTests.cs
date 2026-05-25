using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.CreateGroup;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class CreateGroupCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly MessageGroupDetailMapper _mapper = new();

    [Fact]
    public async Task Handle_WhenAllUsersAreFriends_CreatesGroupWithOwnerAndMembers()
    {
        var creator = User.Create("creator", "creator@test.com", "hash", "Nguyen", "Creator");
        var friendA = User.Create("frienda", "a@test.com", "hash", "Tran", "A");
        var friendB = User.Create("friendb", "b@test.com", "hash", "Le", "B");
        var requestedIds = new[] { friendA.Id, friendB.Id };
        MessageGroup? capturedGroup = null;

        _currentUserMock.Setup(x => x.UserId).Returns(creator.Id);
        _friendRepoMock
            .Setup(x => x.GetFriendIdsAsync(creator.Id, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requestedIds.ToHashSet());
        _groupRepoMock
            .Setup(x => x.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()))
            .Callback<MessageGroup, CancellationToken>((group, _) => capturedGroup = AttachUsers(group, creator, friendA, friendB))
            .Returns(Task.CompletedTask);
        _groupRepoMock
            .Setup(x => x.GetByIdWithMembersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => capturedGroup);

        var handler = CreateHandler();

        var result = await handler.Handle(new CreateGroupCommand(requestedIds), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedGroup.Should().NotBeNull();
        capturedGroup!.RequireApprovalToAddMembers.Should().BeTrue();
        capturedGroup.Members.Should().HaveCount(3);
        capturedGroup.Members.Single(m => m.UserId == creator.Id).Role.Should().Be(GroupMemberRole.Owner);
        capturedGroup.Members.All(m => m.Status == MessageGroupMemberStatus.Joined).Should().BeTrue();
        result.Value!.Members.Select(m => m.UserId).Should().BeEquivalentTo(new[] { creator.Id, friendA.Id, friendB.Id });
        result.Value.RequireApprovalToAddMembers.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenAnyUserIsNotFriend_ReturnsForbidden()
    {
        var creator = User.Create("creator", "creator@test.com", "hash", "Nguyen", "Creator");
        var friendId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        _currentUserMock.Setup(x => x.UserId).Returns(creator.Id);
        _friendRepoMock
            .Setup(x => x.GetFriendIdsAsync(creator.Id, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([friendId]);

        var handler = CreateHandler();

        var result = await handler.Handle(new CreateGroupCommand([friendId, strangerId]), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _groupRepoMock.Verify(x => x.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateGroupCommandHandler CreateHandler()
        => new(_groupRepoMock.Object, _friendRepoMock.Object, _currentUserMock.Object, _storageMock.Object, _mapper);

    private static MessageGroup AttachUsers(MessageGroup group, User creator, User friendA, User friendB)
    {
        var users = new[] { creator, friendA, friendB }.ToDictionary(u => u.Id);
        foreach (var member in group.Members)
            member.User = users[member.UserId];

        return group;
    }
}
