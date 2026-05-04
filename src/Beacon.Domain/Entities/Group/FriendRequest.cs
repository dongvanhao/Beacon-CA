using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group
{
    public class FriendRequest : BaseEntity
    {
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }
        public FriendRequestStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public User Sender { get; set; } = null!;
    }
}
