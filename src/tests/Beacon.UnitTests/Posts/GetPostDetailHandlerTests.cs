using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Queries.GetPostDetail;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class GetPostDetailHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IPostReactionRepository> _reactionRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IFriendRepository> _friendRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly GetPostDetailQueryHandler _sut;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly Guid _mediaId = Guid.NewGuid();

    public GetPostDetailHandlerTests()
    {
        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/media");

        _reactionRepoMock
            .Setup(r => r.GetByPostIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        _reactionRepoMock
            .Setup(r => r.GetByPostAndUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostReaction?)null);

        var ownerUser = User.Create("owner", "owner@test.com", "hash", "Nguyen", "Owner");
        _userRepoMock
            .Setup(r => r.GetByIdAsync(_ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerUser);

        var media = MediaObject.Create("bucket", "key.jpg", "file.jpg", "image/jpeg", 1024, MediaType.Image,
            uploadedByUserId: _ownerId);
        media.SetStatus(MediaStatus.Ready);
        _mediaRepoMock
            .Setup(r => r.GetByIdAsync(_mediaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        _sut = new GetPostDetailQueryHandler(
            _postRepoMock.Object,
            _reactionRepoMock.Object,
            _mediaRepoMock.Object,
            _userRepoMock.Object,
            _friendRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private Post BuildPost(PostVisibility visibility = PostVisibility.Private, PostStatus status = PostStatus.Active)
    {
        var post = Post.Create(_ownerId, _mediaId, "Test caption", visibility);
        return post;
    }

    [Fact]
    public async Task Handle_WhenOwnerFetches_ReturnsPost()
    {
        var post = BuildPost();
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var query = new GetPostDetailQuery(_postId, _ownerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Caption.Should().Be("Test caption");
        result.Value.Owner.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenFriendFetchesFriendsPost_ReturnsPost()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var query = new GetPostDetailQuery(_postId, _viewerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenStrangerFetchesPrivatePost_ReturnsForbidden()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var query = new GetPostDetailQuery(_postId, _viewerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);
    }

    [Fact]
    public async Task Handle_WhenStrangerFetchesFriendsPost_ReturnsForbidden()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _friendRepoMock
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var query = new GetPostDetailQuery(_postId, _viewerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ReturnsNotFound()
    {
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync((Post?)null);

        var query = new GetPostDetailQuery(_postId, _ownerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenPostIsHidden_ReturnsNotFound()
    {
        // Post.Create always creates with Active status.
        // We create a post and then soft-delete to simulate a hidden/non-active scenario.
        // Since Status can't be changed via public API to Hidden, we check IsDeleted path.
        var post = BuildPost();
        post.SoftDelete(); // IsDeleted = true
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var query = new GetPostDetailQuery(_postId, _ownerId);
        var result = await _sut.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }
}
