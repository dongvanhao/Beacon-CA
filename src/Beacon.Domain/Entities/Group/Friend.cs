using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Messaging;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group
{
    public class Friend : BaseEntity
    {
        public Guid UserId1 { get; set; }        // Min(senderId, receiverId)
        public Guid UserId2 { get; set; }        // Max(senderId, receiverId)
        public FriendType Type { get; set; }
        public Guid MessageGroupId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public User User1 { get; set; } = null!;
        public User User2 { get; set; } = null!;
        public MessageGroup MessageGroup { get; set; } = null!;

        public User GetOtherUser(Guid myId) => UserId1 == myId ? User2 : User1;
    }
}
