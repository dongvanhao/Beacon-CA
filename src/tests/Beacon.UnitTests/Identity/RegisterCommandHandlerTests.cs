using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Identity.Commands;
using Beacon.Application.Features.Identity.Dtos;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.IRepository;
using Beacon.Shared.Results;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserDeviceRepository> _deviceRepo = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly RegisterCommandHandler _handler;

    private static readonly DateTime AccessExpiry = DateTime.UtcNow.AddMinutes(15);
    private static readonly DateTime RefreshExpiry = DateTime.UtcNow.AddDays(7);

    public RegisterCommandHandlerTests()
    {
        _jwtService
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>(), It.IsAny<Guid>()))
            .Returns(("access-token", AccessExpiry));
        _jwtService
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("refresh-token", RefreshExpiry));

        _handler = new RegisterCommandHandler(_userRepo.Object, _deviceRepo.Object, _jwtService.Object);
    }

    [Fact]
    public async Task Handle_WhenUsernameAlreadyExists_ReturnsConflictResult()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByUsernameAsync("existinguser", default))
            .ReturnsAsync(true);

        var command = BuildCommand("existinguser");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        _userRepo.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_ReturnsAuthResponseWithTokens()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByUsernameAsync("newuser", default))
            .ReturnsAsync(false);

        var command = BuildCommand("newuser", fullName: "John Doe");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Username.Should().Be("newuser");
        result.Value.FullName.Should().Be("John Doe");
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.AccessTokenExpiresAt.Should().Be(AccessExpiry);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_SavesUserAndRefreshToken()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var command = BuildCommand("newuser");

        // Act
        await _handler.Handle(command, default);

        // Assert
        _userRepo.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Once);
        _deviceRepo.Verify(x => x.AddAsync(It.IsAny<UserDevice>(), default), Times.Once);
        _userRepo.Verify(x => x.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenValidRequest_NormalizesUsernameToLowercase()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        User? capturedUser = null;
        _userRepo
            .Setup(x => x.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u);

        var command = BuildCommand("UPPERCASE_User");

        // Act
        await _handler.Handle(command, default);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.Username.Should().Be("uppercase_user");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_HashesPasswordBeforeSaving()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByUsernameAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        User? capturedUser = null;
        _userRepo
            .Setup(x => x.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u);

        var command = BuildCommand(password: "PlainTextPassword");

        // Act
        await _handler.Handle(command, default);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.PasswordHash.Should().NotBe("PlainTextPassword");
        BCrypt.Net.BCrypt.Verify("PlainTextPassword", capturedUser.PasswordHash).Should().BeTrue();
    }

    private static RegisterCommand BuildCommand(
        string username = "testuser",
        string password = "Password123",
        string fullName = "Test User",
        string? phoneNumber = null)
        => new(new RegisterRequest
        {
            Username = username,
            Password = password,
            FullName = fullName,
            PhoneNumber = phoneNumber
        });
}
