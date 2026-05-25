namespace Beacon.Application.Common.Interfaces.IService;

public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default);

    Task NotifyUserPresenceAsync(Guid userId, UserPresencePayload payload, CancellationToken ct = default);

    Task NotifyNewMessageAsync(Guid groupId, object messageDto, CancellationToken ct = default);

    Task NotifyNewMessageAsync(
        Guid groupId,
        object messageDto,
        IReadOnlyCollection<Guid> recipientUserIds,
        CancellationToken ct = default);

    Task NotifyNewPostAsync(
        object postDto,
        IReadOnlyCollection<Guid> recipientUserIds,
        CancellationToken ct = default);

    Task NotifyTypingAsync(Guid groupId, Guid typingUserId, bool isTyping, CancellationToken ct = default);

    Task NotifyMessageSeenAsync(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default);

    Task NotifyMessageGroupSeenAsync(Guid userId, Guid groupId, Guid lastSeenMessageId, CancellationToken ct = default);

    Task NotifyUnreadMessageCountAsync(Guid userId, Guid groupId, int unreadCount, CancellationToken ct = default);
}
