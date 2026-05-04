using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Commands.SendMessage;
using Beacon.Application.Mappings.Messaging;
using Beacon.Domain.Entities.Messaging;
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
    private readonly MessageMapper _mapper = new();
    private readonly SendMessageCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SendMessageCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.Username).Returns("testuser");

        _sut = new SendMessageCommandHandler(
            _groupRepoMock.Object,
            _messageRepoMock.Object,
            _currentUserMock.Object,
            _mapper);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotMember()
    {
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.IsMemberAsync(groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenMember()
    {
        var groupId = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.IsMemberAsync(groupId, _currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _messageRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _messageRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new SendMessageCommand(groupId, "Hello!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Content.Should().Be("Hello!");
        result.Value.GroupId.Should().Be(groupId);
        result.Value.SenderId.Should().Be(_currentUserId);
        _messageRepoMock.Verify(r => r.AddAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
