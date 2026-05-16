using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Queries.GetMyPosts;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class GetMyPostsHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IPostReactionRepository> _reactionRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly GetMyPostsQueryHandler _sut;

    private readonly Guid _currentUserId = Guid.NewGuid();

    public GetMyPostsHandlerTests()
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

        var user = User.Create("me", "me@test.com", "hash", "Ho", "Me");
        _userRepoMock
            .Setup(r => r.GetByIdAsync(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _sut = new GetMyPostsQueryHandler(
            _postRepoMock.Object,
            _reactionRepoMock.Object,
            _mediaRepoMock.Object,
            _userRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private Post BuildPost(PostVisibility visibility = PostVisibility.Friends)
    {
        var mediaId = Guid.NewGuid();
        var post = Post.Create(_currentUserId, mediaId, "Caption", visibility);

        var media = MediaObject.Create("bucket", $"key-{mediaId}.jpg", "file.jpg", "image/jpeg", 1024, MediaType.Image,
            uploadedByUserId: _currentUserId);
        media.SetStatus(MediaStatus.Ready);
        _mediaRepoMock
            .Setup(r => r.GetByIdAsync(mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        return post;
    }

    [Fact]
    public async Task Handle_ReturnsOwnPosts_IncludingPrivate()
    {
        var posts = new List<Post>
        {
            BuildPost(PostVisibility.Private),
            BuildPost(PostVisibility.Friends)
        };
        _postRepoMock
            .Setup(r => r.GetMyPostsAsync(_currentUserId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await _sut.Handle(new GetMyPostsQuery(_currentUserId, null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().AllSatisfy(i => i.OwnerUserId.Should().Be(_currentUserId));
    }

    [Fact]
    public async Task Handle_WhenNoPosts_ReturnsEmptyItems()
    {
        _postRepoMock
            .Setup(r => r.GetMyPostsAsync(_currentUserId, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>());

        var result = await _sut.Handle(new GetMyPostsQuery(_currentUserId, null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenHasMoreItems_ReturnsNextCursor()
    {
        var posts = Enumerable.Range(0, 3).Select(_ => BuildPost()).ToList();
        _postRepoMock
            .Setup(r => r.GetMyPostsAsync(_currentUserId, null, null, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await _sut.Handle(new GetMyPostsQuery(_currentUserId, null, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WhenNoMoreItems_ReturnsNullCursor()
    {
        var posts = new List<Post> { BuildPost() };
        _postRepoMock
            .Setup(r => r.GetMyPostsAsync(_currentUserId, null, null, 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(posts);

        var result = await _sut.Handle(new GetMyPostsQuery(_currentUserId, null, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithLimitOverMax_ClampsTo50()
    {
        _postRepoMock
            .Setup(r => r.GetMyPostsAsync(_currentUserId, null, null, 51, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Post>());

        var result = await _sut.Handle(new GetMyPostsQuery(_currentUserId, null, 200), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _postRepoMock.Verify(
            r => r.GetMyPostsAsync(_currentUserId, null, null, 51, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
