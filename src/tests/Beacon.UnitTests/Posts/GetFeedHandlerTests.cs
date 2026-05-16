using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Queries.GetFeed;
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

public class GetFeedHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IPostReactionRepository> _reactionRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly GetFeedQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _ownerId;

    public GetFeedHandlerTests()
    {
        _ownerId = _currentUserId;

        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/media");

        _friendRepoMock
            .Setup(r => r.ListFriendIdsAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        _reactionRepoMock
            .Setup(r => r.GetByPostIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        _reactionRepoMock
            .Setup(r => r.GetByPostIdsForUserAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        var user = User.Create("current", "current@test.com", "hash", "Ho", "Current");
        _userRepoMock
            .Setup(r => r.GetByIdAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sut = new GetFeedQueryHandler(
            _postRepoMock.Object,
            _reactionRepoMock.Object,
            _mediaRepoMock.Object,
            _userRepoMock.Object,
            _friendRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private Post BuildPost(Guid? ownerId = null, PostVisibility visibility = PostVisibility.Private)
    {
        var mediaId = Guid.NewGuid();
        var post = Post.Create(ownerId ?? _currentUserId, mediaId, "Caption", visibility);

        var media = MediaObject.Create("bucket", $"key-{mediaId}.jpg", "file.jpg", "image/jpeg", 1024, MediaType.Image,
            uploadedByUserId: ownerId ?? _currentUserId);
        media.SetStatus(MediaStatus.Ready);
        _mediaRepoMock
            .Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        return post;
    }

    [Fact]
    public async Task Handle_ReturnsOwnPostsIncludingPrivate()
    {
        var posts = new List<Post> { BuildPost(visibility: PostVisibility.Private) };
        _postRepoMock
            .Setup(r => r.GetFeedAsync(_currentUserId, It.IsAny<List<Guid>>(), null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var query = new GetFeedQuery(_currentUserId, null, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithCursor_ReturnsNextPage()
    {
        // Return limit+1 items to trigger hasMore
        var posts = Enumerable.Range(0, 3).Select(_ => BuildPost()).ToList();
        _postRepoMock
            .Setup(r => r.GetFeedAsync(_currentUserId, It.IsAny<List<Guid>>(), null, null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var query = new GetFeedQuery(_currentUserId, null, 2); // limit=2, fetch 3
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoMoreItems_ReturnsNullCursor()
    {
        var posts = new List<Post> { BuildPost() };
        _postRepoMock
            .Setup(r => r.GetFeedAsync(_currentUserId, It.IsAny<List<Guid>>(), null, null, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var query = new GetFeedQuery(_currentUserId, null, 20);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithLimitOverMax_ClampsTo50()
    {
        _postRepoMock
            .Setup(r => r.GetFeedAsync(_currentUserId, It.IsAny<List<Guid>>(), null, null, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>());

        var query = new GetFeedQuery(_currentUserId, null, 200); // will be clamped to 50, fetches 51
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Verify that repo was called with limit+1 = 51 (clamped 50 + 1)
        _postRepoMock.Verify(
            r => r.GetFeedAsync(_currentUserId, It.IsAny<List<Guid>>(), null, null, 51, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
