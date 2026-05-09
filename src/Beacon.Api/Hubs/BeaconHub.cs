using Beacon.Application.Common.Interfaces.IHubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Beacon.Api.Hubs;

[Authorize]
public class BeaconHub(ILogger<BeaconHub> logger) : Hub<IBeaconHub>
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
}
