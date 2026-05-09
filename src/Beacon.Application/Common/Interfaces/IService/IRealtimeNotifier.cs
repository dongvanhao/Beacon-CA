namespace Beacon.Application.Common.Interfaces.IService;

public interface IRealtimeNotifier
{
    Task NotifyUserAsync(Guid userId, NotificationPayload payload, CancellationToken ct = default);

    Task NotifyNewMessageAsync(Guid groupId, IEnumerable<Guid> memberIds, object messageDto, CancellationToken ct = default);

    Task NotifyTypingAsync(Guid groupId, IEnumerable<Guid> otherMemberIds, Guid typingUserId, bool isTyping, CancellationToken ct = default);

    Task NotifyMessageSeenAsync(Guid groupId, IEnumerable<Guid> otherMemberIds, Guid seenByUserId, Guid lastSeenMessageId, CancellationToken ct = default);
}
