using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.AddGroupMember;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class AddGroupMemberCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();

    [Theory]
    [InlineData(GroupMemberRole.Owner, MessageGroupMemberStatus.Joined)]
    [InlineData(GroupMemberRole.Manager, MessageGroupMemberStatus.Joined)]
    [InlineData(GroupMemberRole.Member, MessageGroupMemberStatus.PendingApproval)]
    public async Task Handle_WhenJoinedMemberAddsUser_AssignsStatusByCallerRole(
        GroupMemberRole callerRole,
        MessageGroupMemberStatus expectedStatus)
    {
        var groupId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var group = new MessageGroup
        {
            Type = MessageGroupType.Group,
            CreatedAtUtc = DateTime.UtcNow
        };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);
        group.Members.Add(new MessageGroupMember
        {
            GroupId = groupId,
            UserId = callerId,
            Role = callerRole,
            Status = MessageGroupMemberStatus.Joined,
            JoinedAtUtc = DateTime.UtcNow
        });

        _currentUserMock.Setup(x => x.UserId).Returns(callerId);
        _groupRepoMock.Setup(x => x.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _userRepoMock.Setup(x => x.ExistsAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();

        var result = await handler.Handle(new AddGroupMemberCommand(groupId, targetId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        group.Members.Single(m => m.UserId == targetId).Status.Should().Be(expectedStatus);
        _groupRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCallerIsPendingApproval_ReturnsForbidden()
    {
        var groupId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var group = new MessageGroup
        {
            Type = MessageGroupType.Group,
            CreatedAtUtc = DateTime.UtcNow
        };
        group.GetType().GetProperty("Id")!.SetValue(group, groupId);
        group.Members.Add(new MessageGroupMember
        {
            GroupId = groupId,
            UserId = callerId,
            Role = GroupMemberRole.Member,
            Status = MessageGroupMemberStatus.PendingApproval,
            JoinedAtUtc = DateTime.UtcNow
        });

        _currentUserMock.Setup(x => x.UserId).Returns(callerId);
        _groupRepoMock.Setup(x => x.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var handler = CreateHandler();

        var result = await handler.Handle(new AddGroupMemberCommand(groupId, targetId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _groupRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private AddGroupMemberCommandHandler CreateHandler()
        => new(_groupRepoMock.Object, _userRepoMock.Object, _currentUserMock.Object, _notificationMock.Object);
}
