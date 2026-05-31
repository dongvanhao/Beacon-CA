using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.DeleteGroup;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Results;
using FluentAssertions;
using MediatR;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class DeleteGroupCommandHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<ISender> _senderMock = new();

    [Fact]
    public async Task Handle_WhenCallerIsOwner_DeletesGroup()
    {
        var groupId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var group = BuildGroup(groupId, ownerId, GroupMemberRole.Owner);

        _currentUserMock.Setup(x => x.UserId).Returns(ownerId);
        _groupRepoMock.Setup(x => x.GetByIdWithMembersAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _senderMock.Setup(x => x.Send(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MessageDto>.Success(BuildMessageDto(groupId, ownerId)));

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
        _senderMock.Verify(x => x.Send(It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        _groupRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private DeleteGroupCommandHandler CreateHandler()
        => new(_groupRepoMock.Object, _currentUserMock.Object, _senderMock.Object);

    private static MessageDto BuildMessageDto(Guid groupId, Guid senderId)
        => new(
            Guid.NewGuid(),
            groupId,
            senderId,
            "Owner",
            "Group deleted",
            MessageType.GroupDeleted,
            null,
            DateTime.UtcNow,
            null,
            null);

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
