using Beacon.Application.Common.Interfaces.IService;

namespace Beacon.Application.Common.Interfaces.IHubs;

public interface IBeaconHub
{
    Task ReceiveNotification(NotificationPayload payload);
    Task ReceiveNewMessage(object messageDto);
    Task ReceiveTypingStatus(Guid groupId, Guid typingUserId, bool isTyping);
    Task ReceiveMessageSeen(Guid groupId, Guid seenByUserId, Guid lastSeenMessageId);
    Task ReceiveError(string code, string message);
}
