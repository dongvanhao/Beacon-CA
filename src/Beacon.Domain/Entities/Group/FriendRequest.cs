using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Enums.Group;

namespace Beacon.Domain.Entities.Group
{
    public class FriendRequest : BaseEntity
    {
        /// <summary>Luôn là ID nhỏ hơn trong cặp (UserId1 &lt; UserId2). Normalized để tránh duplicate constraint.</summary>
        public Guid UserId1 { get; private set; }

        /// <summary>Luôn là ID lớn hơn trong cặp.</summary>
        public Guid UserId2 { get; private set; }

        /// <summary>ID người thực sự gửi lời mời.</summary>
        public Guid InitiatorId { get; private set; }

        public FriendRequestStatus Status { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }

        // FIX-05: optimistic concurrency
        public byte[] RowVersion { get; private set; } = null!;

        public User Initiator { get; private set; } = null!;

        /// <summary>ID người nhận lời mời (người không phải initiator).</summary>
        public Guid ReceiverUserId => UserId1 == InitiatorId ? UserId2 : UserId1;

        public static FriendRequest Create(Guid senderId, Guid receiverId)
        {
            var (u1, u2) = senderId < receiverId ? (senderId, receiverId) : (receiverId, senderId);
            return new FriendRequest
            {
                UserId1 = u1,
                UserId2 = u2,
                InitiatorId = senderId,
                Status = FriendRequestStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public void Accept() => Status = FriendRequestStatus.Accepted;
        public void Cancel() => Status = FriendRequestStatus.Cancelled;
        public void Decline() => Status = FriendRequestStatus.Declined;
    }
}
