using Beacon.Application.Common.Interfaces.IHubs;
using Beacon.Application.Common.Interfaces.IService;
using Beacon.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Beacon.Api.Services;

public class SignalRRealtimeNotifier(IHubContext<BeaconHub, IBeaconHub> hubContext) : IRealtimeNotifier
{
    public Task NotifyUserAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default)
        => hubContext.Clients.User(userId.ToString()).ReceiveNotification(payload);

    public Task NotifyNewMessageAsync(
        Guid groupId, IEnumerable<Guid> memberIds, object messageDto, CancellationToken ct = default)
        => Task.WhenAll(memberIds.Select(id =>
            hubContext.Clients.User(id.ToString()).ReceiveNewMessage(messageDto)));

    public Task NotifyTypingAsync(
        Guid groupId, IEnumerable<Guid> otherMemberIds, Guid typingUserId, bool isTyping,
        CancellationToken ct = default)
        => Task.WhenAll(otherMemberIds
            .Where(id => id != typingUserId)
            .Select(id => hubContext.Clients.User(id.ToString())
                .ReceiveTypingStatus(groupId, typingUserId, isTyping)));

    public Task NotifyMessageSeenAsync(
        Guid groupId, IEnumerable<Guid> otherMemberIds, Guid seenByUserId, Guid lastSeenMessageId,
        CancellationToken ct = default)
        => Task.WhenAll(otherMemberIds
            .Where(id => id != seenByUserId)
            .Select(id => hubContext.Clients.User(id.ToString())
                .ReceiveMessageSeen(groupId, seenByUserId, lastSeenMessageId)));
}
