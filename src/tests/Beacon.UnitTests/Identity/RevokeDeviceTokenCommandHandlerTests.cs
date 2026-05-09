using Beacon.Application.Features.Identity.Commands.RevokeDeviceToken;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Identity;
using Beacon.Domain.IRepository.Identity;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Identity;

public class RevokeDeviceTokenCommandHandlerTests
{
    private readonly Mock<IUserDeviceTokenRepository> _repoMock = new();
    private readonly RevokeDeviceTokenCommandHandler _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public RevokeDeviceTokenCommandHandlerTests()
    {
        _sut = new RevokeDeviceTokenCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldDeactivateToken_WhenTokenExists()
    {
        var existing = UserDeviceToken.Create(_userId, "fcm-token", DevicePlatform.Android);

        _repoMock
            .Setup(r => r.GetByTokenAsync("fcm-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(
            new RevokeDeviceTokenCommand(_userId, "fcm-token"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.IsActive.Should().BeFalse();
        existing.RevokedAtUtc.Should().NotBeNull();
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenTokenNotExists()
    {
        _repoMock
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDeviceToken?)null);

        var result = await _sut.Handle(
            new RevokeDeviceTokenCommand(_userId, "nonexistent"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
