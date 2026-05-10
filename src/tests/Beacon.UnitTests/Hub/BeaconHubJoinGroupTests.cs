using Beacon.Api.Hubs;
using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Messaging.Queries.CheckGroupMembership;
using Beacon.Shared.Constants;
using Beacon.Shared.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace Beacon.UnitTests.Hub;

public class BeaconHubJoinGroupTests
{
    private readonly Mock<IGroupManager> _groupsMock = new();
    private readonly Mock<HubCallerContext> _contextMock = new();
    private readonly Mock<ILogger<BeaconHub>> _loggerMock = new();
    private readonly Mock<IHubCallerClients<IBeaconHub>> _clientsMock = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<IBeaconHub> _callerClientMock = new();
    private readonly BeaconHub _sut;

    private const string ConnectionId = "test-conn-id";
    private readonly Guid _userId = Guid.NewGuid();

    public BeaconHubJoinGroupTests()
    {
        _contextMock.Setup(c => c.ConnectionId).Returns(ConnectionId);
        _contextMock.Setup(c => c.UserIdentifier).Returns(_userId.ToString());

        _groupsMock
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _clientsMock.Setup(c => c.Caller).Returns(_callerClientMock.Object);
        _callerClientMock
            .Setup(c => c.ReceiveError(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _sut = new BeaconHub(_mediatorMock.Object, _loggerMock.Object)
        {
            Context = _contextMock.Object,
            Groups = _groupsMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task JoinMessageGroup_ShouldAddToRoom_WhenUserIsMember()
    {
        var groupId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CheckGroupMembershipQuery>(q => q.UserId == _userId && q.GroupId == groupId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        var result = await _sut.JoinMessageGroup(new JoinMessageGroupRequest(groupId));

        result.Success.Should().BeTrue();
        result.MessageGroupId.Should().Be(groupId);
        result.Room.Should().Be($"message_group:{groupId}");
        result.ErrorMessage.Should().BeNull();

        _groupsMock.Verify(
            g => g.AddToGroupAsync(ConnectionId, $"message_group:{groupId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinMessageGroup_ShouldReturnError_WhenUserNotMember()
    {
        var groupId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CheckGroupMembershipQuery>(q => q.UserId == _userId && q.GroupId == groupId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure(
                Error.Forbidden(ErrorCodes.Messaging.MESSAGE_GROUP_FORBIDDEN, "Bạn không thuộc nhóm chat này.")));

        var result = await _sut.JoinMessageGroup(new JoinMessageGroupRequest(groupId));

        result.Success.Should().BeFalse();
        result.Room.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();

        _groupsMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinMessageGroup_ShouldReturnError_WhenGroupNotFound()
    {
        var groupId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(
                It.Is<CheckGroupMembershipQuery>(q => q.GroupId == groupId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure(
                Error.NotFound(ErrorCodes.Messaging.MESSAGE_GROUP_NOT_FOUND, "Không tìm thấy nhóm chat.")));

        var result = await _sut.JoinMessageGroup(new JoinMessageGroupRequest(groupId));

        result.Success.Should().BeFalse();
        result.Room.Should().BeNull();

        _groupsMock.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
