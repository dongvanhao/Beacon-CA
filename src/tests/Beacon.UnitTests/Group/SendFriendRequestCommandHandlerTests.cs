using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Commands.SendFriendRequest;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class SendFriendRequestCommandHandlerTests
{
    private readonly Mock<IFriendRequestRepository> _requestRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<INotificationRepository> _notifRepoMock = new();
    private readonly Mock<IRealtimeNotifier> _notifierMock = new();
    private readonly FriendRequestMapper _mapper = new();
    private readonly SendFriendRequestCommandHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SendFriendRequestCommandHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);
        _currentUserMock.Setup(s => s.Username).Returns("sender");
        _currentUserMock.Setup(s => s.FamilyName).Returns("Nguyen");
        _currentUserMock.Setup(s => s.GivenName).Returns("Test");

        // Default: receiver exists
        _userRepoMock
            .Setup(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new SendFriendRequestCommandHandler(
            _requestRepoMock.Object,
            _friendRepoMock.Object,
            _userRepoMock.Object,
            _currentUserMock.Object,
            _notifRepoMock.Object,
            _notifierMock.Object,
            _mapper);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidationError_WhenSelfRequest()
    {
        var command = new SendFriendRequestCommand(_currentUserId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be(ErrorCodes.Friend.SELF_FRIEND_REQUEST);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenDuplicatePendingRequest()
    {
        var receiverId = Guid.NewGuid();
        _requestRepoMock
            .Setup(r => r.HasPendingBetweenAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new SendFriendRequestCommand(receiverId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Friend.FRIEND_REQUEST_DUPLICATE);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenAlreadyFriends()
    {
        var receiverId = Guid.NewGuid();
        _requestRepoMock
            .Setup(r => r.HasPendingBetweenAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new SendFriendRequestCommand(receiverId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be(ErrorCodes.Friend.ALREADY_FRIENDS);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenValidRequest()
    {
        var receiverId = Guid.NewGuid();
        _requestRepoMock
            .Setup(r => r.HasPendingBetweenAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _requestRepoMock
            .Setup(r => r.AddAsync(It.IsAny<FriendRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _requestRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new SendFriendRequestCommand(receiverId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SenderId.Should().Be(_currentUserId);
        _requestRepoMock.Verify(r => r.AddAsync(It.IsAny<FriendRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCallNotifyUserAsync_WhenFriendRequestCreated()
    {
        var receiverId = Guid.NewGuid();
        _requestRepoMock
            .Setup(r => r.HasPendingBetweenAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Handle(new SendFriendRequestCommand(receiverId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(
            n => n.NotifyUserAsync(
                receiverId,
                It.Is<NotificationPayload>(p => p.Type == nameof(NotificationType.FriendRequest)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldSaveNotification_WhenFriendRequestCreated()
    {
        var receiverId = Guid.NewGuid();
        _requestRepoMock
            .Setup(r => r.HasPendingBetweenAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, receiverId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Handle(new SendFriendRequestCommand(receiverId), CancellationToken.None);

        _notifRepoMock.Verify(
            r => r.AddAsync(
                It.Is<Notification>(n =>
                    n.ReceiverUserId == receiverId &&
                    n.Type == NotificationType.FriendRequest),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notifRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
