namespace Beacon.Application.Common.Interfaces.IService;

public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default);

    Task NotifyNewMessageAsync(Guid groupId, object messageDto, CancellationToken ct = default);

    Task NotifyTypingAsync(Guid groupId, Guid typingUserId, bool isTyping, CancellationToken ct = default);

    Task NotifyMessageSeenAsync(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default);
}
