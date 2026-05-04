using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Group.Queries.SearchFriends;
using Beacon.Application.Mappings.Group;
using Beacon.Domain.Entities.Group;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.Enums.Group;
using Beacon.Domain.IRepository.Group;
using Beacon.Shared.Common.Pagination;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class SearchFriendsQueryHandlerTests
{
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly FriendMapper _mapper = new();
    private readonly SearchFriendsQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public SearchFriendsQueryHandlerTests()
    {
        _currentUserMock.Setup(s => s.UserId).Returns(_currentUserId);

        _sut = new SearchFriendsQueryHandler(
            _friendRepoMock.Object,
            _currentUserMock.Object,
            _storageMock.Object,
            _mapper);
    }

    [Fact]
    public async Task Handle_WithPhoneSearch_ReturnsCursorPagedFriendDtos()
    {
        var friendUser = User.Create("frienduser", "friend@example.com", "hash", "Nguyen", "Van A", "0912345678");
        var friend = new Friend
        {
            UserId1 = _currentUserId,
            UserId2 = friendUser.Id,
            User1 = User.Create("me", "me@example.com", "hash", "Tran", "Van B"),
            User2 = friendUser,
            Type = FriendType.Normal,
            MessageGroupId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var pagedResult = new CursorPagedResult<Friend>
        {
            Data = [friend],
            Meta = new CursorMeta { Limit = 20, HasMore = false, NextCursor = null }
        };

        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(_currentUserId, "091", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.Handle(new SearchFriendsQuery("091", null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().HaveCount(1);
        result.Value.Data[0].UserId.Should().Be(friendUser.Id);
        result.Value.Data[0].Username.Should().Be("frienduser");
        result.Value.Meta.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenFriendHasAvatar_PopulatesAvatarUrl()
    {
        var avatar = MediaObject.Create("bucket", "avatar.jpg", "avatar.jpg", "image/jpeg", 1024, MediaType.Image);
        var friendUser = User.Create("frienduser", "friend@example.com", "hash", "Nguyen", "Van A", "0912345678");
        friendUser.UpdateAvatar(avatar.Id);

        var friend = new Friend
        {
            UserId1 = _currentUserId,
            UserId2 = friendUser.Id,
            User1 = User.Create("me", "me@example.com", "hash", "Tran", "Van B"),
            User2 = friendUser,
            Type = FriendType.Normal,
            MessageGroupId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        };
        friend.User2.GetType().GetProperty("AvatarMediaObject")!
            .SetValue(friend.User2, avatar);

        var pagedResult = new CursorPagedResult<Friend>
        {
            Data = [friend],
            Meta = new CursorMeta { Limit = 20, HasMore = false }
        };

        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(_currentUserId, "091", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://presigned-url/avatar.jpg");

        var result = await _sut.Handle(new SearchFriendsQuery("091", null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data[0].AvatarUrl.Should().Be("https://presigned-url/avatar.jpg");
    }

    [Fact]
    public async Task Handle_WhenNoMatchingFriends_ReturnsEmptyPage()
    {
        var pagedResult = new CursorPagedResult<Friend>
        {
            Data = [],
            Meta = new CursorMeta { Limit = 20, HasMore = false }
        };

        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(_currentUserId, "999", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _sut.Handle(new SearchFriendsQuery("999", null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Data.Should().BeEmpty();
        result.Value.Meta.HasMore.Should().BeFalse();
        _storageMock.Verify(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LimitOver100_IsClampedTo100()
    {
        var pagedResult = new CursorPagedResult<Friend>
        {
            Data = [],
            Meta = new CursorMeta { Limit = 100, HasMore = false }
        };

        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(_currentUserId, "091", null, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        await _sut.Handle(new SearchFriendsQuery("091", null, 999), CancellationToken.None);

        _friendRepoMock.Verify(
            r => r.SearchByUserAsync(_currentUserId, "091", null, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SearchTerm_PassedExactlyToRepo()
    {
        var pagedResult = new CursorPagedResult<Friend>
        {
            Data = [],
            Meta = new CursorMeta { Limit = 20, HasMore = false }
        };

        _friendRepoMock
            .Setup(r => r.SearchByUserAsync(_currentUserId, "0912345678", null, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        await _sut.Handle(new SearchFriendsQuery("0912345678", null, 20), CancellationToken.None);

        _friendRepoMock.Verify(
            r => r.SearchByUserAsync(_currentUserId, "0912345678", null, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
