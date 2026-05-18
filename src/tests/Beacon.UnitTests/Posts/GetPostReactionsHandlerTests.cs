using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Queries.GetPostReactions;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;
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

public class GetPostReactionsHandlerTests
{
    private readonly Mock<IPostRepository> _postRepo = new();
    private readonly Mock<IPostReactionRepository> _reactionRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepo = new();
    private readonly Mock<IFriendRepository> _friendRepo = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly GetPostReactionsQueryHandler _handler;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();

    public GetPostReactionsHandlerTests()
    {
        _handler = new GetPostReactionsQueryHandler(
            _postRepo.Object,
            _reactionRepo.Object,
            _userRepo.Object,
            _mediaRepo.Object,
            _friendRepo.Object,
            _storage.Object,
            _mapper);

        // Defaults
        _reactionRepo
            .Setup(r => r.GetPagedByPostIdAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PostReaction>(), false));

        _reactionRepo
            .Setup(r => r.GetAllByPostIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        _friendRepo
            .Setup(r => r.AreFriendsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
    }

    private Post BuildPost(
        PostVisibility visibility = PostVisibility.Private,
        PostStatus status = PostStatus.Active,
        Guid? ownerId = null)
        => Post.Create(ownerId ?? _ownerId, Guid.NewGuid(), null, visibility);

    private PostReaction BuildReaction(Guid? userId = null, string icon = "heart")
        => PostReaction.Create(_postId, userId ?? _viewerId, icon);

    // ──────────────────────────────────────────
    // Error cases
    // ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenPostNotFound_ReturnsNotFoundError()
    {
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Post?)null);

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _ownerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenPostDeleted_ReturnsNotFoundError()
    {
        var post = BuildPost();
        post.SoftDelete();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _viewerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenPrivatePostAndNotOwner_ReturnsForbiddenError()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _viewerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);
    }

    [Fact]
    public async Task Handle_WhenFriendsPostAndNotFriend_ReturnsForbiddenError()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _friendRepo
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _viewerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);
    }

    // ──────────────────────────────────────────
    // Happy path
    // ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOwnerViewsPost_ReturnsEmptyListWithZeroSummary()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _ownerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.HasMore.Should().BeFalse();
        result.Value.NextCursor.Should().BeNull();
        result.Value.Summary.TotalCount.Should().Be(0);
        result.Value.Summary.Icons.Should().ContainKeys("heart", "like", "haha", "sad", "wow");
        result.Value.Summary.Icons.Values.Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public async Task Handle_WhenFriendViewsFriendsPost_ReturnsReactionList()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);
        _friendRepo
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reaction = BuildReaction(icon: "heart");
        _reactionRepo
            .Setup(r => r.GetPagedByPostIdAsync(_postId, null, null, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PostReaction> { reaction }, false));
        _reactionRepo
            .Setup(r => r.GetAllByPostIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction> { reaction });

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _viewerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Icon.Should().Be("heart");
        result.Value.Summary.TotalCount.Should().Be(1);
        result.Value.Summary.Icons["heart"].Should().Be(1);
        result.Value.Summary.Icons.Should().ContainKeys("heart", "like", "haha", "sad", "wow");
    }

    [Fact]
    public async Task Handle_WhenIconFilterApplied_ReturnsOnlyMatchingReactions()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var heartReaction = BuildReaction(icon: "heart");
        _reactionRepo
            .Setup(r => r.GetPagedByPostIdAsync(_postId, "heart", null, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PostReaction> { heartReaction }, false));
        _reactionRepo
            .Setup(r => r.GetAllByPostIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction> { heartReaction, BuildReaction(icon: "like") });

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _ownerId, "heart", null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Icon.Should().Be("heart");
        result.Value.Summary.TotalCount.Should().Be(2); // summary is unfiltered
    }

    [Fact]
    public async Task Handle_WhenHasMoreItems_ReturnsNextCursorAndHasMoreTrue()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var reactions = Enumerable.Range(0, 30)
            .Select(_ => BuildReaction())
            .ToList();

        _reactionRepo
            .Setup(r => r.GetPagedByPostIdAsync(_postId, null, null, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reactions, true));

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _ownerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasMore.Should().BeTrue();
        result.Value.NextCursor.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenLastPage_ReturnsNullCursorAndHasMoreFalse()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(post);

        var reaction = BuildReaction();
        _reactionRepo
            .Setup(r => r.GetPagedByPostIdAsync(_postId, null, null, 30, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PostReaction> { reaction }, false));

        var result = await _handler.Handle(
            new GetPostReactionsQuery(_postId, _ownerId, null, null, 30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.HasMore.Should().BeFalse();
        result.Value.NextCursor.Should().BeNull();
    }
}
