using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Posts.Commands.UpdatePost;
using Beacon.Application.Features.Posts.Dtos;
using Beacon.Application.Mappings.Posts;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Entities.Storage;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Domain.IRepository.Storage;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class UpdatePostHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly Mock<IMediaObjectRepository> _mediaRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly PostDtoMapper _mapper = new();
    private readonly UpdatePostCommandHandler _sut;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();
    private readonly Guid _mediaId = Guid.NewGuid();

    public UpdatePostHandlerTests()
    {
        _storageMock
            .Setup(s => s.GeneratePresignedGetUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/media");

        _postRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var media = MediaObject.Create("bucket", "key.jpg", "file.jpg", "image/jpeg", 1024, MediaType.Image,
            uploadedByUserId: _ownerId);
        media.SetStatus(MediaStatus.Ready);
        _mediaRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(media);

        _sut = new UpdatePostCommandHandler(
            _postRepoMock.Object,
            _mediaRepoMock.Object,
            _storageMock.Object,
            _mapper);
    }

    private Post BuildPost(Guid? ownerId = null)
        => Post.Create(ownerId ?? _ownerId, _mediaId, "Original caption", PostVisibility.Private);

    [Fact]
    public async Task Handle_WhenOwnerUpdates_ReturnsSuccess()
    {
        var post = BuildPost();
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpdatePostCommand(
            _postId,
            new UpdatePostRequest { Caption = "Updated", Visibility = "friends" },
            _ownerId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Caption.Should().Be("Updated");
        result.Value.Visibility.Should().Be("friends");
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ReturnsNotFound()
    {
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync((Post?)null);

        var command = new UpdatePostCommand(_postId, new UpdatePostRequest(), _ownerId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenNonOwnerUpdates_ReturnsForbidden()
    {
        var post = BuildPost(_ownerId);
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var otherUser = Guid.NewGuid();
        var command = new UpdatePostCommand(_postId, new UpdatePostRequest { Caption = "Hack" }, otherUser);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_UPDATE_DENIED);
    }

    [Fact]
    public async Task Handle_WhenInvalidVisibility_ReturnsValidationFailure()
    {
        var post = BuildPost();
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new UpdatePostCommand(
            _postId,
            new UpdatePostRequest { Visibility = "public" },
            _ownerId);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be(ErrorCodes.Post.INVALID_VISIBILITY);
    }
}
