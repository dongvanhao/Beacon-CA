using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Commands.UpsertReaction;
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
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Posts;

public class UpsertPostReactionHandlerTests
{
    private readonly Mock<IPostRepository> _postRepo = new();
    private readonly Mock<IPostReactionRepository> _reactionRepo = new();
    private readonly Mock<IFriendRepository> _friendRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepo = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<IFcmService> _fcmService = new();
    private readonly Mock<ILogger<UpsertPostReactionCommandHandler>> _logger = new();
    private readonly UpsertPostReactionCommandHandler _handler;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();

    public UpsertPostReactionHandlerTests()
    {
        _handler = new UpsertPostReactionCommandHandler(
            _postRepo.Object,
            _reactionRepo.Object,
            _friendRepo.Object,
            _userRepo.Object,
            _mediaRepo.Object,
            _storage.Object,
            _fcmService.Object,
            _logger.Object);

        // Default: no existing reaction
        _reactionRepo
            .Setup(r => r.GetByPostAndUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostReaction?)null);

        // Default: reactions list is empty
        _reactionRepo
            .Setup(r => r.GetAllByPostIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PostReaction>());

        // Default: AddAsync is a no-op task
        _reactionRepo
            .Setup(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default: SaveChangesAsync is a no-op task
        _reactionRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fcmService
            .Setup(n => n.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _friendRepo
            .Setup(r => r.AreFriendsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _userRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
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
        _fcmService.Verify(n => n.SendToUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExistingReactionDifferentIcon_AppendsIcon()
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
        result.Value!.MyReaction!.Icon.Should().Be("heart - haha");
        existing.Icon.Should().Be("heart - haha");

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _reactionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fcmService.Verify(n => n.SendToUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenExistingReactionSameIcon_AppendsIcon()
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
        result.Value!.MyReaction!.Icon.Should().Be("heart - heart");

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _reactionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingReactionAlreadyHasThreeIcons_DropsOldestIcon()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var existing = PostReaction.Create(_postId, _ownerId, "heart");
        existing.AppendIcon("haha");
        existing.AppendIcon("like");
        _reactionRepo
            .Setup(r => r.GetByPostAndUserAsync(_postId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new UpsertPostReactionCommand(_postId, "wow", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MyReaction!.Icon.Should().Be("haha - like - wow");
        existing.Icon.Should().Be("haha - like - wow");
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
    public async Task Handle_WhenFriendReactsToFriendsPost_CreatesReactionAndNotifiesOwner()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _friendRepo
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var reactor = User.Create("reactor", "reactor@test.com", "hash", "Tran", "Reactor");
        var avatar = MediaObject.Create(
            "bucket",
            "avatars/reactor.jpg",
            "reactor.jpg",
            "image/jpeg",
            123,
            MediaType.Image,
            uploadedByUserId: _viewerId);
        reactor.UpdateAvatar(avatar.Id);
        _userRepo
            .Setup(r => r.GetByIdAsync(_viewerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reactor);
        _mediaRepo
            .Setup(r => r.GetByIdAsync(avatar.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(avatar);
        _storage
            .Setup(s => s.GeneratePresignedGetUrlAsync("avatars/reactor.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.test/avatar.jpg");

        var command = new UpsertPostReactionCommand(_postId, "😊", _viewerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MyReaction!.Icon.Should().Be("😊");

        _reactionRepo.Verify(r => r.AddAsync(
            It.Is<PostReaction>(reaction => reaction.PostId == _postId && reaction.UserId == _viewerId && reaction.Icon == "😊"),
            It.IsAny<CancellationToken>()), Times.Once);
        _fcmService.Verify(n => n.SendToUserAsync(
            _ownerId,
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("😊")),
            It.Is<Dictionary<string, string>>(data =>
                data["type"] == "POST_REACTION"
                && data["postId"] == _postId.ToString()
                && data["reactorUserId"] == _viewerId.ToString()
                && data["reactorDisplayName"] == "Tran Reactor"
                && data["reactorAvatarUrl"] == "https://cdn.test/avatar.jpg"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNonFriendReactsToFriendsPost_ReturnsForbidden()
    {
        var post = BuildPost(PostVisibility.Friends);
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);
        _friendRepo
            .Setup(r => r.AreFriendsAsync(_viewerId, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new UpsertPostReactionCommand(_postId, "heart", _viewerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_ACCESS_DENIED);

        _reactionRepo.Verify(r => r.AddAsync(It.IsAny<PostReaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _fcmService.Verify(n => n.SendToUserAsync(
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenIconContainsSeparator_ReturnsFailure()
    {
        var post = BuildPost();
        _postRepo.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpsertPostReactionCommand(_postId, "heart - haha", _ownerId);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(ErrorCodes.Reaction.INVALID_REACTION_ICON);
    }
}
