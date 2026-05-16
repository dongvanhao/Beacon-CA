using Beacon.Application.Features.Group.Events;
using Beacon.Application.Features.Messaging.EventHandlers;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Messaging;
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
        _groupRepoMock
            .Setup(r => r.GetByDirectKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageGroup?)null);
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _settingRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroupMemberSetting>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _groupRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new CreateDirectMessageGroupHandler(
            _groupRepoMock.Object,
            _settingRepoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateDirectGroupWithCorrectDirectKey()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var ev = new FriendRequestAcceptedEvent(Guid.NewGuid(), senderId, receiverId, Guid.NewGuid());

        MessageGroup? capturedGroup = null;
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()))
            .Callback<MessageGroup, CancellationToken>((g, _) => capturedGroup = g)
            .Returns(Task.CompletedTask);

        await _sut.Handle(ev, CancellationToken.None);

        capturedGroup.Should().NotBeNull();
        capturedGroup!.Type.Should().Be(MessageGroupType.Direct);
        capturedGroup.DirectKey.Should().Be(MessageGroup.BuildDirectKey(senderId, receiverId));
        capturedGroup.Members.Should().HaveCount(2);
        capturedGroup.Members.Should().Contain(m => m.UserId == senderId);
        capturedGroup.Members.Should().Contain(m => m.UserId == receiverId);
    }

    [Fact]
    public async Task Handle_ShouldSkipCreation_WhenDirectKeyAlreadyExists()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var directKey = MessageGroup.BuildDirectKey(senderId, receiverId);
        var ev = new FriendRequestAcceptedEvent(Guid.NewGuid(), senderId, receiverId, Guid.NewGuid());

        var existing = new MessageGroup { Type = MessageGroupType.Direct, DirectKey = directKey };
        _groupRepoMock
            .Setup(r => r.GetByDirectKeyIncludingDeletedAsync(directKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.Handle(ev, CancellationToken.None);

        _groupRepoMock.Verify(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()), Times.Never);
        _groupRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreateTwoMemberSettings_WhenGroupIsNew()
    {
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var ev = new FriendRequestAcceptedEvent(Guid.NewGuid(), senderId, receiverId, Guid.NewGuid());

        await _sut.Handle(ev, CancellationToken.None);

        _settingRepoMock.Verify(
            r => r.AddAsync(It.IsAny<MessageGroupMemberSetting>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _groupRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildDirectKey_ShouldBeOrderIndependent()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        MessageGroup.BuildDirectKey(userA, userB).Should().Be(MessageGroup.BuildDirectKey(userB, userA));
    }
}
