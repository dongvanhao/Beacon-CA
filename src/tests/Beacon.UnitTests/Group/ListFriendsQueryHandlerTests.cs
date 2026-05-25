using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Queries.ListFriends;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class ListFriendsQueryHandlerTests
{
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly FriendMapper _mapper = new();

    [Fact]
    public async Task Handle_WhenSearchIsEmpty_ListsFriendsNormally()
    {
        var currentUser = User.Create("me", "me@test.com", "hash", "Le", "Me");
        var friendUser = User.Create("friend", "friend@test.com", "hash", "Nguyen", "Friend");
        var paged = CreatePagedFriend(currentUser, friendUser);

        _currentUserMock.Setup(c => c.UserId).Returns(currentUser.Id);
        _friendRepoMock
            .Setup(r => r.ListByUserAsync(currentUser.Id, null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var handler = CreateHandler();

        var result = await handler.Handle(new ListFriendsQuery(null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].UserId.Should().Be(friendUser.Id);
        _friendRepoMock.Verify(
            r => r.SearchByUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSearchHasValue_SearchesFriends()
    {
        var currentUser = User.Create("me", "me@test.com", "hash", "Le", "Me");
        var friendUser = User.Create("friend", "friend@test.com", "hash", "Nguyen", "Hao");
        var paged = CreatePagedFriend(currentUser, friendUser);

        _currentUserMock.Setup(c => c.UserId).Returns(currentUser.Id);
        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(currentUser.Id, "nguyenhao", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var handler = CreateHandler();

        var result = await handler.Handle(new ListFriendsQuery(null, 20, "nguyenhao"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].UserId.Should().Be(friendUser.Id);
        _friendRepoMock.Verify(r => r.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ListFriendsQueryHandler CreateHandler()
        => new(_friendRepoMock.Object, _currentUserMock.Object, _storageMock.Object, _mapper);

    private static CursorPagedResult<FriendListItem> CreatePagedFriend(User currentUser, User friendUser)
    {
        var friend = new Friend
        {
            UserId1 = currentUser.Id,
            UserId2 = friendUser.Id,
            Type = FriendType.Normal,
            CreatedAtUtc = DateTime.UtcNow,
            User1 = currentUser,
            User2 = friendUser
        };

        return new CursorPagedResult<FriendListItem>
        {
            Data = [new FriendListItem(friend, null)],
            Meta = new CursorMeta { Limit = 20, HasMore = false, NextCursor = null }
        };
    }
}
