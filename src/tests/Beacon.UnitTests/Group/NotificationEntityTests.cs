using Beacon.Domain.Entities.Group;
using Beacon.Domain.Enums.Group;
using FluentAssertions;

namespace Beacon.UnitTests.Group;

public class NotificationEntityTests
{
    [Fact]
    public void Create_ShouldInitializeWithIsReadFalse()
    {
        var receiverId = Guid.NewGuid();

        var notification = Notification.Create(
            receiverId,
            NotificationType.FriendRequest,
            "New friend request",
            "User X sent you a friend request");

        notification.IsRead.Should().BeFalse();
        notification.ReadAtUtc.Should().BeNull();
        notification.ReceiverUserId.Should().Be(receiverId);
        notification.Type.Should().Be(NotificationType.FriendRequest);
    }

    [Fact]
    public void MarkRead_ShouldSetIsReadTrue_AndSetReadAtUtc()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.FriendAccepted,
            "Request accepted",
            "User X accepted your friend request");

        notification.MarkRead();

        notification.IsRead.Should().BeTrue();
        notification.ReadAtUtc.Should().NotBeNull();
        notification.ReadAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkRead_WhenAlreadyRead_ShouldNotUpdateReadAtUtc()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.GroupMessage,
            "New message",
            "You have a new message");

        notification.MarkRead();
        var firstReadAt = notification.ReadAtUtc;

        // Wait a tiny bit then call again — ReadAtUtc must not change
        notification.MarkRead();

        notification.ReadAtUtc.Should().Be(firstReadAt);
    }
}
