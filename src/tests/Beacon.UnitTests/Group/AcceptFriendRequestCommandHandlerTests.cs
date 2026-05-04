using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Commands.AcceptFriendRequest;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Messaging;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class AcceptFriendRequestCommandHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requestRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IMessageGroupRepository> _groupRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly FriendMapper _mapper = new();
    private readonly AcceptFriendRequestCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public AcceptFriendRequestCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.Username).Returns("receiver");

        _sut = new AcceptFriendRequestCommandHandler(
            _requestRepoMock.Object,
            _friendRepoMock.Object,
            _groupRepoMock.Object,
            _currentUserMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenRequestDoesNotExist()
    {
        _requestRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_REQUEST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnForbidden_WhenNotReceiver()
    {
        var request = new FriendRequest
        {
            SenderId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(), // different from currentUserId
            Status = FriendRequestStatus.Pending
        };

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_REQUEST_FORBIDDEN);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenAlreadyAccepted()
    {
        var request = new FriendRequest
        {
            SenderId = Guid.NewGuid(),
            ReceiverId = _currentUserId,
            Status = FriendRequestStatus.Accepted
        };

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndCreateFriendAndGroup()
    {
        var senderId = Guid.NewGuid();
        var request = new FriendRequest
        {
            SenderId = senderId,
            ReceiverId = _currentUserId,
            Status = FriendRequestStatus.Pending
        };

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _friendRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _requestRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(FriendRequestStatus.Accepted);
        _groupRepoMock.Verify(r => r.AddAsync(It.IsAny<MessageGroup>(), It.IsAny<CancellationToken>()), Times.Once);
        _friendRepoMock.Verify(r => r.AddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
