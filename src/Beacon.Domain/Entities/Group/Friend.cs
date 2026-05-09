using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group
{
    public class Friend : BaseEntity
    {
        public Guid UserId1 { get; set; }        // Min(senderId, receiverId)
        public Guid UserId2 { get; set; }        // Max(senderId, receiverId)
        public FriendType Type { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public User User1 { get; set; } = null!;
        public User User2 { get; set; } = null!;

        public static Friend Create(Guid senderId, Guid receiverId)
        {
            var (u1, u2) = FriendPair.Normalize(senderId, receiverId);
            return new Friend { UserId1 = u1, UserId2 = u2, Type = FriendType.Normal, CreatedAtUtc = DateTime.UtcNow };
        }

        public User GetOtherUser(Guid myId) => UserId1 == myId ? User2 : User1;
    }
}
