using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Queries.SearchUsers;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IFriendRequestRepository> _friendRequestRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly SearchUsersQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SearchUsersQueryHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);

        _sut = new SearchUsersQueryHandler(
            _userRepoMock.Object,
            _friendRepoMock.Object,
            _friendRequestRepoMock.Object,
            _currentUserMock.Object,
            _storageMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUsersFound_ReturnsListWithCorrectStatus()
    {
        var alice = User.Create("alice", "alice@test.com", "hash", "Alice", "Nguyen", "0912345678");
        var bob   = User.Create("bob",   "bob@test.com",   "hash", "Bob",   "Tran",   "0987654321");

        SetupSearch([alice, bob]);
        SetupNoFriendship(alice.Id);
        SetupNoFriendship(bob.Id);

        var result = await _sut.Handle(new SearchUsersQuery("alice"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].FamilyName.Should().Be("Alice");
        result.Value![0].GivenName.Should().Be("Nguyen");
        result.Value[0].FriendshipStatus.Should().Be(FriendshipStatus.None);
    }

    [Fact]
    public async Task Handle_WhenNoUsersFound_ReturnsEmptyList()
    {
        SetupSearch([]);

        var result = await _sut.Handle(new SearchUsersQuery("xyz_notfound"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAlreadyFriends_ReturnsFriendsStatus()
    {
        var target = User.Create("bob", "bob@test.com", "hash", "Bob", "Tran", "0987654321");

        SetupSearch([target]);
        SetupFriendIds([target.Id]);
        SetupNoPendingRequests([target.Id]);

        var result = await _sut.Handle(new SearchUsersQuery("bob"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].FriendshipStatus.Should().Be(FriendshipStatus.Friends);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserSentRequest_ReturnsPendingSentStatus()
    {
        var target = User.Create("carol", "carol@test.com", "hash", "Carol", "Le", "0911111111");
        var pendingReq = FriendRequest.Create(_currentUserId, target.Id);

        SetupSearch([target]);
        SetupFriendIds([]);
        SetupPendingRequests(new Dictionary<Guid, FriendRequest> { [target.Id] = pendingReq });

        var result = await _sut.Handle(new SearchUsersQuery("carol"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].FriendshipStatus.Should().Be(FriendshipStatus.PendingSent);
    }

    [Fact]
    public async Task Handle_WhenTargetSentRequest_ReturnsPendingReceivedStatus()
    {
        var target = User.Create("dave", "dave@test.com", "hash", "Dave", "Pham", "0922222222");
        var pendingReq = FriendRequest.Create(target.Id, _currentUserId);

        SetupSearch([target]);
        SetupFriendIds([]);
        SetupPendingRequests(new Dictionary<Guid, FriendRequest> { [target.Id] = pendingReq });

        var result = await _sut.Handle(new SearchUsersQuery("dave"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value![0].FriendshipStatus.Should().Be(FriendshipStatus.PendingReceived);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetupSearch(List<User> users)
        => _userRepoMock
            .Setup(r => r.SearchByNameOrPhoneAsync(
                It.IsAny<string>(), _currentUserId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

    private void SetupFriendIds(List<Guid> friendIds)
        => _friendRepoMock
            .Setup(r => r.GetFriendIdsAsync(
                _currentUserId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>(friendIds));

    private void SetupNoPendingRequests(List<Guid> targetIds)
        => _friendRequestRepoMock
            .Setup(r => r.GetPendingBetweenBatchAsync(
                _currentUserId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, FriendRequest>());

    private void SetupPendingRequests(Dictionary<Guid, FriendRequest> requests)
        => _friendRequestRepoMock
            .Setup(r => r.GetPendingBetweenBatchAsync(
                _currentUserId, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

    private void SetupNoFriendship(Guid targetId)
    {
        SetupFriendIds([]);
        SetupNoPendingRequests([targetId]);
    }
}
