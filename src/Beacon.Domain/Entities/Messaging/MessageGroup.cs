using Beacon.Domain.Common;

namespace Beacon.Domain.Entities.Messaging
{
    public class MessageGroup : BaseEntity
    {
        public bool IsPrivate { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public ICollection<MessageGroupMember> Members { get; set; } = [];
        public ICollection<Message> Messages { get; set; } = [];
    }
}
