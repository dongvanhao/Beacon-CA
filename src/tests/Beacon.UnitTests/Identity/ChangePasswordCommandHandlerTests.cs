using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands.ChangePassword;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class ChangePasswordCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly ChangePasswordCommandHandler _handler;

    private static readonly Guid UserId = Guid.NewGuid();
    private const string CorrectPassword = "OldPass123!";
    private const string NewPassword = "NewPass456@";

    public ChangePasswordCommandHandlerTests()
    {
        _currentUser.Setup(x => x.UserId).Returns(UserId);

        _handler = new ChangePasswordCommandHandler(_userRepo.Object, _currentUser.Object);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);
        _userRepo.Setup(x => x.GetActiveRefreshTokensByUserIdAsync(UserId, default))
                 .ReturnsAsync(new List<RefreshToken>());

        var command = BuildCommand(CorrectPassword, NewPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCredentials_UpdatesPasswordHash()
    {
        // Arrange
        var user = CreateActiveUser();
        var oldHash = user.PasswordHash;

        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);
        _userRepo.Setup(x => x.GetActiveRefreshTokensByUserIdAsync(UserId, default))
                 .ReturnsAsync(new List<RefreshToken>());

        // Act
        await _handler.Handle(BuildCommand(CorrectPassword, NewPassword), default);

        // Assert
        user.PasswordHash.Should().NotBe(oldHash);
        BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidCredentials_RevokesAllActiveRefreshTokens()
    {
        // Arrange
        var user = CreateActiveUser();
        var tokens = new List<RefreshToken>
        {
            RefreshToken.Create(UserId, "token-1", DateTime.UtcNow.AddDays(7)),
            RefreshToken.Create(UserId, "token-2", DateTime.UtcNow.AddDays(7)),
        };

        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);
        _userRepo.Setup(x => x.GetActiveRefreshTokensByUserIdAsync(UserId, default))
                 .ReturnsAsync(tokens);

        // Act
        await _handler.Handle(BuildCommand(CorrectPassword, NewPassword), default);

        // Assert
        tokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
        tokens.Should().AllSatisfy(t => t.RevokedAtUtc.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_WithValidCredentials_SavesChangesOnce()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);
        _userRepo.Setup(x => x.GetActiveRefreshTokensByUserIdAsync(UserId, default))
                 .ReturnsAsync(new List<RefreshToken>());

        // Act
        await _handler.Handle(BuildCommand(CorrectPassword, NewPassword), default);

        // Assert
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ReturnsUnauthorized()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);

        var command = BuildCommand("WrongPassword!", NewPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be(ErrorCodes.Identity.INVALID_CURRENT_PASSWORD);
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_DoesNotSave()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);

        // Act
        await _handler.Handle(BuildCommand("WrongPassword!", NewPassword), default);

        // Assert
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsUnauthorized()
    {
        // Arrange
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(BuildCommand(CorrectPassword, NewPassword), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be(ErrorCodes.Identity.TOKEN_INVALID);
    }

    [Fact]
    public async Task Handle_WhenUserInactive_ReturnsUnauthorized()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(BuildCommand(CorrectPassword, NewPassword), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be(ErrorCodes.Identity.ACCOUNT_INACTIVE);
    }

    [Fact]
    public async Task Handle_WhenNewPasswordSameAsCurrent_ReturnsValidationError()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(BuildCommand(CorrectPassword, CorrectPassword), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be(ErrorCodes.Identity.NEW_PASSWORD_SAME_AS_OLD);
    }

    [Fact]
    public async Task Handle_WhenNewPasswordSameAsCurrent_DoesNotSave()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo.Setup(x => x.GetByIdAsync(UserId, default)).ReturnsAsync(user);

        // Act
        await _handler.Handle(BuildCommand(CorrectPassword, CorrectPassword), default);

        // Assert
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Never);
    }

    private static User CreateActiveUser()
        => User.Create(
            username: "testuser",
            email: "test@example.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword(CorrectPassword),
            familyName: "Test",
            givenName: "User");

    private static ChangePasswordCommand BuildCommand(string currentPassword, string newPassword)
        => new(new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });
}
