using Beacon.Application.Features.Identity.Commands;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(_userRepo.Object);
    }

    [Fact]
    public async Task Handle_WhenTokenNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        _userRepo
            .Setup(x => x.GetActiveRefreshTokenAsync("invalid-token", default))
            .ReturnsAsync((RefreshToken?)null);

        var command = new LogoutCommand("invalid-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidToken_RevokesTokenAndReturnsSuccess()
    {
        // Arrange
        var token = RefreshToken.Create(
            userId: Guid.NewGuid(),
            token: "valid-token",
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        _userRepo
            .Setup(x => x.GetActiveRefreshTokenAsync("valid-token", default))
            .ReturnsAsync(token);

        var command = new LogoutCommand("valid-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        token.RevokedAtUtc.Should().NotBeNull();
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidToken_DoesNotQueryRepositoryForUser()
    {
        // Arrange
        var token = RefreshToken.Create(
            userId: Guid.NewGuid(),
            token: "valid-token",
            expiresAtUtc: DateTime.UtcNow.AddDays(7));

        _userRepo
            .Setup(x => x.GetActiveRefreshTokenAsync("valid-token", default))
            .ReturnsAsync(token);

        var command = new LogoutCommand("valid-token");

        // Act
        await _handler.Handle(command, default);

        // Assert — logout chỉ cần revoke token, không cần load User
        _userRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _userRepo.Verify(x => x.GetByUsernameAsync(It.IsAny<string>(), default), Times.Never);
    }
}
