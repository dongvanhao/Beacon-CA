using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Application.Common.Interfaces.IHubs;

public interface IBeaconHub
{
    Task ReceiveNotification(NotificationPayload payload);
    Task ReceiveUserPresence(UserPresencePayload payload);
    Task ReceiveNewMessage(object messageDto);
    Task ReceiveTypingStatus(Guid groupId, Guid typingUserId, bool isTyping);
    Task ReceiveMessageSeen(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId);
    Task ReceiveMessageGroupSeen(Guid groupId, Guid lastSeenMessageId);
    Task ReceiveUnreadMessageCount(Guid groupId, int unreadCount);
    Task ReceiveError(string code, string message);
}
