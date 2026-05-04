using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;

namespace Beacon.Domain.Entities.Messaging
{
    public class Message : BaseEntity
    {
        public Guid GroupId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public MessageGroup Group { get; set; } = null!;
        public User Sender { get; set; } = null!;
    }
}
