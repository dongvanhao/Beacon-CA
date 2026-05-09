using Beacon.Api.Hubs;
using Beacon.Application.Common.Interfaces.IHubs;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Hub;

public class BeaconHubConnectionTests
{
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<HubCallerContext> _contextMock = new();
    private readonly Mock<ILogger<BeaconHub>> _loggerMock = new();
    private readonly Mock<IHubCallerClients<IBeaconHub>> _clientsMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly BeaconHub _sut;

    private const string ConnectionId = "test-conn-id";

    public BeaconHubConnectionTests()
    {
        _contextMock.Setup(c => c.ConnectionId).Returns(ConnectionId);
        _groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new BeaconHub(_mediatorMock.Object, _loggerMock.Object)
        {
            Context = _contextMock.Object,
            Groups = _groupsMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task OnConnectedAsync_ShouldAddToPersonalRoom_WhenUserIdentifierIsSet()
    {
        var userId = Guid.NewGuid().ToString();
        _contextMock.Setup(c => c.UserIdentifier).Returns(userId);

        await _sut.OnConnectedAsync();

        _groupsMock.Verify(
            g => g.AddToGroupAsync(ConnectionId, $"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_ShouldAbortConnection_WhenUserIdentifierIsNull()
    {
        _contextMock.Setup(c => c.UserIdentifier).Returns((string?)null);

        await _sut.OnConnectedAsync();

        _contextMock.Verify(c => c.Abort(), Times.Once);
        _groupsMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnDisconnectedAsync_ShouldNotThrow_WhenExceptionIsNull()
    {
        _contextMock.Setup(c => c.UserIdentifier).Returns(Guid.NewGuid().ToString());

        var act = async () => await _sut.OnDisconnectedAsync(null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnDisconnectedAsync_ShouldNotThrow_WhenExceptionProvided()
    {
        _contextMock.Setup(c => c.UserIdentifier).Returns(Guid.NewGuid().ToString());

        var act = async () => await _sut.OnDisconnectedAsync(new InvalidOperationException("test error"));

        await act.Should().NotThrowAsync();
    }
}
