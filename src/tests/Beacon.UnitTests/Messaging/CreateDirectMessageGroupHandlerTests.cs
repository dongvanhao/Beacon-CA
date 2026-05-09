using Beacon.Application.Features.Group.Events;
using Beacon.Application.Features.Messaging.EventHandlers;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.IRepository.Messaging;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Messaging;

public class CreateDirectMessageGroupHandlerTests
{
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<IMessageGroupMemberSettingRepository> _settingRepoMock = new();
    private readonly CreateDirectMessageGroupHandler _sut;

    public CreateDirectMessageGroupHandlerTests()
    {
        _sut = new CreateDirectMessageGroupHandler(
            _groupRepoMock.Object,
            _settingRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreatePrivateGroupWithTwoMembersAndSettings()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var ev = new FriendRequestAcceptedEvent(Guid.NewGuid(), senderId, receiverId, Guid.NewGuid());

        MessageGroup? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()))
            .Callback<MessageGroup, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);
        _settingRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroupMemberSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.Handle(ev, CancellationToken.None);

        capturedGroup.Should().NotBeNull();
        capturedGroup!.IsPrivate.Should().BeTrue();
        capturedGroup.Members.Should().HaveCount(2);
        capturedGroup.Members.Should().Contain(m => m.UserId == senderId);
        capturedGroup.Members.Should().Contain(m => m.UserId == receiverId);

        _settingRepoMock.Verify(r => r.AddAsync(It.IsAny<MessageGroupMemberSetting>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _groupRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
