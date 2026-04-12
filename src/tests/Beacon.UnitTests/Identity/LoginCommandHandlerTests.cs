using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly LoginCommandHandler _handler;

    private static readonly DateTime AccessExpiry = DateTime.UtcNow.AddMinutes(15);
    private static readonly DateTime RefreshExpiry = DateTime.UtcNow.AddDays(7);

    private const string CorrectPassword = "Password123";

    public LoginCommandHandlerTests()
    {
        _jwtService
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns(("access-token", AccessExpiry));
        _jwtService
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("refresh-token", RefreshExpiry));

        _handler = new LoginCommandHandler(_userRepo.Object, _jwtService.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailNotFound_ReturnsUnauthorizedResult()
    {
        // Arrange
        _userRepo
            .Setup(x => x.GetByEmailAsync("notfound@example.com", default))
            .ReturnsAsync((User?)null);

        var command = BuildCommand("notfound@example.com");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAccountIsInactive_ReturnsUnauthorizedResult()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();

        _userRepo
            .Setup(x => x.GetByEmailAsync("user@example.com", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsWrong_ReturnsUnauthorizedResult()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByEmailAsync("user@example.com", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: "WrongPassword");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_ReturnsAuthResponseWithTokens()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByEmailAsync("user@example.com", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("user@example.com");
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.AccessTokenExpiresAt.Should().Be(AccessExpiry);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_SavesRefreshTokenAndRecordsLogin()
    {
        // Arrange
        var user = CreateActiveUser();
        var loginTimeBefore = DateTime.UtcNow;

        _userRepo
            .Setup(x => x.GetByEmailAsync("user@example.com", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _userRepo.Verify(x => x.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Once);
        user.LastLoginAtUtc.Should().NotBeNull();
        user.LastLoginAtUtc.Should().BeOnOrAfter(loginTimeBefore);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_RefreshTokenHasCorrectExpiry()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByEmailAsync("user@example.com", default))
            .ReturnsAsync(user);

        RefreshToken? capturedToken = null;
        _userRepo
            .Setup(x => x.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), default))
            .Callback<RefreshToken, CancellationToken>((t, _) => capturedToken = t);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        await _handler.Handle(command, default);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken!.Token.Should().Be("refresh-token");
        capturedToken.ExpiresAtUtc.Should().Be(RefreshExpiry);
    }

    private static User CreateActiveUser()
        => User.Create("user@example.com", BCrypt.Net.BCrypt.HashPassword(CorrectPassword), "Test User");

    private static LoginCommand BuildCommand(
        string email = "user@example.com",
        string password = CorrectPassword)
        => new(new LoginRequest { Email = email, Password = password });
}
