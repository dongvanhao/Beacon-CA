using Beacon.Domain.Common;
using Beacon.Domain.Entities.Storage;

namespace Beacon.Domain.Entities.Messaging
{
    public class MessageGroup : BaseEntity
    {
        public bool IsPrivate { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        /// <summary>Tên tuỳ chỉnh của nhóm. null = dùng tên peer (chat 1-1) hoặc để trống (nhóm nhiều người).</summary>
        public string? Name { get; set; }

        /// <summary>FK đến MediaObject dùng làm avatar của nhóm. null = dùng avatar peer (chat 1-1) hoặc không có.</summary>
        public Guid? AvatarMediaObjectId { get; set; }
        public MediaObject? AvatarMedia { get; set; }

        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAtUtc { get; private set; }

        public ICollection<MessageGroupMember> Members { get; set; } = [];
        public ICollection<Message> Messages { get; set; } = [];

        public void Delete()
        {
            IsDeleted = true;
            DeletedAtUtc = DateTime.UtcNow;
        }
    }
}
