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
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly RegisterCommandHandler _handler;

    private static readonly DateTime AccessExpiry = DateTime.UtcNow.AddMinutes(15);
    private static readonly DateTime RefreshExpiry = DateTime.UtcNow.AddDays(7);

    public RegisterCommandHandlerTests()
    {
        _jwtService
            .Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns(("access-token", AccessExpiry));
        _jwtService
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("refresh-token", RefreshExpiry));

        _handler = new RegisterCommandHandler(_userRepo.Object, _jwtService.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsConflictResult()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByEmailAsync("test@example.com", default))
            .ReturnsAsync(true);

        var command = BuildCommand("test@example.com");

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
            .Setup(x => x.ExistsByEmailAsync("new@example.com", default))
            .ReturnsAsync(false);

        var command = BuildCommand("new@example.com", fullName: "John Doe");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("new@example.com");
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
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        var command = BuildCommand("new@example.com");

        // Act
        await _handler.Handle(command, default);

        // Assert
        _userRepo.Verify(x => x.AddAsync(It.IsAny<User>(), default), Times.Once);
        _userRepo.Verify(x => x.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _userRepo.Verify(x => x.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidRequest_NormalizesEmailToLowercase()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), default))
            .ReturnsAsync(false);

        User? capturedUser = null;
        _userRepo
            .Setup(x => x.AddAsync(It.IsAny<User>(), default))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u);

        var command = BuildCommand("UPPER@Example.COM");

        // Act
        await _handler.Handle(command, default);

        // Assert
        capturedUser.Should().NotBeNull();
        capturedUser!.Email.Should().Be("upper@example.com");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_HashesPasswordBeforeSaving()
    {
        // Arrange
        _userRepo
            .Setup(x => x.ExistsByEmailAsync(It.IsAny<string>(), default))
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
        string email = "user@example.com",
        string password = "Password123",
        string fullName = "Test User",
        string? phoneNumber = null)
        => new(new RegisterRequest
        {
            Email = email,
            Password = password,
            FullName = fullName,
            PhoneNumber = phoneNumber
        });
}
