using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Queries.FindUserByPhone;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class FindUserByPhoneQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IFriendRequestRepository> _friendRequestRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly FindUserByPhoneQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public FindUserByPhoneQueryHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);

        _sut = new FindUserByPhoneQueryHandler(
            _userRepoMock.Object,
            _friendRepoMock.Object,
            _friendRequestRepoMock.Object,
            _currentUserMock.Object,
            _storageMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserFoundAndNoRelationship_ReturnsNoneStatus()
    {
        var target = User.Create("alice", "alice@test.com", "hash", "Alice", "Nguyen", "0912345678");

        _userRepoMock
            .Setup(r => r.GetByPhoneAsync("0912345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRequestRepoMock
            .Setup(r => r.GetPendingBetweenAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FriendRequest?)null);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0912345678"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.UserId.Should().Be(target.Id);
        result.Value.Username.Should().Be("alice");
        result.Value.FriendshipStatus.Should().Be(FriendshipStatus.None);
        result.Value.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenAlreadyFriends_ReturnsFriendsStatus()
    {
        var target = User.Create("bob", "bob@test.com", "hash", "Bob", "Tran", "0987654321");

        _userRepoMock
            .Setup(r => r.GetByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0987654321"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FriendshipStatus.Should().Be(FriendshipStatus.Friends);
        _friendRequestRepoMock.Verify(
            r => r.GetPendingBetweenAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserSentRequest_ReturnsPendingSentStatus()
    {
        var target = User.Create("carol", "carol@test.com", "hash", "Carol", "Le", "0911111111");
        var pendingReq = new FriendRequest
        {
            SenderId = _currentUserId,
            ReceiverId = target.Id,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _userRepoMock
            .Setup(r => r.GetByPhoneAsync("0911111111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRequestRepoMock
            .Setup(r => r.GetPendingBetweenAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingReq);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0911111111"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FriendshipStatus.Should().Be(FriendshipStatus.PendingSent);
    }

    [Fact]
    public async Task Handle_WhenTargetSentRequest_ReturnsPendingReceivedStatus()
    {
        var target = User.Create("dave", "dave@test.com", "hash", "Dave", "Pham", "0922222222");
        var pendingReq = new FriendRequest
        {
            SenderId = target.Id,
            ReceiverId = _currentUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _userRepoMock
            .Setup(r => r.GetByPhoneAsync("0922222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _friendRequestRepoMock
            .Setup(r => r.GetPendingBetweenAsync(_currentUserId, target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingReq);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0922222222"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FriendshipStatus.Should().Be(FriendshipStatus.PendingReceived);
    }

    [Fact]
    public async Task Handle_WhenPhoneNotFound_ReturnsNotFoundError()
    {
        _userRepoMock
            .Setup(r => r.GetByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0999999999"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Identity.USER_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenSearchingOwnPhone_ReturnsNotFoundError()
    {
        var self = User.Create("me", "me@test.com", "hash", "Me", "Self", "0900000000");
        self.GetType().GetProperty("Id")!.SetValue(self, _currentUserId);

        _userRepoMock
            .Setup(r => r.GetByPhoneAsync("0900000000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(self);

        var result = await _sut.Handle(new FindUserByPhoneQuery("0900000000"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Identity.USER_NOT_FOUND);
    }
}
