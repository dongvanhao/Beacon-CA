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

    public Task NotifyNewMessageAsync(
        Guid groupId,
        object messageDto,
        IReadOnlyCollection<Guid> recipientUserIds,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>
        {
            hubContext.Clients.Group($"message_group:{groupId}").ReceiveNewMessage(messageDto)
        };

        tasks.AddRange(recipientUserIds
            .Distinct()
            .Select(userId => hubContext.Clients.Group($"user:{userId}").ReceiveNewMessage(messageDto)));

        return Task.WhenAll(tasks);
    }

    public Task NotifyTypingAsync(Guid groupId, Guid typingUserId, bool isTyping, CancellationToken ct = default)
        => hubContext.Clients.Group($"message_group:{groupId}")
            .ReceiveTypingStatus(groupId, typingUserId, isTyping);

    public Task NotifyMessageSeenAsync(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default)
        => hubContext.Clients.Group($"message_group:{groupId}")
            .ReceiveMessageSeen(groupId, seenByUserId, lastSeenMessageId);

    public Task NotifyMessageGroupSeenAsync(Guid userId, Guid groupId, Guid lastSeenMessageId, CancellationToken ct = default)
        => hubContext.Clients.Group($"user:{userId}").ReceiveMessageGroupSeen(groupId, lastSeenMessageId);

    public Task NotifyUnreadMessageCountAsync(Guid userId, Guid groupId, int unreadCount, CancellationToken ct = default)
        => hubContext.Clients.Group($"user:{userId}").ReceiveUnreadMessageCount(groupId, unreadCount);
}
