using Beacon.Domain.Common;
using Beacon.Domain.Entities.Identity;
using Beacon.Domain.Entities.Posts;

namespace Beacon.Domain.Entities.Messaging
{
    public class Message : BaseEntity
    {
        public Guid GroupId { get; private set; }
        public Guid SenderId { get; private set; }
        public string Content { get; private set; } = string.Empty;
        public DateTime CreatedAtUtc { get; private set; }
        public long SequenceNumber { get; private set; } // DB-generated IDENTITY, không set trong code
        public bool IsDeleted { get; private set; }
        public DateTime? DeletedAtUtc { get; private set; }
        public DateTime? EditedAtUtc { get; private set; }
        public string? ClientMessageId { get; private set; }
        public Guid? ReplyToMessageId { get; private set; }
        public Guid? PostId { get; private set; }

        public MessageGroup Group { get; private set; } = null!;
        public User Sender { get; private set; } = null!;
        public Post? Post { get; private set; }

        private Message() { }

        public static Message Create(
            Guid groupId,
            Guid senderId,
            string content,
            string? clientMessageId = null,
            Guid? postId = null)
            => new()
            {
                GroupId = groupId,
                SenderId = senderId,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow,
                ClientMessageId = clientMessageId,
                PostId = postId
            };

        public void SoftDelete()
        {
            IsDeleted = true;
            DeletedAtUtc = DateTime.UtcNow;
            Content = string.Empty;
        }

        public void Edit(string newContent)
        {
            Content = newContent;
            EditedAtUtc = DateTime.UtcNow;
        }
    }
}
