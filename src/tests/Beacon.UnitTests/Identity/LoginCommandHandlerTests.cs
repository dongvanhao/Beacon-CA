using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Application.Mappings.Identity;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserDeviceRepository> _deviceRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly LoginCommandHandler _handler;

    private static readonly DateTime AccessExpiry = DateTime.UtcNow.AddMinutes(15);
    private static readonly DateTime RefreshExpiry = DateTime.UtcNow.AddDays(7);

    private const string CorrectPassword = "Password123";

    public LoginCommandHandlerTests()
    {
        _jwtService
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(("access-token", AccessExpiry));
        _jwtService
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("refresh-token", RefreshExpiry));

        // Default: no active tokens to revoke
        _userRepo
            .Setup(x => x.GetActiveRefreshTokensByUserIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(new List<RefreshToken>());

        _handler = new LoginCommandHandler(_userRepo.Object, _deviceRepo.Object, _jwtService.Object, new UserAuthMapper());
    }

    [Fact]
    public async Task Handle_WhenUsernameNotFound_ReturnsUnauthorizedResult()
    {
        // Arrange
        _userRepo
            .Setup(x => x.GetByUsernameAsync("notfound", default))
            .ReturnsAsync((User?)null);

        var command = BuildCommand("notfound");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAccountIsInactive_ReturnsUnauthorizedResult()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate();

        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsWrong_ReturnsUnauthorizedResult()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: "WrongPassword");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        _jwtService.Verify(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_ReturnsAuthResponseWithTokens()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Username.Should().Be("testuser");
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.AccessTokenExpiresAt.Should().Be(AccessExpiry);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_SavesDeviceAndRefreshTokenAndRecordsLogin()
    {
        // Arrange
        var user = CreateActiveUser();
        var loginTimeBefore = DateTime.UtcNow;

        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
            .ReturnsAsync(user);

        var command = BuildCommand(password: CorrectPassword);

        // Act
        await _handler.Handle(command, default);

        // Assert
        _deviceRepo.Verify(x => x.AddAsync(It.IsAny<UserDevice>(), default), Times.Once);
        _userRepo.Verify(x => x.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
        user.LastLoginAtUtc.Should().NotBeNull();
        user.LastLoginAtUtc.Should().BeOnOrAfter(loginTimeBefore);
    }

    [Fact]
    public async Task Handle_WhenValidCredentials_RefreshTokenHasCorrectExpiry()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
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

    [Theory]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)", "iOS Device")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8)", "Android Device")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64)", "Web Browser")]
    [InlineData(null, "Unknown Device")]
    public async Task Handle_WhenValidCredentials_AutoDetectsDeviceFromUserAgent(
        string? userAgent, string expectedDeviceName)
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepo
            .Setup(x => x.GetByUsernameAsync("testuser", default))
            .ReturnsAsync(user);

        UserDevice? capturedDevice = null;
        _deviceRepo
            .Setup(x => x.AddAsync(It.IsAny<UserDevice>(), default))
            .Callback<UserDevice, CancellationToken>((d, _) => capturedDevice = d);

        var command = BuildCommand(password: CorrectPassword, userAgent: userAgent);

        // Act
        await _handler.Handle(command, default);

        // Assert
        capturedDevice.Should().NotBeNull();
        capturedDevice!.DeviceName.Should().Be(expectedDeviceName);
    }

    private static User CreateActiveUser()
        => User.Create(
            username: "testuser",
            email: "test@example.com",
            passwordHash: BCrypt.Net.BCrypt.HashPassword(CorrectPassword),
            familyName: "Test",
            givenName: "User");

    private static LoginCommand BuildCommand(
        string username = "testuser",
        string password = CorrectPassword,
        string? userAgent = null)
        => new(new LoginRequest { Username = username, Password = password }, userAgent);
}
