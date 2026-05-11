using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Queries.ListFriendPresence;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class ListFriendPresenceQueryHandlerTests
{
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly Mock<IUserOnlineTracker> _onlineTrackerMock = new();
    private readonly FriendPresenceMapper _mapper = new();

    [Fact]
    public async Task Handle_ShouldReturnPresenceForFriends()
    {
        var currentUserId = Guid.NewGuid();
        var friendId = Guid.NewGuid();

        var currentUser = User.Create("me", "me@test.com", "hash", "Le", "Me");
        var friendUser = User.Create("friend", "friend@test.com", "hash", "Nguyen", "Friend");
        friendUser.RecordActivity();

        var friend = new Friend
        {
            UserId1 = currentUserId,
            UserId2 = friendId,
            Type = FriendType.Normal,
            CreatedAtUtc = DateTime.UtcNow,
            User1 = currentUser,
            User2 = friendUser
        };

        var paged = new CursorPagedResult<FriendListItem>
        {
            Data = [new FriendListItem(friend, null)],
            Meta = new CursorMeta { Limit = 20, HasMore = false, NextCursor = null }
        };

        _currentUserMock.Setup(c => c.UserId).Returns(currentUserId);
        _friendRepoMock
            .Setup(r => r.ListByUserAsync(currentUserId, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);
        _onlineTrackerMock.Setup(t => t.IsOnline(friendId)).Returns(true);

        var handler = new ListFriendPresenceQueryHandler(
            _friendRepoMock.Object,
            _currentUserMock.Object,
            _storageMock.Object,
            _onlineTrackerMock.Object,
            _mapper);

        var result = await handler.Handle(new ListFriendPresenceQuery(null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].UserId.Should().Be(friendId);
        result.Value.Data[0].IsOnline.Should().BeTrue();
        result.Value.Data[0].LastActiveAtUtc.Should().Be(friendUser.LastActiveAtUtc);
    }
}
