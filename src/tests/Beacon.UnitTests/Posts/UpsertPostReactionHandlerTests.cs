using Beacon.Application.Features.Posts.Commands.UpsertReaction;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Group;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class UpsertPostReactionHandlerTests
{
    private readonly Mock<IPostRepository> _postRepo = new();
    private readonly Mock<IPostReactionRepository> _reactionRepo = new();
    private readonly Mock<IFriendRepository> _friendRepo = new();
    private readonly UpsertPostReactionCommandHandler _handler;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();

    public UpsertPostReactionHandlerTests()
    {
        _handler = new UpsertPostReactionCommandHandler(
            _postRepo.Object,
            _reactionRepo.Object,
            _friendRepo.Object);

        // Default: no existing reaction
        _reactionRepo
            .Setup(r => r.GetByPostAndUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostReaction?)null);

        // Default: reactions list is empty
        _reactionRepo
            .Setup(r => r.GetByPostIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        // Default: AddAsync is a no-op task
        _reactionRepo
            .Setup(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: SaveChangesAsync is a no-op task
        _reactionRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private Post BuildPost(PostVisibility visibility = PostVisibility.Private, PostStatus status = PostStatus.Active)
        => Post.Create(_ownerId, Guid.NewGuid(), "Test caption", visibility);

    [Fact]
    public async Task Handle_WhenNoExistingReaction_CreatesNew()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpsertPostReactionCommand(_postId, "heart", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MyReaction!.Icon.Should().Be("heart");

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _reactionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingReactionDifferentIcon_UpdatesIcon()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existing = PostReaction.Create(_postId, _ownerId, "heart");
        _reactionRepo
            .Setup(r => r.GetByPostAndUserAsync(_postId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpsertPostReactionCommand(_postId, "haha", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MyReaction!.Icon.Should().Be("haha");
        existing.Icon.Should().Be("haha");

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _reactionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingReactionSameIcon_NoOp()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existing = PostReaction.Create(_postId, _ownerId, "heart");
        _reactionRepo
            .Setup(r => r.GetByPostAndUserAsync(_postId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpsertPostReactionCommand(_postId, "heart", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MyReaction!.Icon.Should().Be("heart");

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _reactionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ReturnsNotFound()
    {
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync((Post?)null);

        var command = new UpsertPostReactionCommand(_postId, "heart", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenPostIsHidden_ReturnsNotFound()
    {
        var post = BuildPost();
        post.SoftDelete(); // IsDeleted = true
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpsertPostReactionCommand(_postId, "heart", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenUserHasNoPermissionToViewPrivatePost_ReturnsForbidden()
    {
        var post = BuildPost(PostVisibility.Private);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        // _viewerId is not the owner
        var command = new UpsertPostReactionCommand(_postId, "heart", _viewerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);
    }

    [Fact]
    public async Task Handle_WhenIconInvalid_ReturnsFailure()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpsertPostReactionCommand(_postId, "🎉", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Reaction.INVALID_REACTION_ICON);
    }
}
