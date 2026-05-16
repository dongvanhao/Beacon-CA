using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Queries.GetFriendsFeed;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class GetFriendsFeedHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IPostReactionRepository> _reactionRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly GetFriendsFeedQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _friendId = Guid.NewGuid();

    public GetFriendsFeedHandlerTests()
    {
        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/media");

        _reactionRepoMock
            .Setup(r => r.GetByPostIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        _reactionRepoMock
            .Setup(r => r.GetByPostIdsForUserAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        var friend = User.Create("friend", "friend@test.com", "hash", "Ho", "Friend");
        _userRepoMock
            .Setup(r => r.GetByIdAsync(_friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(friend);

        _sut = new GetFriendsFeedQueryHandler(
            _postRepoMock.Object,
            _reactionRepoMock.Object,
            _mediaRepoMock.Object,
            _userRepoMock.Object,
            _friendRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private Post BuildFriendPost()
    {
        var mediaId = Guid.NewGuid();
        var post = Post.Create(_friendId, mediaId, "Caption", PostVisibility.Friends);

        var media = MediaObject.Create("bucket", $"key-{mediaId}.jpg", "file.jpg", "image/jpeg", 1024, MediaType.Image,
            uploadedByUserId: _friendId);
        media.SetStatus(MediaStatus.Ready);
        _mediaRepoMock
            .Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        return post;
    }

    private GetFriendsFeedQuery Query(int limit = 20, string? cursor = null)
        => new(_currentUserId, _friendId, cursor, limit);

    [Fact]
    public async Task Handle_WhenNotFriends_ReturnsForbidden()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        _postRepoMock.Verify(
            r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsFriendsPosts_OnlyFromSpecifiedFriend()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var posts = new List<Post> { BuildFriendPost(), BuildFriendPost() };
        _postRepoMock
            .Setup(r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await _sut.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(i => i.OwnerUserId.Should().Be(_friendId));
        _postRepoMock.Verify(
            r => r.GetFriendsPostsAsync(
                It.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == _friendId),
                null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNoPosts_ReturnsEmptyItems()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _postRepoMock
            .Setup(r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>());

        var result = await _sut.Handle(Query(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenHasMoreItems_ReturnsNextCursor()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var posts = Enumerable.Range(0, 3).Select(_ => BuildFriendPost()).ToList();
        _postRepoMock
            .Setup(r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await _sut.Handle(Query(limit: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoMoreItems_ReturnsNullCursor()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _postRepoMock
            .Setup(r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post> { BuildFriendPost() });

        var result = await _sut.Handle(Query(limit: 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithLimitOverMax_ClampsTo50()
    {
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_currentUserId, _friendId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _postRepoMock
            .Setup(r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>());

        var result = await _sut.Handle(Query(limit: 999), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _postRepoMock.Verify(
            r => r.GetFriendsPostsAsync(It.IsAny<List<Guid>>(), null, null, 51, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
