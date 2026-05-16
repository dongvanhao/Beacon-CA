using Beacon.Application.Features.Posts.Commands.DeletePost;
using Beacon.Domain.Entities.Posts;
using Beacon.Domain.Enums;
using Beacon.Domain.IRepository.Posts;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Posts;

public class DeletePostHandlerTests
{
    private readonly Mock<IPostRepository> _postRepoMock = new();
    private readonly DeletePostCommandHandler _sut;

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _postId = Guid.NewGuid();

    public DeletePostHandlerTests()
    {
        _postRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new DeletePostCommandHandler(_postRepoMock.Object);
    }

    private Post BuildPost(Guid? ownerId = null)
        => Post.Create(ownerId ?? _ownerId, Guid.NewGuid(), null, PostVisibility.Private);

    [Fact]
    public async Task Handle_WhenOwnerDeletes_SetsDeletedAtUtc()
    {
        var post = BuildPost();
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var command = new DeletePostCommand(_postId, _ownerId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        post.DeletedAtUtc.Should().NotBeNull();
        post.IsDeleted.Should().BeTrue();
        _postRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPostNotFound_ReturnsNotFound()
    {
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync((Post?)null);

        var command = new DeletePostCommand(_postId, _ownerId);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_WhenNonOwnerDeletes_ReturnsForbidden()
    {
        var post = BuildPost(_ownerId);
        _postRepoMock.Setup(r => r.GetByIdAsync(_postId, It.IsAny<CancellationToken>())).ReturnsAsync(post);

        var otherUser = Guid.NewGuid();
        var command = new DeletePostCommand(_postId, otherUser);
        var result = await _sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be(ErrorCodes.Post.POST_DELETE_DENIED);
    }
}
