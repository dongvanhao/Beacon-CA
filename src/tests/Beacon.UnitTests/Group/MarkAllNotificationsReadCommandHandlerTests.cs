using Beacon.Application.Features.Group.Commands.MarkAllNotificationsRead;
using Beacon.Domain.IRepository.Group;
using FluentAssertions;
using Moq;

namespace Beacon.UnitTests.Group;

public class MarkAllNotificationsReadCommandHandlerTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();
    private readonly MarkAllNotificationsReadCommandHandler _sut;

    public MarkAllNotificationsReadCommandHandlerTests()
    {
        _sut = new MarkAllNotificationsReadCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnUnreadCountZero_AfterMarkAll()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.MarkAllReadAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadCount.Should().Be(0);
        _repoMock.Verify(r => r.MarkAllReadAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenNoUnreadExists()
    {
        var userId = Guid.NewGuid();
        _repoMock.Setup(r => r.MarkAllReadAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _sut.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UnreadCount.Should().Be(0);
    }
}
