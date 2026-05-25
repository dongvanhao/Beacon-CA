using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.DeleteGroup;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class DeleteGroupCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    [Fact]
    public async Task Handle_WhenCallerIsOwner_DeletesGroup()
    {
        var groupId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var group = BuildGroup(groupId, ownerId, GroupMemberRole.Owner);

        _currentUserMock.Setup(x => x.UserId).Returns(ownerId);
        _groupRepoMock.Setup(x => x.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteGroupCommand(groupId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        group.IsDeleted.Should().BeTrue();
        _groupRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(GroupMemberRole.Member)]
    [InlineData(GroupMemberRole.Manager)]
    public async Task Handle_WhenCallerIsNotOwner_ReturnsForbidden(GroupMemberRole role)
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var group = BuildGroup(groupId, userId, role);

        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _groupRepoMock.Setup(x => x.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();

        var result = await handler.Handle(new DeleteGroupCommand(groupId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        group.IsDeleted.Should().BeFalse();
        _groupRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private DeleteGroupCommandHandler CreateHandler()
        => new(_groupRepoMock.Object, _currentUserMock.Object);

    private static MessageGroup BuildGroup(Guid groupId, Guid userId, GroupMemberRole role)
    {
        var group = new MessageGroup
        {
            Type = MessageGroupType.Group,
            CreatedAtUtc = DateTime.UtcNow
        };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);
        group.Members.Add(new MessageGroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = DateTime.UtcNow
        });
        return group;
    }
}
