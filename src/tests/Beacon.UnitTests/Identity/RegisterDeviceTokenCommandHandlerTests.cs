using Beacon.Application.Features.Identity.Commands.RegisterDeviceToken;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Identity;
using Beacon.Domain.IRepository.Identity;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class RegisterDeviceTokenCommandHandlerTests
{
    private readonly Mock<IUserDeviceTokenRepository> _repoMock = new();
    private readonly RegisterDeviceTokenCommandHandler _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public RegisterDeviceTokenCommandHandlerTests()
    {
        _sut = new RegisterDeviceTokenCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreateNewToken_WhenTokenNotExists()
    {
        _repoMock
            .Setup(r => r.GetByTokenAsync("fcm-new-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDeviceToken?)null);
        _repoMock
            .Setup(r => r.AddAsync(It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new RegisterDeviceTokenCommand(_userId, "fcm-new-token", DevicePlatform.Android, null, "Pixel 7", "2.0.0"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(
            r => r.AddAsync(
                It.Is<UserDeviceToken>(t => t.Token == "fcm-new-token" && t.UserId == _userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRecordUsage_WhenTokenBelongsToSameUser()
    {
        var existing = UserDeviceToken.Create(_userId, "fcm-existing", DevicePlatform.iOS);

        _repoMock
            .Setup(r => r.GetByTokenAsync("fcm-existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new RegisterDeviceTokenCommand(_userId, "fcm-existing", DevicePlatform.iOS, null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.IsActive.Should().BeTrue();
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldTransferToken_WhenTokenBelongsToAnotherUser()
    {
        var otherUserId = Guid.NewGuid();
        var existing = UserDeviceToken.Create(otherUserId, "fcm-shared", DevicePlatform.Android);

        _repoMock
            .Setup(r => r.GetByTokenAsync("fcm-shared", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new RegisterDeviceTokenCommand(_userId, "fcm-shared", DevicePlatform.Android, null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.UserId.Should().Be(_userId);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
