using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Beacon.Api.Services;

public class SignalRRealtimeNotifier(IHubContext<BeaconHub, IBeaconHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyUserAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default)
        => hubContext.Clients.Group($"user:{userId}").ReceiveNotification(payload);

    public Task NotifyNewMessageAsync(Guid groupId, object messageDto, CancellationToken ct = default)
        => hubContext.Clients.Group($"message_group:{groupId}").ReceiveNewMessage(messageDto);

    public Task NotifyTypingAsync(Guid groupId, Guid typingUserId, bool isTyping, CancellationToken ct = default)
        => hubContext.Clients.Group($"message_group:{groupId}")
            .ReceiveTypingStatus(groupId, typingUserId, isTyping);

    public Task NotifyMessageSeenAsync(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default)
        => hubContext.Clients.Group($"message_group:{groupId}")
            .ReceiveMessageSeen(groupId, seenByUserId, lastSeenMessageId);
}
