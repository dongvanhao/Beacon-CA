using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Messaging;

namespace Beacon.Domain.Entities.Messaging
{
    // No BaseEntity — composite PK (GroupId, UserId)
    public class MessageGroupMember
    {
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public GroupMemberRole Role { get; set; }
        public DateTime JoinedAtUtc { get; set; }
        public Guid? InvitedByUserId { get; set; }
        public Guid? LastSeenMessageId { get; set; }

        public MessageGroup Group { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
