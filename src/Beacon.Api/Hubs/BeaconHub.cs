using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Application.Features.Messaging.Dtos;
using Beacon.Application.Features.Messaging.Queries.CheckGroupMembership;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Beacon.Api.Hubs;

[Authorize]
public class BeaconHub(
    IMediator mediator,
    ILogger<BeaconHub> logger,
    IMessageGroupPresenceTracker presenceTracker,
    IUserOnlineTracker onlineTracker,
    IUserPresenceService presenceService) : Hub<IBeaconHub>
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

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            onlineTracker.TrackOnline(parsedUserId, Context.ConnectionId);
            try
            {
                await presenceService.MarkOnlineAsync(parsedUserId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Presence update failed for user {UserId}", parsedUserId);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        logger.LogInformation("Hub disconnected: userId={UserId}, connId={ConnId}, error={Error}",
            userId, Context.ConnectionId, exception?.Message);

        if (userId is not null && Guid.TryParse(userId, out var parsedUserId))
        {
            presenceTracker.TrackDisconnect(parsedUserId, Context.ConnectionId);
            onlineTracker.TrackOffline(parsedUserId, Context.ConnectionId);

            if (!onlineTracker.IsOnline(parsedUserId))
            {
                try
                {
                    await presenceService.MarkOfflineAsync(parsedUserId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Presence update failed for user {UserId}", parsedUserId);
                }
            }
        }

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
        presenceTracker.TrackJoin(parsedUserId, request.MessageGroupId, Context.ConnectionId);

        logger.LogInformation("User {UserId} joined room {Room}", userId, roomName);

        return new JoinGroupResult(true, request.MessageGroupId, roomName, null);
    }

    public async Task<JoinGroupResult> LeaveMessageGroup(LeaveMessageGroupRequest request)
    {
        var userId = Context.UserIdentifier;
        if (userId is null)
            return new JoinGroupResult(false, request.MessageGroupId, null, "Không xác định được người dùng.");

        var roomName = $"message_group:{request.MessageGroupId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomName);
        presenceTracker.TrackLeave(Guid.Parse(userId), request.MessageGroupId, Context.ConnectionId);

        logger.LogInformation("User {UserId} left room {Room}", userId, roomName);

        return new JoinGroupResult(true, request.MessageGroupId, roomName, null);
    }

    public async Task<JoinGroupResult> SendTypingStatus(TypingStatusRequest request)
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
        await Clients.OthersInGroup(roomName)
            .ReceiveTypingStatus(request.MessageGroupId, parsedUserId, request.IsTyping);

        return new JoinGroupResult(true, request.MessageGroupId, roomName, null);
    }
}
