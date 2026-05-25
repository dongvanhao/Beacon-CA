using Beacon.Application.Features.Identity.Commands;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Domain.IRepository.Identity;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserDeviceTokenRepository> _deviceTokenRepo = new();
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _handler = new LogoutCommandHandler(_userRepo.Object, _deviceTokenRepo.Object);
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
        _deviceTokenRepo.Verify(x => x.GetActiveByUserIdAsync(It.IsAny<Guid>(), default), Times.Never);
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
        _deviceTokenRepo
            .Setup(x => x.GetActiveByUserIdAsync(token.UserId, default))
            .ReturnsAsync([]);

        var command = new LogoutCommand("valid-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
        token.RevokedAtUtc.Should().NotBeNull();
        _deviceTokenRepo.Verify(x => x.GetActiveByUserIdAsync(token.UserId, default), Times.Once);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidToken_RevokesActiveDeviceTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = RefreshToken.Create(
            userId: userId,
            token: "valid-token",
            expiresAtUtc: DateTime.UtcNow.AddDays(7));
        var deviceToken = UserDeviceToken.Create(userId, "fcm-token", Domain.Enums.Identity.DevicePlatform.Android);

        _userRepo
            .Setup(x => x.GetActiveRefreshTokenAsync("valid-token", default))
            .ReturnsAsync(token);
        _deviceTokenRepo
            .Setup(x => x.GetActiveByUserIdAsync(userId, default))
            .ReturnsAsync([deviceToken]);

        var command = new LogoutCommand("valid-token");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deviceToken.IsActive.Should().BeFalse();
        deviceToken.RevokedAtUtc.Should().NotBeNull();
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
        _deviceTokenRepo
            .Setup(x => x.GetActiveByUserIdAsync(token.UserId, default))
            .ReturnsAsync([]);

        var command = new LogoutCommand("valid-token");

        // Act
        await _handler.Handle(command, default);

        // Assert — logout chỉ cần revoke token, không cần load User
        _userRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
        _userRepo.Verify(x => x.GetByUsernameAsync(It.IsAny<string>(), default), Times.Never);
    }
}
