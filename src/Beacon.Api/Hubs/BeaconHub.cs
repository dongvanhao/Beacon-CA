using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Messaging.Queries.CheckGroupMembership;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Beacon.Api.Hubs;

[Authorize]
public class BeaconHub(IMediator mediator, ILogger<BeaconHub> logger) : Hub<IBeaconHub>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        logger.LogInformation("Hub connected: userId={UserId}, connId={ConnId}", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        logger.LogInformation("Hub disconnected: userId={UserId}, connId={ConnId}, error={Error}",
            userId, Context.ConnectionId, exception?.Message);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task<JoinGroupResult> JoinMessageGroup(JoinMessageGroupRequest request)
    {
        var userId = Context.UserIdentifier;
        if (userId is null)
            return new JoinGroupResult(false, request.MessageGroupId, null, "Không xác định được người dùng.");

        var parsedUserId = Guid.Parse(userId);
        var result = await mediator.Send(
            new CheckGroupMembershipQuery(parsedUserId, request.MessageGroupId));

        if (!result.IsSuccess)
            return new JoinGroupResult(false, request.MessageGroupId, null, result.Error.Message);

        var roomName = $"message_group:{request.MessageGroupId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);

        logger.LogInformation("User {UserId} joined room {Room}", userId, roomName);

        return new JoinGroupResult(true, request.MessageGroupId, roomName, null);
    }
}
