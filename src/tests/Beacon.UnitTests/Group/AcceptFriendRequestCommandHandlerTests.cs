using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Commands.AcceptFriendRequest;
using Beacon.Application.Features.Group.Events;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using MediatR;
using Moq;

namespace Beacon.UnitTests.Group;

public class AcceptFriendRequestCommandHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requestRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly AcceptFriendRequestCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public AcceptFriendRequestCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.FamilyName).Returns("Tran");
        _currentUserMock.Setup(s => s.GivenName).Returns("Accepter");

        _notificationServiceMock
            .Setup(s => s.CreateAndDeliverAsync(
                It.IsAny<Guid>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new AcceptFriendRequestCommandHandler(
            _requestRepoMock.Object,
            _friendRepoMock.Object,
            _mediatorMock.Object,
            _currentUserMock.Object,
            _notificationServiceMock.Object);
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
        var senderId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var request = FriendRequest.Create(senderId, otherId); // receiver = otherId, not currentUser

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
        var senderId = Guid.NewGuid();
        var request = FriendRequest.Create(senderId, _currentUserId);
        request.Accept(); // simulate already accepted

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_REQUEST_NOT_PENDING);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_AndPublishEventAndAcceptRequest()
    {
        var senderId = Guid.NewGuid();
        var request = FriendRequest.Create(senderId, _currentUserId);

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _friendRepoMock
            .Setup(r => r.TryAddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _requestRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mediatorMock
            .Setup(m => m.Publish(It.IsAny<FriendRequestAcceptedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        request.Status.Should().Be(FriendRequestStatus.Accepted);
        _friendRepoMock.Verify(r => r.TryAddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(m => m.Publish(
            It.Is<FriendRequestAcceptedEvent>(e => e.SenderId == senderId && e.ReceiverId == _currentUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenFriendAlreadyExists()
    {
        var senderId = Guid.NewGuid();
        var request = FriendRequest.Create(senderId, _currentUserId);

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _friendRepoMock
            .Setup(r => r.TryAddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // unique constraint hit

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        _mediatorMock.Verify(m => m.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotifyOriginalSender_WhenFriendRequestAccepted()
    {
        var initiatorId = Guid.NewGuid();
        var request = FriendRequest.Create(initiatorId, _currentUserId);

        _requestRepoMock
            .Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        _friendRepoMock
            .Setup(r => r.TryAddAsync(It.IsAny<Friend>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _requestRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mediatorMock
            .Setup(m => m.Publish(It.IsAny<FriendRequestAcceptedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new AcceptFriendRequestCommand(request.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notificationServiceMock.Verify(
            s => s.CreateAndDeliverAsync(
                initiatorId,
                NotificationType.FriendAccepted,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
