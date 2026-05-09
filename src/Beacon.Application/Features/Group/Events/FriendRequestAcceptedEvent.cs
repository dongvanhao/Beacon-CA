using MediatR;

namespace Beacon.Application.Features.Group.Events;

/// <summary>Published after a friend request is accepted. Messaging domain listens to create the DM group.</summary>
public record FriendRequestAcceptedEvent(
    Guid FriendRequestId,
    Guid SenderId,
    Guid ReceiverId,
    Guid FriendId
) : INotification;
